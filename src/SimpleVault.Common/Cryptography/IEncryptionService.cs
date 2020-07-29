namespace SimpleVault.Common.Cryptography
{
    public interface IEncryptionService
    {
        string Encrypt(string data);
        string Decrypt(string data);
    }
}
