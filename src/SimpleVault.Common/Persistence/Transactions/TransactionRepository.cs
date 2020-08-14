using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SimpleVault.Common.Domain;
using SimpleVault.Common.Exceptions;

namespace SimpleVault.Common.Persistence.Transactions
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly DbContextOptionsBuilder<DatabaseContext> _dbContextOptionsBuilder;

        public TransactionRepository(DbContextOptionsBuilder<DatabaseContext> dbContextOptionsBuilder)
        {
            _dbContextOptionsBuilder = dbContextOptionsBuilder;
        }

        public async Task<Transaction> GetBySigningRequestIdAsync(long transactionSigningRequestId)
        {
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

                var walletEntity = await context
                    .Transactions
                    .FirstOrDefaultAsync(x => x.TransactionSigningRequestId == transactionSigningRequestId);

                return walletEntity != null ? MapToDomain(walletEntity) : null;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException pgException &&
                                                      pgException.SqlState == PostgresErrorCodes.TooManyConnections)
            {
                throw new DbUnavailableException(exception);
            }
        }

        public async Task InsertAsync(Transaction transaction)
        {
            try
            {
                await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

                var entity = MapToEntity(transaction);

                context.Transactions.Add(entity);

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

        private static TransactionEntity MapToEntity(Transaction transaction)
        {
            return new TransactionEntity
            {
                CreatedAt = transaction.CreatedAt,
                NetworkType = transaction.NetworkType,
                ProtocolCode = transaction.ProtocolCode,
                SignedTransaction = transaction.SignedTransaction,
                BlockchainId = transaction.BlockchainId,
                TransactionId = transaction.TransactionId,
                SigningAddresses = transaction.SigningAddresses,
                TransactionSigningRequestId = transaction.TransactionSigningRequestId
            };
        }

        private static Transaction MapToDomain(TransactionEntity entity)
        {
            var transaction = Transaction.Restore(
                entity.TransactionSigningRequestId,
                entity.BlockchainId,
                entity.CreatedAt.UtcDateTime,
                entity.NetworkType,
                entity.ProtocolCode,
                entity.SigningAddresses,
                entity.SignedTransaction,
                entity.TransactionId);

            return transaction;
        }
    }
}
