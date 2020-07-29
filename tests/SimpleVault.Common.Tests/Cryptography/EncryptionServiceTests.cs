using Shouldly;
using SimpleVault.Common.Cryptography;
using Xunit;

namespace SimpleVault.Common.Tests.Cryptography
{
    public class EncryptionServiceTests
    {
        public EncryptionServiceTests()
        {
            
        }

        [Fact]
        public void EncryptDecryptTest()
        {
            var encryptionService = new EncryptionService("p9ZPPoDWy16e7KNICDL6VhIy5mhBZ3IGbyFMO7RGYzw=");

            var privateKey = "0x68af9de4d9d44adcadbbec532fcd60f746121d277c682f12cccffee58b313485";
            var encryptedPrivateKey = encryptionService.Encrypt(privateKey);
            var decryptedPrivateKey = encryptionService.Decrypt(encryptedPrivateKey);

            //var nk = encryptionService.NewKey();
            privateKey.ShouldBe(decryptedPrivateKey);
        }
    }
}
