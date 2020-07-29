using System.Text;

namespace SimpleVault.Common.Cryptography
{
    public static class VaultEncoding
    {
        public static readonly UTF8Encoding SecureUtf8Encoding =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    }
}
