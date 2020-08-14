using System.Collections.Generic;
using System.Threading.Tasks;
using SimpleVault.Common.Domain;

namespace SimpleVault.Common.Persistence.Wallets
{
    public interface IWalletRepository
    {
        Task<Wallet> GetByWalletGenerationRequestAsync(long walletGenerationRequestId);

        Task<IReadOnlyCollection<Wallet>> GetByAddressesAsync(IReadOnlyCollection<string> signingAddresses);

        Task InsertAsync(Wallet wallet);
    }
}
