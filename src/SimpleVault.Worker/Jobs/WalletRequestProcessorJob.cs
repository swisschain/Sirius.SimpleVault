using System;
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
using SimpleVault.Common.Persistence.Wallets;

namespace SimpleVault.Worker.Jobs
{
    public class WalletRequestProcessorJob : IDisposable
    {
        private readonly ILogger<WalletRequestProcessorJob> _logger;
        private readonly TimeSpan _delayBetweenRequestsUpdate;
        private readonly ISiriusApiClient _siriusApiClient;
        private readonly IEncryptionService _encryptionService;
        private readonly IWalletRepository _walletRepository;
        private readonly ICursorRepository _cursorRepository;
        private readonly Timer _timer;
        private readonly ManualResetEventSlim _done;
        private readonly CancellationTokenSource _cts;
        private readonly AsyncRetryPolicy _retryPolicy;

        public WalletRequestProcessorJob(
            ILogger<WalletRequestProcessorJob> logger,
            ISiriusApiClient siriusApiClient,
            IEncryptionService encryptionService,
            IWalletRepository walletRepository,
            ICursorRepository cursorRepository)
        {
            _logger = logger;
            _delayBetweenRequestsUpdate = TimeSpan.FromSeconds(1);
            _siriusApiClient = siriusApiClient;
            _encryptionService = encryptionService;
            _walletRepository = walletRepository;
            _cursorRepository = cursorRepository;

            _timer = new Timer(TimerCallback, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _done = new ManualResetEventSlim(false);
            _cts = new CancellationTokenSource();

            _logger.LogInformation($"{nameof(WalletRequestProcessorJob)} is being created.");
            _retryPolicy = Policy
                .Handle<ApiException>()
                .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
            );
        }

        public void Start()
        {
            _timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);

            _logger.LogInformation($"{nameof(WalletRequestProcessorJob)} is being started.");
        }

        public void Stop()
        {
            _logger.LogInformation($"{nameof(WalletRequestProcessorJob)} is being stopped.");

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
            _logger.LogDebug("Wallet requests processing being started");

            try
            {
                ProcessAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing wallet requests");
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

            _logger.LogDebug("Wallet requests processing has been done");
        }

        private async Task ProcessAsync()
        {
            var existingCursor = await _cursorRepository.GetOrDefaultAsync(Cursor.WalletId);
            var cursor = existingCursor?.CursorValue ?? 0;

            do
            {
                var paginatedResponse = await _siriusApiClient.ApiWalletGenerationRequestUpdatesAsync(
                        id: null,
                        cursor: cursor,
                        order: PaginationOrder.Asc,
                        limit: 100,
                        state: new []
                        {
                            WalletGenerationRequestState.Pending
                        },
                        vaultType: null,
                        vaultId: null,
                        walletGenerationRequestId: null,
                        component: null,
                        blockchainId: null,
                        cancellationToken: _cts.Token);

                if (paginatedResponse.Pagination.Count == 0)
                    break;

                cursor = paginatedResponse.Items.Last().Id;

                foreach (var item in paginatedResponse.Items)
                {
                    try
                    {
                        var wallet = Wallet.Create(
                            _encryptionService,
                            item.WalletGenerationRequestId,
                            item.ProtocolCode,
                            item.NetworkType switch
                            {
                                NetworkType.Private => Swisschain.Sirius.Sdk.Primitives.NetworkType.Private,
                                NetworkType.Test => Swisschain.Sirius.Sdk.Primitives.NetworkType.Test,
                                NetworkType.Public => Swisschain.Sirius.Sdk.Primitives.NetworkType.Public,
                                _ => throw new ArgumentOutOfRangeException(nameof(item.NetworkType), item.NetworkType, null)
                            },
                            item.BlockchainId);

                        wallet = await _walletRepository.AddOrGetAsync(wallet);
                        await _retryPolicy.ExecuteAsync(async () =>
                        {
                            await _siriusApiClient.ApiWalletGenerationRequestUpdatesConfirmAsync(
                                $"Vault:Wallet:{wallet.WalletGenerationRequestId}",
                                wallet.WalletGenerationRequestId,
                                new WalletGenerationConfirmationRequest
                                {
                                    PublicKey = wallet.PublicKey,
                                    Address = wallet.Address,
                                    ScriptPubKey = wallet.ScriptPubKey
                                });
                        });
                    }
                    catch (BlockchainIsNotSupportedException)
                    {
                        await _retryPolicy.ExecuteAsync(async () =>
                        {
                            await _siriusApiClient.ApiWalletGenerationRequestUpdatesRejectAsync($"Vault:Wallet:{item.WalletGenerationRequestId}",
                                item.WalletGenerationRequestId,
                                new WalletGenerationRejectionRequest()
                                {
                                    ReasonMessage = "BlockchainId is not supported",
                                    Reason = RejectionReason.UnknownBlockchain
                                });
                        });
                    }
                }

                var newCursor = Cursor.CreateForWallet(cursor);
                await _cursorRepository.UpdateOrAddAsync(newCursor);
            } while (true);
        }
    }
}
