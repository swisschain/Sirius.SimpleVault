using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleVault.Common.Configuration;
using SimpleVault.Common.HostedServices;
using Swisschain.Sdk.Server.Common;
using SimpleVault.Common.Persistence;

namespace SimpleVault
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

            services.AddPostgresPersistence(Config.Db.ConnectionString)
                .AddHostedService<DbSchemaValidationHost>();
        }
    }
}
