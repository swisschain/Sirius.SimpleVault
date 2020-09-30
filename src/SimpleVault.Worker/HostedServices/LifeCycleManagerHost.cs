using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using SimpleVault.Worker.Jobs;

namespace SimpleVault.Worker.HostedServices
{
    public class LifeCycleManagerHost : IHostedService
    {
        private readonly TransactionSigningProcessorJob _transactionSigningProcessorJob;
        private readonly WalletRequestProcessorJob _walletRequestProcessorJob;
        private readonly TransferValidationProcessorJob _transferValidationProcessorJob;

        public LifeCycleManagerHost(
            TransactionSigningProcessorJob transactionSigningProcessorJob,
            WalletRequestProcessorJob walletRequestProcessorJob,
            TransferValidationProcessorJob transferValidationProcessorJob)
        {
            _transactionSigningProcessorJob = transactionSigningProcessorJob;
            _walletRequestProcessorJob = walletRequestProcessorJob;
            _transferValidationProcessorJob = transferValidationProcessorJob;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _transactionSigningProcessorJob.Start();
            _walletRequestProcessorJob.Start();
            _transferValidationProcessorJob.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _transactionSigningProcessorJob.Stop();
            _walletRequestProcessorJob.Stop();
            _transferValidationProcessorJob.Stop();

            _transactionSigningProcessorJob.Wait();
            _walletRequestProcessorJob.Wait();
            _transferValidationProcessorJob.Wait();

            return Task.CompletedTask;
        }
    }
}
