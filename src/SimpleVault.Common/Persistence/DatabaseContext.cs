using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SimpleVault.Common.Persistence.Transactions;
using SimpleVault.Common.Persistence.Wallets;

namespace SimpleVault.Common.Persistence
{
    public class DatabaseContext : DbContext
    {
        public static string SchemaName { get; } = "simple_vault";
        public static string MigrationHistoryTable { get; } = "__EFMigrationsHistory";

        public DatabaseContext(DbContextOptions<DatabaseContext> options)
            : base(options)
        {
        }

        public DbSet<WalletEntity> Wallets { get; set; }

        public DbSet<TransactionEntity> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(SchemaName);

            BuildWallets(modelBuilder);
            BuildTransactions(modelBuilder);
        }

        private static void BuildWallets(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WalletEntity>()
                .ToTable("wallets")
                .HasKey(entity => entity.WalletGenerationRequestId);

            modelBuilder.Entity<WalletEntity>()
                .HasIndex(entity => entity.Address);

            modelBuilder.Entity<WalletEntity>()
                .Property(entity => entity.TenantId)
                .IsRequired();

            modelBuilder.Entity<WalletEntity>()
                .Property(entity => entity.Group)
                .IsRequired();
        }

        private static void BuildTransactions(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TransactionEntity>()
                .ToTable("transactions")
                .HasKey(entity => entity.TransactionSigningRequestId);

            var jsonSerializingSettings = new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore};

            modelBuilder.Entity<TransactionEntity>()
                .Property(entity => entity.SigningAddresses)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v, jsonSerializingSettings),
                    v => JsonConvert.DeserializeObject<IReadOnlyCollection<string>>(v, jsonSerializingSettings));
        }
    }
}
