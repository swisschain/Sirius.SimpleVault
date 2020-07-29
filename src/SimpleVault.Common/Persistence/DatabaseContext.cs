using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SimpleVault.Common.Persistence.Cursors;
using SimpleVault.Common.Persistence.Transactions;
using SimpleVault.Common.Persistence.Wallets;

namespace SimpleVault.Common.Persistence
{
    public class DatabaseContext : DbContext
    {
        public static string SchemaName { get; } = "simple_vault";
        public static string MigrationHistoryTable { get; } = "__EFMigrationsHistory";

        public DatabaseContext(DbContextOptions<DatabaseContext> options) :
            base(options)
        {
        }

        public DbSet<WalletEntity> Wallets { get; set; }

        public DbSet<TransactionEntity> Transactions { get; set; }

        public DbSet<CursorEntity> Cursor { get; set; }

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
                .HasKey(x => x.WalletGenerationRequestId);

            modelBuilder.Entity<WalletEntity>()
                .HasIndex(x => x.Address)
                .HasName("IX_Wallet_Address");

            modelBuilder.Entity<CursorEntity>()
                .ToTable("cursor")
                .HasKey(x => x.Id);
        }

        private static void BuildTransactions(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TransactionEntity>()
                .ToTable("transactions")
                .HasKey(x => x.TransactionSigningRequestId);

            var jsonSerializingSettings = new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore};

            #region Conversions

            modelBuilder.Entity<TransactionEntity>()
                .Property(e => e.SigningAddresses)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v,
                        jsonSerializingSettings),
                    v =>
                        JsonConvert.DeserializeObject<IReadOnlyCollection<string>>(v,
                            jsonSerializingSettings));

            #endregion
        }
    }
}
