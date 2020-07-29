﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SimpleVault.Common.Domain;

namespace SimpleVault.Common.Persistence.Wallets
{
    public class WalletRepository : IWalletRepository
    {
        private readonly DbContextOptionsBuilder<DatabaseContext> _dbContextOptionsBuilder;

        public WalletRepository(DbContextOptionsBuilder<DatabaseContext> dbContextOptionsBuilder)
        {
            _dbContextOptionsBuilder = dbContextOptionsBuilder;
        }

        public async Task AddOrIgnoreAsync(Wallet wallet)
        {
            await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

            var entity = MapToEntity(wallet);

            context.Wallets.Add(entity);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
            }
        }

        public async Task<Wallet> AddOrGetAsync(Wallet wallet)
        {
            await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

            var entity = MapToEntity(wallet);

            context.Wallets.Add(entity);

            try
            {
                await context.SaveChangesAsync();

                return wallet;
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                var existing = await context.Wallets.FirstAsync(x =>
                    x.WalletGenerationRequestId == wallet.WalletGenerationRequestId);

                return MapToDomain(existing);
            }
        }

        public async Task<IReadOnlyCollection<Wallet>> GetByAddressesAsync(IReadOnlyCollection<string> signingAddresses)
        {
            await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

            var query = context.Wallets.Where(x => signingAddresses.Contains(x.Address));

            await query.LoadAsync();

            return query
                .AsEnumerable()
                .Select(MapToDomain)
                .ToArray();
        }

        public async Task<Wallet> GetAsync(long walletGenerationRequestId)
        {
            await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

            var walletEntity = await context
                .Wallets
                .FirstAsync(x => x.WalletGenerationRequestId == walletGenerationRequestId);

            return MapToDomain(walletEntity);
        }

        public async Task<Wallet> GetOrDefaultAsync(long walletGenerationRequestId)
        {
            await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

            var walletEntity = await context
                .Wallets
                .FirstOrDefaultAsync(x => x.WalletGenerationRequestId == walletGenerationRequestId);

            return walletEntity != null ? MapToDomain(walletEntity) : null;
        }

        private static WalletEntity MapToEntity(Wallet wallet)
        {
            return new WalletEntity()
            {
                CreatedAt = wallet.CreatedAt,
                BlockchainId = wallet.BlockchainId,
                Address = wallet.Address,
                WalletGenerationRequestId = wallet.WalletGenerationRequestId,
                PublicKey = wallet.PublicKey,
                PrivateKey = wallet.PrivateKey,
                NetworkType = wallet.NetworkType,
                ProtocolCode = wallet.ProtocolCode,
                ScriptPubKey = wallet.ScriptPubKey
            };
        }

        private static Wallet MapToDomain(WalletEntity entity)
        {
            var wallet = Wallet.Restore(
                entity.WalletGenerationRequestId,
                entity.BlockchainId,
                entity.CreatedAt,
                entity.Address,
                entity.PublicKey,
                entity.ScriptPubKey,
                entity.PrivateKey,
                entity.ProtocolCode,
                entity.NetworkType);

            return wallet;
        }
    }
}
