using System.Collections.Generic;
using System.Threading.Tasks;
using SimpleVault.Common.Domain;

namespace SimpleVault.Common.Persistence.Wallets
{
    public interface IWalletRepository
    {
        Task<Wallet> GetAsync(long walletGenerationRequestId);

        Task<Wallet> GetOrDefaultAsync(long id);

        Task AddOrIgnoreAsync(Wallet wallet);

        Task<Wallet> AddOrGetAsync(Wallet wallet);

        Task<IReadOnlyCollection<Wallet>> GetByAddressesAsync(IReadOnlyCollection<string> signingAddresses);
    }
}
