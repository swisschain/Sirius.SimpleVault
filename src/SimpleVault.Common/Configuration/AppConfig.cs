namespace SimpleVault.Common.Configuration
{
    public class AppConfig
    {
        public DbConfig Db { get; set; }

        public SecretConfig Secret { get; set; }

        public VaultApiConfig VaultApi { get; set; }
    }
}
