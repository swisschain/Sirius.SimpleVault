using System;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using SimpleVault.Common.Cryptography;
using SimpleVault.Common.Domain;
using SimpleVault.Common.Exceptions;
using SimpleVault.Common.Persistence.Transactions;
using SimpleVault.Common.Persistence.Wallets;
using Swisschain.Sirius.VaultApi.ApiClient;
using Swisschain.Sirius.VaultApi.ApiContract.Transactions;
using Swisschain.Sirius.VaultApi.ApiContract.Common;

namespace SimpleVault.Worker.Jobs
{
    public class TransactionSigningProcessorJob : IDisposable
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly IEncryptionService _encryptionService;
        private readonly IVaultApiClient _vaultApiClient;
        private readonly ILogger<TransactionSigningProcessorJob> _logger;

        private readonly TimeSpan _delayBetweenRequestsUpdate;
        private readonly Timer _timer;
        private readonly ManualResetEventSlim _done;
        private readonly CancellationTokenSource _cts;

        public TransactionSigningProcessorJob(
            ITransactionRepository transactionRepository,
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

            _delayBetweenRequestsUpdate = TimeSpan.FromSeconds(1);

            _timer = new Timer(TimerCallback,
                null,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            _done = new ManualResetEventSlim(false);
            _cts = new CancellationTokenSource();
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
                    _timer.Change(_delayBetweenRequestsUpdate, Timeout.InfiniteTimeSpan);
            }

            if (_cts.IsCancellationRequested)
                _done.Set();
        }

        private async Task ProcessAsync()
        {
            var response = await _vaultApiClient.Transactions.GetAsync(new GetTransactionSigningRequestRequest());

            if (response.BodyCase == GetTransactionSigningRequestResponse.BodyOneofCase.Error)
            {
                _logger.LogError("An error occurred while getting transaction signing requests. {@context}",
                    new {response.Error.ErrorMessage});
                return;
            }

            foreach (var transactionSigningRequest in response.Response.Requests)
            {
                var context = new LoggingContext
                {
                    TransactionSigningRequestId = transactionSigningRequest.Id,
                    BlockchainId = transactionSigningRequest.BlockchainId,
                    DoubleSpendingProtectionType = transactionSigningRequest.DoubleSpendingProtectionType,
                    NetworkType = transactionSigningRequest.NetworkType
                };

                try
                {
                    _logger.LogInformation("Transaction signing request processing. {@context}", context);

                    var transaction = await SignTransactionAsync(transactionSigningRequest);

                    await _vaultApiClient.Transactions.ConfirmAsync(new ConfirmTransactionSigningRequestRequest
                    {
                        RequestId = $"Vault:Transaction:{transaction.TransactionSigningRequestId}",
                        TransactionSigningRequestId = transaction.TransactionSigningRequestId,
                        TransactionId = transaction.TransactionId,
                        SignedTransaction = ByteString.CopyFrom(transaction.SignedTransaction)
                    });

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

                    await _vaultApiClient.Transactions.RejectAsync(new RejectTransactionSigningRequestRequest
                    {
                        RequestId = $"Vault:Transaction:{transactionSigningRequest.Id}",
                        TransactionSigningRequestId = transactionSigningRequest.Id,
                        ReasonMessage = "BlockchainId is not supported",
                        Reason = TransactionSigningRequestRejectionReason.UnknownBlockchain
                    });

                    _logger.LogInformation("Transaction signing request rejected. {@context}", context);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception,
                        "An error occurred while processing transaction signing request. {@context}",
                        context);

                    await _vaultApiClient.Transactions.RejectAsync(
                        new RejectTransactionSigningRequestRequest
                        {
                            RequestId = $"Vault:Transaction:{transactionSigningRequest.Id}",
                            TransactionSigningRequestId = transactionSigningRequest.Id,
                            ReasonMessage = exception.Message,
                            Reason = TransactionSigningRequestRejectionReason.Other
                        });

                    _logger.LogInformation("Transaction signing request rejected. {@context}", context);
                }
            }
        }

        private async Task<Transaction> SignTransactionAsync(TransactionSigningRequest request)
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
                                    decimal.Parse(x.Value),
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

            return await _transactionRepository.AddOrGetAsync(transaction);
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
