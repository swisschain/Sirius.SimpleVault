using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleVault.Common.Configuration;
using Swisschain.Sdk.Server.Common;
using Swisschain.Sirius.SimpleVault.Api.Client;
using SimpleVault.Common.Cryptography;
using SimpleVault.Common.Persistence;
using SimpleVault.Common.Services;
using SimpleVault.Worker.HostedServices;
using SimpleVault.Worker.Jobs;

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

            services.AddHttpClient();
            services.AddTransient<ISiriusApiClient>(x =>
            {
                var clientFactory = x.GetRequiredService<IHttpClientFactory>();
                var client = clientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", Config.SiriusApi.Token);

                return new SiriusApiClient(Config.SiriusApi.Url, client);
            });
            services.AddServices(Config.Secret.Key);
            services.AddDomainServices();
            services.AddPostgresPersistence(Config.Db.ConnectionString);
            services.AddPostgresStaleConnectionsCleaning(Config.Db.ConnectionString);
            services.AddPostgresMigration();

            services.AddSingleton<LifeCycleManagerHost>();
            services.AddHostedService<LifeCycleManagerHost>();
            services.AddJobs();
        }
    }
}
