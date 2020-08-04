using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleVault.Common.Configuration;
using Swisschain.Sdk.Server.Common;
using SimpleVault.Common.Cryptography;
using SimpleVault.Common.HostedServices;
using SimpleVault.Common.Persistence;
using SimpleVault.Worker.HostedServices;
using SimpleVault.Worker.Jobs;
using Swisschain.Sirius.VaultApi.ApiClient;

namespace SimpleVault.Worker
{
    public sealed class Startup : SwisschainStartup<AppConfig>
    {
        public Startup(IConfiguration configuration)
            : base(configuration)
        {
        }

        protected override void ConfigureServicesExt(IServiceCollection services)
        {
            base.ConfigureServicesExt(services);

            services.AddHttpClient()
                .AddSingleton<IVaultApiClient>(x => new VaultApiClient(Config.VaultApi.Token, Config.VaultApi.Url))
                .AddServices(Config.Secret.Key)
                .AddPostgresPersistence(Config.Db.ConnectionString)
                .AddPostgresStaleConnectionsCleaning(Config.Db.ConnectionString)
                .AddHostedService<MigrationHost>()
                .AddHostedService<LifeCycleManagerHost>()
                .AddJobs();
        }
    }
}
