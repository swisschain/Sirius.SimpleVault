using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Swisschain.Sirius.SimpleVault.Api.Client;
using SimpleVault.Common.Cryptography;
using SimpleVault.Common.Domain;
using SimpleVault.Common.Exceptions;
using SimpleVault.Common.Persistence;
using SimpleVault.Common.Persistence.Transactions;
using SimpleVault.Common.Persistence.Wallets;

namespace SimpleVault.Worker.Jobs
{
    public class TransactionSigningProcessorJob : IDisposable
    {
        private readonly ILogger<TransactionSigningProcessorJob> _logger;
        private readonly TimeSpan _delayBetweenRequestsUpdate;
        private readonly ISiriusApiClient _siriusApiClient;
        private readonly IEncryptionService _encryptionService;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICursorRepository _cursorRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly Timer _timer;
        private readonly ManualResetEventSlim _done;
        private readonly CancellationTokenSource _cts;
        private readonly AsyncRetryPolicy _retryPolicy;

        public TransactionSigningProcessorJob(ILogger<TransactionSigningProcessorJob> logger,
            ISiriusApiClient siriusApiClient,
            IEncryptionService encryptionService,
            ITransactionRepository transactionRepository,
            ICursorRepository cursorRepository,
            IWalletRepository walletRepository)
        {
            _logger = logger;
            _delayBetweenRequestsUpdate = TimeSpan.FromSeconds(1);
            _siriusApiClient = siriusApiClient;
            _encryptionService = encryptionService;
            _transactionRepository = transactionRepository;
            _cursorRepository = cursorRepository;
            _walletRepository = walletRepository;

            _timer = new Timer(TimerCallback,
                null,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            _done = new ManualResetEventSlim(false);
            _cts = new CancellationTokenSource();

            _logger.LogInformation($"{nameof(TransactionSigningProcessorJob)} is being created.");
            _retryPolicy = Policy
                .Handle<ApiException>()
                .WaitAndRetryAsync(3,
                    retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                );
        }

        public void Start()
        {
            _timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);

            _logger.LogInformation($"{nameof(TransactionSigningProcessorJob)} is being started.");
        }

        public void Stop()
        {
            _logger.LogInformation($"{nameof(TransactionSigningProcessorJob)} is being stopped.");

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
            _logger.LogDebug("transaction signing processing being started");

            try
            {
                ProcessAsync().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error while processing transaction signing");
            }
            finally
            {
                if (!_cts.IsCancellationRequested)
                {
                    _timer.Change(_delayBetweenRequestsUpdate, Timeout.InfiniteTimeSpan);
                }
            }

            if (_cts.IsCancellationRequested)
            {
                _done.Set();
            }

            _logger.LogDebug("transaction signing processing has been done");
        }

        private async Task ProcessAsync()
        {
            var existingCursor = await _cursorRepository.GetOrDefaultAsync(Cursor.TransactionId);
            var cursor = existingCursor?.CursorValue ?? 0;

            do
            {
                var paginatedResponse = await _siriusApiClient.ApiTransactionSigningRequestUpdatesAsync(
                    id: null,
                    cursor: cursor,
                    order: PaginationOrder.Asc,
                    limit: 100,
                    state: new[] {TransactionSigningRequestState.Pending},
                    vaultType: null,
                    vaultId: null,
                    component: null,
                    blockchainId: null,
                    operationType: null,
                    operationId: null,
                    transactionSigningRequestId: null,
                    cancellationToken: _cts.Token);

                if (paginatedResponse.Pagination.Count == 0)
                    break;

                cursor = paginatedResponse.Items.Last().Id;

                foreach (var item in paginatedResponse.Items)
                {
                    var context = new LoggingContext
                    {
                        RequestId = item.Id,
                        BlockchainId = item.BlockchainId,
                        DoubleSpendingProtectionType = item.DoubleSpendingProtectionType,
                        NetworkType = item.NetworkType,
                        TransactionSigningRequestId = item.TransactionSigningRequestId
                    };

                    try
                    {
                        await ProcessRequestAsync(item);

                        _logger.LogInformation("Transaction signing request processed. {@context}", context);
                    }
                    catch (BlockchainIsNotSupportedException exception)
                    {
                        _logger.LogError(exception, "BlockchainId is not supported. {@context}", context);

                        await _retryPolicy.ExecuteAsync(async () =>
                        {
                            await _siriusApiClient.ApiTransactionSigningRequestUpdatesRejectAsync(
                                $"Vault:Transaction:{item.TransactionSigningRequestId}",
                                item.TransactionSigningRequestId,
                                new TransactionSigningRejectionRequest
                                {
                                    ReasonMessage = "BlockchainId is not supported",
                                    Reason = TransactionRejectionReason.UnknownBlockchain
                                });
                        });
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception,
                            "An error occurred while processing transaction signing request {@context}",
                            context);

                        if (!(exception is DbException))
                        {
                            await _retryPolicy.ExecuteAsync(async () =>
                            {
                                await _siriusApiClient.ApiTransactionSigningRequestUpdatesRejectAsync(
                                    $"Vault:Transaction:{item.TransactionSigningRequestId}",
                                    item.TransactionSigningRequestId,
                                    new TransactionSigningRejectionRequest
                                    {
                                        ReasonMessage = exception.Message,
                                        Reason = TransactionRejectionReason.Other
                                    });
                            });
                        }
                    }
                }

                var newCursor = Cursor.CreateForTransaction(cursor);
                await _cursorRepository.UpdateOrAddAsync(newCursor);
            } while (true);
        }

        private async Task ProcessRequestAsync(TransactionSigningRequestUpdateResponse request)
        {
            var protectionType = request.DoubleSpendingProtectionType switch
            {
                DoubleSpendingProtectionType.Coins =>
                Swisschain.Sirius.Sdk.Primitives.DoubleSpendingProtectionType.Coins,
                DoubleSpendingProtectionType.Nonce =>
                Swisschain.Sirius.Sdk.Primitives.DoubleSpendingProtectionType.Nonce,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(request.DoubleSpendingProtectionType),
                    request.DoubleSpendingProtectionType,
                    null)
            };

            var coins = request.CoinsToSpend?
                            .Select(x =>
                                new Swisschain.Sirius.Sdk.Primitives.Coin(
                                    new Swisschain.Sirius.Sdk.Primitives.CoinId(x.Id.TransactionId,
                                        x.Id.Number),
                                    new Swisschain.Sirius.Sdk.Primitives.BlockchainAsset(
                                        new Swisschain.Sirius.Sdk.Primitives.BlockchainAssetId(
                                            x.Asset.Id.Symbol,
                                            x.Asset.Id.Address),
                                        x.Asset.Accuracy),
                                    x.Value,
                                    x.ScriptPubKey,
                                    x.Redeem
                                ))
                            .ToArray() ?? Array.Empty<Swisschain.Sirius.Sdk.Primitives.Coin>();

            var transaction = await Transaction.Create(
                _walletRepository,
                _encryptionService,
                request.TransactionSigningRequestId,
                request.BlockchainId,
                request.CreatedAt.UtcDateTime,
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
                request.BuiltTransaction,
                protectionType,
                coins);

            transaction = await _transactionRepository.AddOrGetAsync(transaction);

            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _siriusApiClient.ApiTransactionSigningRequestUpdatesConfirmAsync(
                    $"Vault:Transaction:{transaction.TransactionSigningRequestId}",
                    transaction.TransactionSigningRequestId,
                    new TransactionSigningConfirmationRequest
                    {
                        TransactionId = transaction.TransactionId,
                        SignedTransaction = transaction.SignedTransaction
                    });
            });
        }

        #region Nested classes

        public class LoggingContext
        {
            public long RequestId { get; set; }

            public string BlockchainId { get; set; }

            public NetworkType NetworkType { get; set; }

            public DoubleSpendingProtectionType DoubleSpendingProtectionType { get; set; }

            public long TransactionSigningRequestId { get; set; }
        }

        #endregion
    }
}
