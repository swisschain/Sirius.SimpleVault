using System.Threading.Tasks;
using SimpleVault.Common.Domain;

namespace SimpleVault.Common.Persistence.Transactions
{
    public interface ITransactionRepository
    {
        Task<Transaction> GetAsync(long transactionSigningRequestId);

        Task<Transaction> GetOrDefaultAsync(long transactionSigningRequestId);

        Task AddOrIgnoreAsync(Transaction transaction);

        Task<Transaction> AddOrGetAsync(Transaction transaction);
    }
}
