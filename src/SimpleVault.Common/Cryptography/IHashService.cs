namespace SimpleVault.Common.Cryptography
{
    public interface IHashService
    {
        string GetHash(string input, int times = 1);

        byte[] GetHash(byte[] input, int times = 1);
    }
}
