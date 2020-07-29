using SimpleVault.Common.Domain;

namespace SimpleVault.Common.Services
{
    public interface IVaultStatusService
    {
        VaultStatus Get();

        void Set(VaultStatus status);
    }
}
