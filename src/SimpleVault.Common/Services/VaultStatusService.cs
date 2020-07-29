using SimpleVault.Common.Domain;

namespace SimpleVault.Common.Services
{
    public class VaultStatusService : IVaultStatusService
    {
        private VaultStatus _vaultStatus = VaultStatus.Initializing;

        public VaultStatus Get()
        {
            return _vaultStatus;
        }

        public void Set(VaultStatus status)
        {
            _vaultStatus = status;
        }
    }
}
