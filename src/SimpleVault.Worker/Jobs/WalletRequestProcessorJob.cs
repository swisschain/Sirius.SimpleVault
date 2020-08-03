using System;
using System.ComponentModel;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SimpleVault.Common.Cryptography;
using SimpleVault.Common.Domain;
using SimpleVault.Common.Exceptions;
using SimpleVault.Common.Persistence.Wallets;
using Swisschain.Sirius.VaultApi.ApiClient;
using Swisschain.Sirius.VaultApi.ApiContract.Common;
using Swisschain.Sirius.VaultApi.ApiContract.Wallets;

namespace SimpleVault.Worker.Jobs
{
    public class WalletRequestProcessorJob : IDisposable
    {
        private readonly IWalletRepository _walletRepository;
        private readonly IEncryptionService _encryptionService;
        private readonly IVaultApiClient _vaultApiClient;
        private readonly ILogger<WalletRequestProcessorJob> _logger;

        private readonly TimeSpan _delayBetweenRequestsUpdate;
        private readonly Timer _timer;
        private readonly ManualResetEventSlim _done;
        private readonly CancellationTokenSource _cts;

        public WalletRequestProcessorJob(IWalletRepository walletRepository,
            IEncryptionService encryptionService,
            IVaultApiClient vaultApiClient,
            ILogger<WalletRequestProcessorJob> logger)
        {
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
            _logger.LogInformation($"{nameof(WalletRequestProcessorJob)} started.");
        }

        public void Stop()
        {
            _logger.LogInformation($"{nameof(WalletRequestProcessorJob)} stopped.");
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
                _logger.LogError(exception, "An error occurred while processing wallet generation requests.");
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
            var response = await _vaultApiClient.Wallets.GetAsync(new GetWalletGenerationRequestRequest());

            if (response.BodyCase == GetWalletGenerationRequestResponse.BodyOneofCase.Error)
            {
                _logger.LogError("An error occurred while getting wallet generation requests. {@context}",
                    new {response.Error.ErrorMessage});
                return;
            }

            foreach (var walletGenerationRequest in response.Response.Requests)
            {
                var context = new LoggingContext
                {
                    WalletGenerationRequestId = walletGenerationRequest.Id,
                    BlockchainId = walletGenerationRequest.BlockchainId,
                    ProtocolCode = walletGenerationRequest.ProtocolCode,
                    NetworkType = walletGenerationRequest.NetworkType
                };
                
                try
                {
                    _logger.LogInformation("Wallet generation request processing. {@context}", context);

                    var wallet = await GenerateWalletAsync(walletGenerationRequest);

                    await _vaultApiClient.Wallets.ConfirmAsync(new ConfirmWalletGenerationRequestRequest
                    {
                        RequestId = $"Vault:Wallet:{walletGenerationRequest.Id}",
                        WalletGenerationRequestId = wallet.WalletGenerationRequestId,
                        PublicKey = wallet.PublicKey,
                        Address = wallet.Address,
                    });

                    _logger.LogInformation("Wallet generation request confirmed. {@context}", context);
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

                    await _vaultApiClient.Wallets.RejectAsync(new RejectWalletGenerationRequestRequest
                    {
                        RequestId = $"Vault:Wallet:{walletGenerationRequest.Id}",
                        WalletGenerationRequestId = walletGenerationRequest.Id,
                        ReasonMessage = "BlockchainId is not supported",
                        Reason = RejectionReason.UnknownBlockchain
                    });

                    _logger.LogInformation("Wallet generation request rejected. {@context}", context);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception,
                        "An error occurred while processing wallet generation request. {@context}",
                        context);

                    await _vaultApiClient.Wallets.RejectAsync(new RejectWalletGenerationRequestRequest
                    {
                        RequestId = $"Vault:Wallet:{walletGenerationRequest.Id}",
                        WalletGenerationRequestId = walletGenerationRequest.Id,
                        ReasonMessage = exception.Message,
                        Reason = RejectionReason.Other
                    });

                    _logger.LogInformation("Wallet generation request rejected. {@context}", context);
                }
            }
        }

        private async Task<Wallet> GenerateWalletAsync(WalletGenerationRequest request)
        {
            var wallet = Wallet.Create(
                _encryptionService,
                request.Id,
                request.ProtocolCode,
                request.NetworkType switch
                {
                    NetworkType.Private => Swisschain.Sirius.Sdk.Primitives.NetworkType.Private,
                    NetworkType.Test => Swisschain.Sirius.Sdk.Primitives.NetworkType.Test,
                    NetworkType.Public => Swisschain.Sirius.Sdk.Primitives.NetworkType.Public,
                    _ => throw new InvalidEnumArgumentException(nameof(request.NetworkType),
                        (int) request.NetworkType,
                        typeof(NetworkType))
                },
                request.BlockchainId);

            return await _walletRepository.AddOrGetAsync(wallet);
        }
        
        #region Nested classes

        public class LoggingContext
        {
            public long WalletGenerationRequestId { get; set; }

            public string BlockchainId { get; set; }

            public NetworkType NetworkType { get; set; }

            public string ProtocolCode { get; set; }
        }

        #endregion
    }
}
