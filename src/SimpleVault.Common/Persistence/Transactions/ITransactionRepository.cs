using System.Threading.Tasks;
using SimpleVault.Common.Domain;

namespace SimpleVault.Common.Persistence.Transactions
{
    public interface ITransactionRepository
    {
        Task<Transaction> GetBySigningRequestIdAsync(long transactionSigningRequestId);

        Task InsertAsync(Transaction transaction);
    }
}
