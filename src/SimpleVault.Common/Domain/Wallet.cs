using System;
using Swisschain.Sirius.Sdk.Crypto.AddressGeneration;
using Swisschain.Sirius.Sdk.Primitives;
using SimpleVault.Common.Cryptography;
using SimpleVault.Common.Exceptions;

namespace SimpleVault.Common.Domain
{
    public class Wallet
    {
        private Wallet(
            long walletGenerationRequestId,
            string blockchainId,
            DateTime createdAt,
            string address,
            string publicKey,
            string scriptPubKey,
            string privateKey,
            NetworkType networkType,
            string protocolCode)
        {
            WalletGenerationRequestId = walletGenerationRequestId;
            BlockchainId = blockchainId;
            CreatedAt = createdAt;
            Address = address;
            PublicKey = publicKey;
            ScriptPubKey = scriptPubKey;
            PrivateKey = privateKey;
            NetworkType = networkType;
            ProtocolCode = protocolCode;
        }

        public long WalletGenerationRequestId { get; }

        public string BlockchainId { get; }

        public DateTime CreatedAt { get; }

        public string Address { get; }

        public string PublicKey { get; }

        public string ScriptPubKey { get; }

        public string PrivateKey { get; }

        public string ProtocolCode { get; }

        public NetworkType NetworkType { get; }

        public static Wallet Create(
            IEncryptionService encryptionService,
            long walletGenerationRequestId,
            string protocolCode,
            NetworkType networkType,
            string blockchainId)
        {
            var addressGeneratorFactory = new AddressGeneratorFactory();
            IAddressGenerator addressGenerator;
            try
            {
                addressGenerator = addressGeneratorFactory.Create(protocolCode);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new BlockchainIsNotSupportedException(e);
            }

            var generatedWallet = addressGenerator.Generate(networkType);
            var privateKey = encryptionService.Encrypt(generatedWallet.PrivateKey);

            var wallet = new Wallet(
                walletGenerationRequestId,
                blockchainId,
                DateTime.UtcNow,
                generatedWallet.Address,
                generatedWallet.PublicKey,
                generatedWallet.ScriptPubKey,
                privateKey,
                networkType,
                protocolCode);

            return wallet;
        }

        public static Wallet Restore(
            long walletGenerationRequestId,
            string blockchainId,
            DateTime createdAt,
            string address,
            string publicKey,
            string scriptPubKey,
            string privateKey,
            string protocolCode,
            NetworkType networkType)
        {
            var wallet = new Wallet(
                walletGenerationRequestId,
                blockchainId,
                createdAt,
                address,
                publicKey,
                scriptPubKey,
                privateKey,
                networkType,
                protocolCode);

            return wallet;
        }
    }
}
