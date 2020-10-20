using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using SimpleVault.Worker.Jobs;

namespace SimpleVault.Worker.HostedServices
{
    public class LifeCycleManagerHost : IHostedService
    {
        private readonly TransferSigningProcessorJob _transferSigningProcessorJob;
        private readonly WalletRequestProcessorJob _walletRequestProcessorJob;
        private readonly TransferValidationProcessorJob _transferValidationProcessorJob;

        public LifeCycleManagerHost(
            TransferSigningProcessorJob transferSigningProcessorJob,
            WalletRequestProcessorJob walletRequestProcessorJob,
            TransferValidationProcessorJob transferValidationProcessorJob)
        {
            _transferSigningProcessorJob = transferSigningProcessorJob;
            _walletRequestProcessorJob = walletRequestProcessorJob;
            _transferValidationProcessorJob = transferValidationProcessorJob;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _transferSigningProcessorJob.Start();
            _walletRequestProcessorJob.Start();
            _transferValidationProcessorJob.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _transferSigningProcessorJob.Stop();
            _walletRequestProcessorJob.Stop();
            _transferValidationProcessorJob.Stop();

            _transferSigningProcessorJob.Wait();
            _walletRequestProcessorJob.Wait();
            _transferValidationProcessorJob.Wait();

            return Task.CompletedTask;
        }
    }
}
