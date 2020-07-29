using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Swisschain.Sirius.Sdk.Crypto.TransactionSigning;
using SimpleVault.Common.Cryptography;
using SimpleVault.Common.Exceptions;
using SimpleVault.Common.Persistence.Wallets;

namespace SimpleVault.Common.Domain
{
    public class Transaction
    {
        public Transaction(
            long transactionSigningRequestId,
            string blockchainId,
            DateTime createdAt,
            Swisschain.Sirius.Sdk.Primitives.NetworkType networkType,
            string protocolCode,
            IReadOnlyCollection<string> signingAddresses,
            byte[] signedTransaction,
            string transactionId)
        {
            TransactionSigningRequestId = transactionSigningRequestId;
            BlockchainId = blockchainId;
            CreatedAt = createdAt;
            NetworkType = networkType;
            ProtocolCode = protocolCode;
            SigningAddresses = signingAddresses;
            SignedTransaction = signedTransaction;
            TransactionId = transactionId;
        }

        public long TransactionSigningRequestId { get; }

        public string BlockchainId { get; }

        public DateTime CreatedAt { get; }

        public byte[] SignedTransaction { get; }

        public IReadOnlyCollection<string> SigningAddresses { get; }

        public string ProtocolCode { get; }

        public Swisschain.Sirius.Sdk.Primitives.NetworkType NetworkType { get; }

        public string TransactionId { get; }

        public static async Task<Transaction> Create(
            IWalletRepository walletRepository,
            IEncryptionService encryptionService,
            long transactionSigningRequestId,
            string blockchainId,
            DateTime createdAt,
            Swisschain.Sirius.Sdk.Primitives.NetworkType networkType,
            string protocolCode,
            IReadOnlyCollection<string> signingAddresses,
            byte[] builtTransaction,
            Swisschain.Sirius.Sdk.Primitives.DoubleSpendingProtectionType doubleSpendingProtectionType,
            IReadOnlyCollection<Swisschain.Sirius.Sdk.Primitives.Coin> coinsToSpend)
        {
            var transactionSignerFactory = new TransactionSignerFactory();
            TransactionSigningResult signedResult;

            if (doubleSpendingProtectionType == Swisschain.Sirius.Sdk.Primitives.DoubleSpendingProtectionType.Nonce)
            {
                signedResult = await CreateAsync(transactionSignerFactory,
                    protocolCode,
                    walletRepository,
                    signingAddresses,
                    encryptionService,
                    networkType,
                    builtTransaction);
            }
            else if (doubleSpendingProtectionType ==
                     Swisschain.Sirius.Sdk.Primitives.DoubleSpendingProtectionType.Coins)
            {
                signedResult = await CreateCoinsAsync(transactionSignerFactory,
                    protocolCode,
                    walletRepository,
                    signingAddresses,
                    encryptionService,
                    networkType,
                    builtTransaction,
                    coinsToSpend);
            }
            else
            {
                throw new InvalidEnumArgumentException("Unknown double spending protection type",
                    (int) doubleSpendingProtectionType,
                    typeof(Swisschain.Sirius.Sdk.Primitives.DoubleSpendingProtectionType));
            }

            var transaction = new Transaction(
                transactionSigningRequestId,
                blockchainId,
                createdAt,
                networkType,
                protocolCode,
                signingAddresses,
                signedResult.SignedTransaction,
                signedResult.TransactionId);

            return transaction;
        }

        public static Transaction Restore(
            long transactionSigningRequestId,
            string blockchainId,
            DateTime createdAt,
            Swisschain.Sirius.Sdk.Primitives.NetworkType networkType,
            string protocolCode,
            IReadOnlyCollection<string> signingAddresses,
            byte[] signedTransaction,
            string transactionId)
        {
            var transaction = new Transaction(
                transactionSigningRequestId,
                blockchainId,
                createdAt,
                networkType,
                protocolCode,
                signingAddresses,
                signedTransaction,
                transactionId);

            return transaction;
        }

        private static async Task<TransactionSigningResult> CreateAsync(
            ITransactionSignerFactory transactionSignerFactory,
            string protocolCode,
            IWalletRepository walletRepository,
            IReadOnlyCollection<string> signingAddresses,
            IEncryptionService encryptionService,
            Swisschain.Sirius.Sdk.Primitives.NetworkType networkType,
            byte[] builtTransaction)
        {
            ITransactionSigner transactionSigner;
            try
            {
                transactionSigner = transactionSignerFactory.Create(protocolCode);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new BlockchainIsNotSupportedException(e);
            }

            var wallets = await walletRepository.GetByAddressesAsync(signingAddresses);

            var privateKeys = wallets.Select(x => encryptionService.Decrypt(x.PrivateKey)).ToArray();

            try
            {
                return transactionSigner.Sign(builtTransaction, privateKeys, networkType);
            }
            catch (Exception exception)
            {
                throw new TransactionSigninFailedException("An error occured while signing transaction", exception);
            }
        }

        private static async Task<TransactionSigningResult> CreateCoinsAsync(
            ITransactionSignerFactory transactionSignerFactory,
            string protocolCode,
            IWalletRepository walletRepository,
            IReadOnlyCollection<string> signingAddresses,
            IEncryptionService encryptionService,
            Swisschain.Sirius.Sdk.Primitives.NetworkType networkType,
            byte[] builtTransaction,
            IReadOnlyCollection<Swisschain.Sirius.Sdk.Primitives.Coin> coinsToSpend)
        {
            ICoinsTransactionSigner transactionSigner;
            try
            {
                transactionSigner = transactionSignerFactory.CreateCoinsSigner(protocolCode);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new BlockchainIsNotSupportedException(e);
            }

            var wallets = await walletRepository.GetByAddressesAsync(signingAddresses);

            var privateKeys = wallets.Select(x => encryptionService.Decrypt(x.PrivateKey)).ToArray();

            try
            {
                return transactionSigner.Sign(builtTransaction,
                    coinsToSpend,
                    privateKeys,
                    networkType);
            }
            catch (Exception exception)
            {
                throw new TransactionSigninFailedException("An error occured while signing transaction", exception);
            }
        }
    }
}
