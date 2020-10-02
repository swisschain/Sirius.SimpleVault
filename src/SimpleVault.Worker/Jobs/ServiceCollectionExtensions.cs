using Microsoft.Extensions.DependencyInjection;

namespace SimpleVault.Worker.Jobs
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddJobs(this IServiceCollection services)
        {
            services.AddSingleton<TransferValidationProcessorJob>();
            services.AddSingleton<TransactionSigningProcessorJob>();
            services.AddSingleton<WalletRequestProcessorJob>();

            return services;
        }
    }
}
