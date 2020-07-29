using Microsoft.Extensions.DependencyInjection;

namespace SimpleVault.Common.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDomainServices(this IServiceCollection services)
        {
            services.AddSingleton<IVaultStatusService, VaultStatusService>();

            return services;
        }
    }
}
