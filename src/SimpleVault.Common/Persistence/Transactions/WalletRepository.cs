using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SimpleVault.Common.Domain;

namespace SimpleVault.Common.Persistence.Transactions
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly DbContextOptionsBuilder<DatabaseContext> _dbContextOptionsBuilder;

        public TransactionRepository(DbContextOptionsBuilder<DatabaseContext> dbContextOptionsBuilder)
        {
            _dbContextOptionsBuilder = dbContextOptionsBuilder;
        }

        public async Task AddOrIgnoreAsync(Transaction transaction)
        {
            await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

            var entity = MapToEntity(transaction);

            context.Transactions.Add(entity);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
            }
        }

        public async Task<Transaction> AddOrGetAsync(Transaction transaction)
        {
            await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

            var entity = MapToEntity(transaction);

            context.Transactions.Add(entity);

            try
            {
                await context.SaveChangesAsync();

                return transaction;
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                var existing = await context.Transactions.FirstAsync(x => x.TransactionSigningRequestId == transaction.TransactionSigningRequestId);

                return MapToDomain(existing);
            }
        }


        public async Task<Transaction> GetAsync(long transactionSigningRequestId)
        {
            await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

            var transactionEntity = await context
                .Transactions
                .FirstAsync(x => x.TransactionSigningRequestId == transactionSigningRequestId);

            return MapToDomain(transactionEntity);
        }

        public async Task<Transaction> GetOrDefaultAsync(long transactionSigningRequestId)
        {
            await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

            var walletEntity = await context
                .Transactions
                .FirstOrDefaultAsync(x => x.TransactionSigningRequestId == transactionSigningRequestId);

            return walletEntity != null ? MapToDomain(walletEntity) : null;
        }

        private static TransactionEntity MapToEntity(Transaction transaction)
        {
            return new TransactionEntity()
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
