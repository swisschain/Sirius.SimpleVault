using System;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;

namespace SimpleVault.Common.Cryptography
{
    public class HashService : IHashService
    {
        public string GetHash(string input, int times = 1)
        {
            var bytes = Encoding.UTF8.GetBytes(input);

            var hash = GetHash(bytes, times);

            return Convert.ToBase64String(hash);
        }

        public byte[] GetHash(byte[] input, int times = 1)
        {
            if (times <= 0)
                throw new ArgumentException("times should be more than 0", nameof(times));

            var digest = new Sha256Digest();
            var saltBytes = new byte[input.Length];
            byte[] result = null;
            Array.Copy(input, saltBytes, input.Length);

            for (int i = 0; i < times; i++)
            {
                digest.BlockUpdate(saltBytes, 0, saltBytes.Length);
                result = new byte[digest.GetDigestSize()];
                digest.DoFinal(result, 0);
            }

            return result;
        }
    }
}
