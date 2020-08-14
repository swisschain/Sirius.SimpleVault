using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SimpleVault.Common.Domain;
using SimpleVault.Common.Exceptions;

namespace SimpleVault.Common.Persistence.Wallets
{
    public class WalletRepository : IWalletRepository
    {
        private readonly DbContextOptionsBuilder<DatabaseContext> _dbContextOptionsBuilder;

        public WalletRepository(DbContextOptionsBuilder<DatabaseContext> dbContextOptionsBuilder)
        {
            _dbContextOptionsBuilder = dbContextOptionsBuilder;
        }

        public async Task<Wallet> GetByWalletGenerationRequestAsync(long walletGenerationRequestId)
        {
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

                var walletEntity = await context
                    .Wallets
                    .FirstOrDefaultAsync(x => x.WalletGenerationRequestId == walletGenerationRequestId);

                return walletEntity != null ? MapToDomain(walletEntity) : null;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException pgException &&
                                                      pgException.SqlState == PostgresErrorCodes.TooManyConnections)
            {
                throw new DbUnavailableException(exception);
            }
        }

        public async Task<IReadOnlyCollection<Wallet>> GetByAddressesAsync(IReadOnlyCollection<string> signingAddresses)
        {
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

                var query = context.Wallets.Where(x => signingAddresses.Contains(x.Address));

                await query.LoadAsync();

                return query
                    .AsEnumerable()
                    .Select(MapToDomain)
                    .ToArray();
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException pgException &&
                                                      pgException.SqlState == PostgresErrorCodes.TooManyConnections)
            {
                throw new DbUnavailableException(exception);
            }
        }

        public async Task InsertAsync(Wallet wallet)
        {
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

                var entity = MapToEntity(wallet);

                context.Wallets.Add(entity);

                await context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException pgException &&
                                                      pgException.SqlState == PostgresErrorCodes.TooManyConnections)
            {
                throw new DbUnavailableException(exception);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException pgException &&
                                                      pgException.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new EntityAlreadyExistsException(exception);
            }
        }

        private static WalletEntity MapToEntity(Wallet wallet)
        {
            return new WalletEntity
            {
                CreatedAt = wallet.CreatedAt,
                BlockchainId = wallet.BlockchainId,
                Address = wallet.Address,
                WalletGenerationRequestId = wallet.WalletGenerationRequestId,
                PublicKey = wallet.PublicKey,
                PrivateKey = wallet.PrivateKey,
                NetworkType = wallet.NetworkType,
                ProtocolCode = wallet.ProtocolCode
            };
        }

        private static Wallet MapToDomain(WalletEntity entity)
        {
            var wallet = Wallet.Restore(
                entity.WalletGenerationRequestId,
                entity.BlockchainId,
                entity.CreatedAt.UtcDateTime,
                entity.Address,
                entity.PublicKey,
                entity.PrivateKey,
                entity.ProtocolCode,
                entity.NetworkType);

            return wallet;
        }
    }
}
