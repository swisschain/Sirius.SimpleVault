using System;
using Swisschain.Sirius.Sdk.Crypto.AddressGeneration;
using Swisschain.Sirius.Sdk.Primitives;
using SimpleVault.Common.Cryptography;
using SimpleVault.Common.Exceptions;

namespace SimpleVault.Common.Domain
{
    public class Wallet
    {
        private Wallet(long walletGenerationRequestId,
            string blockchainId,
            DateTime createdAt,
            string address,
            string publicKey,
            string privateKey,
            NetworkType networkType,
            string protocolCode,
            string tenantId,
            string group)
        {
            WalletGenerationRequestId = walletGenerationRequestId;
            BlockchainId = blockchainId;
            CreatedAt = createdAt;
            Address = address;
            PublicKey = publicKey;
            PrivateKey = privateKey;
            NetworkType = networkType;
            ProtocolCode = protocolCode;
            TenantId = tenantId;
            Group = group;
        }

        public long WalletGenerationRequestId { get; }

        public string BlockchainId { get; }

        public string ProtocolCode { get; }

        public NetworkType NetworkType { get; }

        public string Address { get; }

        public string PublicKey { get; }

        public string PrivateKey { get; }

        public string TenantId { get; }

        public string Group { get; }

        public DateTime CreatedAt { get; }

        public static Wallet Create(
            IEncryptionService encryptionService,
            long walletGenerationRequestId,
            string protocolCode,
            NetworkType networkType,
            string blockchainId,
            string tenantId,
            string group)
        {
            var addressGeneratorFactory = new AddressGeneratorFactory();
            
            IAddressGenerator addressGenerator;
            
            try
            {
                addressGenerator = addressGeneratorFactory.Create(protocolCode);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new BlockchainIsNotSupportedException(exception);
            }

            var generatedWallet = addressGenerator.Generate(networkType);
            var privateKey = encryptionService.Encrypt(generatedWallet.PrivateKey);

            var wallet = new Wallet(
                walletGenerationRequestId,
                blockchainId,
                DateTime.UtcNow,
                generatedWallet.Address,
                generatedWallet.PublicKey,
                privateKey,
                networkType,
                protocolCode,
                tenantId,
                group);

            return wallet;
        }

        public static Wallet Restore(
            long walletGenerationRequestId,
            string blockchainId,
            DateTime createdAt,
            string address,
            string publicKey,
            string privateKey,
            string protocolCode,
            NetworkType networkType,
            string tenantId,
            string group)
        {
            var wallet = new Wallet(
                walletGenerationRequestId,
                blockchainId,
                createdAt,
                address,
                publicKey,
                privateKey,
                networkType,
                protocolCode,
                tenantId,
                group);

            return wallet;
        }
    }
}
