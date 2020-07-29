using Microsoft.Extensions.DependencyInjection;

namespace SimpleVault.Common.Cryptography
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services, string secret)
        {
            services.AddTransient<IHashService, HashService>();
            services.AddTransient<IEncryptionService>(x => new EncryptionService(secret));

            return services;
        }
    }
}
