using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swisschain.Extensions.Postgres;
using SimpleVault.Common.Persistence.Cursors;
using SimpleVault.Common.Persistence.Transactions;
using SimpleVault.Common.Persistence.Wallets;
using SimpleVault.Common.Persistence.HostedServices;

namespace SimpleVault.Common.Persistence
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPostgresPersistence(this IServiceCollection services,
            string connectionString)
        {
            services.AddTransient<IWalletRepository, WalletRepository>();
            services.AddTransient<ICursorRepository, CursorRepository>();
            services.AddTransient<ITransactionRepository, TransactionRepository>();

            services.AddSingleton(x =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
                optionsBuilder.UseNpgsql(connectionString,
                    builder =>
                        builder.MigrationsHistoryTable(
                            DatabaseContext.MigrationHistoryTable,
                            DatabaseContext.SchemaName));

                return optionsBuilder;
            });

            services.AddHostedService<MigrationHost>();

            return services;
        }

        public static IServiceCollection AddPostgresMigration(this IServiceCollection services)
        {
            services.AddHostedService<MigrationHost>();

            return services;
        }

        public static IServiceCollection AddPostgresSchemaValidation(this IServiceCollection services)
        {
            services.AddHostedService<DbSchemaValidationHost>();

            return services;
        }

        public static IServiceCollection AddPostgresStaleConnectionsCleaning(this IServiceCollection services,
            string connectionString)
        {
            services.AddStaleConnectionsCleaning(connectionString, TimeSpan.FromMinutes(5));

            return services;
        }
    }
}
