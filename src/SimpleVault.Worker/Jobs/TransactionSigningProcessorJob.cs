﻿using System;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using SimpleVault.Common.Cryptography;
using SimpleVault.Common.Domain;
using SimpleVault.Common.Exceptions;
using SimpleVault.Common.Persistence.Transactions;
using SimpleVault.Common.Persistence.Wallets;
using Swisschain.Sirius.VaultApi.ApiClient;
using Swisschain.Sirius.VaultApi.ApiContract.Common;
using Swisschain.Sirius.VaultApi.ApiContract.TransferSigninRequests;

namespace SimpleVault.Worker.Jobs
{
    public class TransactionSigningProcessorJob : IDisposable
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly IEncryptionService _encryptionService;
        private readonly IVaultApiClient _vaultApiClient;
        private readonly ILogger<TransactionSigningProcessorJob> _logger;

        private readonly TimeSpan _delay;
        private readonly TimeSpan _delayOnError;
        private readonly Timer _timer;
        private readonly ManualResetEventSlim _done;
        private readonly CancellationTokenSource _cts;

        private readonly AsyncRetryPolicy _retryPolicy;

        public TransactionSigningProcessorJob(ITransactionRepository transactionRepository,
            IWalletRepository walletRepository,
            IEncryptionService encryptionService,
            IVaultApiClient vaultApiClient,
            ILogger<TransactionSigningProcessorJob> logger)
        {
            _transactionRepository = transactionRepository;
            _walletRepository = walletRepository;
            _encryptionService = encryptionService;
            _vaultApiClient = vaultApiClient;
            _logger = logger;

            _delay = TimeSpan.FromSeconds(1);
            _delayOnError = TimeSpan.FromSeconds(30);

            _timer = new Timer(TimerCallback,
                null,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            _done = new ManualResetEventSlim(false);
            _cts = new CancellationTokenSource();

            _retryPolicy = Policy
                .Handle<DbUnavailableException>()
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(5, retryAttempt))
                );
        }

        public void Start()
        {
            _timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
            _logger.LogInformation($"{nameof(TransactionSigningProcessorJob)} started.");
        }

        public void Stop()
        {
            _logger.LogInformation($"{nameof(TransactionSigningProcessorJob)} stopped.");
            _cts.Cancel();
        }

        public void Wait()
        {
            _done.Wait();
        }

        public void Dispose()
        {
            _timer.Dispose();
            _cts.Dispose();
            _done.Dispose();
        }

        private void TimerCallback(object state)
        {
            try
            {
                ProcessAsync().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "An error occurred while processing transaction signing requests.");
            }
            finally
            {
                if (!_cts.IsCancellationRequested)
                    _timer.Change(_delay, Timeout.InfiniteTimeSpan);
            }

            if (_cts.IsCancellationRequested)
                _done.Set();
        }

        private async Task ProcessAsync()
        {
            var response = await _vaultApiClient.TransferSigningRequests.GetAsync(new GetTransferSigningRequestsRequest());

            if (response.BodyCase == GetTransferSigningRequestsResponse.BodyOneofCase.Error)
            {
                _logger.LogError("An error occurred while getting transfers signing requests. {@error}",
                    response.Error);
                await Task.Delay(_delayOnError);
                return;
            }

            foreach (var transferSigningRequest in response.Response.Requests)
            {
                var context = new LoggingContext
                {
                    TransactionSigningRequestId = transferSigningRequest.Id,
                    BlockchainId = transferSigningRequest.BlockchainId,
                    DoubleSpendingProtectionType = transferSigningRequest.DoubleSpendingProtectionType,
                    NetworkType = transferSigningRequest.NetworkType
                };

                try
                {
                    _logger.LogInformation("Transaction signing request processing. {@context}", context);

                    var transaction =
                        await _retryPolicy.ExecuteAsync(() => SignTransactionAsync(transferSigningRequest));

                    if (await ConfirmAsync(transaction, context))
                        _logger.LogInformation("Transaction signing request confirmed. {@context}", context);
                }
                catch (DbException exception)
                {
                    _logger.LogError(exception,
                        "An error occurred while attempting to access the database. {@context}",
                        context);

                    // silently retry
                }
                catch (BlockchainIsNotSupportedException exception)
                {
                    _logger.LogError(exception, "BlockchainId is not supported. {@context}", context);

                    if (await RejectAsync(transferSigningRequest.Id,
                        TransferSigningRequestRejectionReason.UnknownBlockchain,
                        "BlockchainId is not supported",
                        context))
                    {
                        _logger.LogInformation("Transaction signing request rejected. {@context}", context);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception,
                        "An error occurred while processing transaction signing request. {@context}",
                        context);

                    if (await RejectAsync(transferSigningRequest.Id,
                        TransferSigningRequestRejectionReason.Other,
                        exception.Message,
                        context))
                    {
                        _logger.LogInformation("Transaction signing request rejected. {@context}", context);
                    }
                }
            }
        }

        private async Task<Transaction> SignTransactionAsync(TransferSigningRequest request)
        {
            var protectionType = request.DoubleSpendingProtectionType switch
            {
                DoubleSpendingProtectionType.Coins =>
                Swisschain.Sirius.Sdk.Primitives.DoubleSpendingProtectionType.Coins,
                DoubleSpendingProtectionType.Nonce =>
                Swisschain.Sirius.Sdk.Primitives.DoubleSpendingProtectionType.Nonce,
                _ => throw new InvalidEnumArgumentException(
                    nameof(request.DoubleSpendingProtectionType),
                    (int) request.DoubleSpendingProtectionType,
                    typeof(DoubleSpendingProtectionType))
            };

            var coins = request.CoinsToSpend?
                            .Select(x =>
                                new Coin(
                                    new Swisschain.Sirius.Sdk.Primitives.CoinId(x.Id.TransactionId,
                                        x.Id.Number),
                                    new Swisschain.Sirius.Sdk.Primitives.BlockchainAsset(
                                        new Swisschain.Sirius.Sdk.Primitives.BlockchainAssetId(
                                            x.Asset.Id.Symbol,
                                            x.Asset.Id.Address),
                                        x.Asset.Accuracy),
                                    x.Value,
                                    x.Address,
                                    x.Redeem
                                ))
                            .ToArray() ?? Array.Empty<Coin>();

            var transaction = await Transaction.Create(
                _walletRepository,
                _encryptionService,
                request.Id,
                request.BlockchainId,
                request.CreatedAt.ToDateTime(),
                request.NetworkType switch
                {
                    NetworkType.Private => Swisschain.Sirius.Sdk.Primitives.NetworkType.Private,
                    NetworkType.Test => Swisschain.Sirius.Sdk.Primitives.NetworkType.Test,
                    NetworkType.Public => Swisschain.Sirius.Sdk.Primitives.NetworkType.Public,
                    _ => throw new ArgumentOutOfRangeException(nameof(request.NetworkType),
                        request.NetworkType,
                        null)
                },
                request.ProtocolCode,
                request.SigningAddresses?.ToArray(),
                request.BuiltTransaction.ToByteArray(),
                protectionType,
                coins);

            try
            {
                await _transactionRepository.InsertAsync(transaction);
            }
            catch (EntityAlreadyExistsException)
            {
                return await _transactionRepository.GetBySigningRequestIdAsync(request.Id);
            }

            return transaction;
        }

        private async Task<bool> ConfirmAsync(Transaction transaction, LoggingContext context)
        {
            var response = await _vaultApiClient.TransferSigningRequests.ConfirmAsync(
                new ConfirmTransferSigningRequestRequest()
                {
                    RequestId = $"Vault:Transaction:{transaction.TransactionSigningRequestId}",
                    TransferSigningRequestId = transaction.TransactionSigningRequestId,
                    TransactionId = transaction.TransactionId,
                    SignedTransaction = ByteString.CopyFrom(transaction.SignedTransaction)
                });

            if (response.BodyCase == ConfirmTransferSigningRequestResponse.BodyOneofCase.Error)
            {
                _logger.LogError(
                    "An error occurred while confirming transaction signing request. {@context} {@error}",
                    context,
                    response.Error);

                return false;
            }

            return true;
        }

        private async Task<bool> RejectAsync(long transactionSigningRequestId,
            TransferSigningRequestRejectionReason reason,
            string reasonMessage,
            LoggingContext context)
        {
            var response = await _vaultApiClient.TransferSigningRequests.RejectAsync(new RejectTransferSigningRequestRequest()
            {
                RequestId = $"Vault:Transfer:{transactionSigningRequestId}",
                TransferSigningRequestId = transactionSigningRequestId,
                ReasonMessage = reasonMessage,
                Reason = reason
            });

            if (response.BodyCase == RejectTransferSigningRequestResponse.BodyOneofCase.Error)
            {
                _logger.LogError(
                    "An error occurred while rejecting transaction signing request. {@context} {@error}",
                    context,
                    response.Error);

                return false;
            }

            return true;
        }

        #region Nested classes

        public class LoggingContext
        {
            public long TransactionSigningRequestId { get; set; }

            public string BlockchainId { get; set; }

            public NetworkType NetworkType { get; set; }

            public DoubleSpendingProtectionType DoubleSpendingProtectionType { get; set; }
        }

        #endregion
    }
}
