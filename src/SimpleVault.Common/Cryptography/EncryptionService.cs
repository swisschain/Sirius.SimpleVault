using System;
using System.IO;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace SimpleVault.Common.Cryptography
{
    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _secret;

        private const int KEY_BIT_SIZE = 256;
        private const int MAC_BIT_SIZE = 128;
        private const int NONCE_BIT_SIZE = 128;

        private readonly SecureRandom _random;
        
        public EncryptionService(string secret)
        {
            _secret = Convert.FromBase64String(secret);
            _random = new SecureRandom();
        }

        public string Encrypt(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("data required!", nameof(data));

            var dataBytes = Encoding.UTF8.GetBytes(data);
            var encryptedData = EncryptWithKey(dataBytes, _secret);
            
            return Convert.ToBase64String(encryptedData);
        }

        public string Decrypt(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("data is required!", nameof(data));

            var cipherData = Convert.FromBase64String(data);
            var plainText = DecryptWithKey(cipherData, _secret);

            return Encoding.UTF8.GetString(plainText);
        }

        //decrypt with byte array
        private byte[] DecryptWithKey(byte[] message, byte[] key, int nonSecretPayloadLength = 0)
        {
            if (key == null || key.Length != KEY_BIT_SIZE / 8)
                throw new ArgumentException(String.Format("Key needs to be {0} bit!", KEY_BIT_SIZE), "key");
            if (message == null || message.Length == 0)
                throw new ArgumentException("Message required!", "message");

            using (var cipherStream = new MemoryStream(message))
            using (var cipherReader = new BinaryReader(cipherStream))
            {
                var nonSecretPayload = cipherReader.ReadBytes(nonSecretPayloadLength);
                var nonce = cipherReader.ReadBytes(NONCE_BIT_SIZE / 8);
                var cipher = new GcmBlockCipher(new AesEngine());
                var parameters = new AeadParameters(new KeyParameter(key), MAC_BIT_SIZE, nonce, nonSecretPayload);
                cipher.Init(false, parameters);
                var cipherData = cipherReader.ReadBytes(message.Length - nonSecretPayloadLength - nonce.Length);
                var plainText = new byte[cipher.GetOutputSize(cipherData.Length)];
                try
                {
                    var len = cipher.ProcessBytes(cipherData, 0, cipherData.Length, plainText, 0);
                    cipher.DoFinal(plainText, len);
                }
                catch (InvalidCipherTextException)
                {
                    return null;
                }
                return plainText;
            }
        }

        //encrypt with byte array
        private byte[] EncryptWithKey(byte[] text, byte[] key, byte[] nonSecretPayload = null)
        {
            if (key == null || key.Length != KEY_BIT_SIZE / 8)
                throw new ArgumentException(String.Format("Key needs to be {0} bit!", KEY_BIT_SIZE), "key");

            nonSecretPayload = nonSecretPayload ?? new byte[] { };
            var nonce = new byte[NONCE_BIT_SIZE / 8];
            _random.NextBytes(nonce, 0, nonce.Length);
            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(key), MAC_BIT_SIZE, nonce, nonSecretPayload);
            cipher.Init(true, parameters);
            var cipherData = new byte[cipher.GetOutputSize(text.Length)];
            var len = cipher.ProcessBytes(text, 0, text.Length, cipherData, 0);
            cipher.DoFinal(cipherData, len);
            using (var combinedStream = new MemoryStream())
            {
                using (var binaryWriter = new BinaryWriter(combinedStream))
                {
                    binaryWriter.Write(nonSecretPayload);
                    binaryWriter.Write(nonce);
                    binaryWriter.Write(cipherData);
                }
                return combinedStream.ToArray();
            }
        }

        //create new key
        public string NewKey()
        {
            var key = new byte[KEY_BIT_SIZE / 8];
            _random.NextBytes(key);
            return Convert.ToBase64String(key);
        }
    }
}
