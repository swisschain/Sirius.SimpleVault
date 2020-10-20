using Microsoft.Extensions.DependencyInjection;

namespace SimpleVault.Worker.Jobs
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddJobs(this IServiceCollection services)
        {
            services.AddSingleton<TransferSigningProcessorJob>();
            services.AddSingleton<TransferValidationProcessorJob>();
            services.AddSingleton<WalletRequestProcessorJob>();

            return services;
        }
    }
}
