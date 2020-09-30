using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using SimpleVault.Common.Exceptions;
using Swisschain.Sirius.VaultApi.ApiClient;
using Swisschain.Sirius.VaultApi.ApiContract.TransferValidationRequests;

namespace SimpleVault.Worker.Jobs
{
    public class TransferValidationProcessorJob : IDisposable
    {
        private readonly IVaultApiClient _vaultApiClient;
        private readonly ILogger<TransferValidationProcessorJob> _logger;

        private readonly TimeSpan _delay;
        private readonly TimeSpan _delayOnError;
        private readonly Timer _timer;
        private readonly ManualResetEventSlim _done;
        private readonly CancellationTokenSource _cts;

        private readonly AsyncRetryPolicy _retryPolicy;

        public TransferValidationProcessorJob(
            IVaultApiClient vaultApiClient,
            ILogger<TransferValidationProcessorJob> logger)
        {
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
            _logger.LogInformation($"{nameof(TransferValidationProcessorJob)} stopped.");
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
                _logger.LogError(exception, "An error occurred while processing transfer validation requests.");
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
            var response = await _vaultApiClient.TransferValidationRequests.GetAsync(new GetTransferValidationRequestsRequest());

            if (response.BodyCase == GetTransferValidationRequestsResponse.BodyOneofCase.Error)
            {
                _logger.LogError("An error occurred while getting transfer validation requests. {@error}",
                    response.Error);
                await Task.Delay(_delayOnError);
                return;
            }

            foreach (var transferValidationRequest in response.Response.Requests)
            {
                var context = new LoggingContext
                {
                    TransferValidationRequestId = transferValidationRequest.Id,
                    BlockchainId = transferValidationRequest.Details.Blockchain.Id,
                };

                try
                {
                    _logger.LogInformation("Transfer validation request processing. {@context}", context);

                    if (await ConfirmAsync(transferValidationRequest, context))
                        _logger.LogInformation("Transfer validation  request confirmed. {@context}", context);
                }
                catch (DbException exception)
                {
                    _logger.LogError(exception,
                        "An error occurred while attempting to access the database. {@context}",
                        context);

                    // silently retry
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception,
                        "An error occurred while processing transaction signing request. {@context}",
                        context);
                }
            }
        }

        private async Task<bool> ConfirmAsync(TransferValidationRequest transferValidationRequest, LoggingContext context)
        {
            var response = await _vaultApiClient.TransferValidationRequests.ConfirmAsync(
               new ConfirmTransferValidationRequestRequest()
               {
                   HostProcessId = "",
                   PolicyResult = "Approved",
                   RequestId = $"Vault:TransferValidation:Confirm:{transferValidationRequest.Id}",
                   Signature = "",
                   TransferValidationRequestId = transferValidationRequest.Id
               });

            if (response.BodyCase == ConfirmTransferValidationRequestResponse.BodyOneofCase.Error)
            {
                _logger.LogError(
                    "An error occurred while confirming transaction signing request. {@context} {@error}",
                    context,
                    response.Error);

                return false;
            }

            return true;
        }

        #region Nested classes

        internal class LoggingContext
        {
            public long TransferValidationRequestId { get; set; }

            public string BlockchainId { get; set; }

        }

        #endregion
    }
}
