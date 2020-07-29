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

        public LifeCycleManagerHost(
            TransactionSigningProcessorJob transactionSigningProcessorJob,
            WalletRequestProcessorJob walletRequestProcessorJob)
        {
            _transactionSigningProcessorJob = transactionSigningProcessorJob;
            _walletRequestProcessorJob = walletRequestProcessorJob;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _transactionSigningProcessorJob.Start();
            _walletRequestProcessorJob.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _transactionSigningProcessorJob.Stop();
            _walletRequestProcessorJob.Stop();

            _transactionSigningProcessorJob.Wait();
            _walletRequestProcessorJob.Wait();

            return Task.CompletedTask;
        }
    }
}
