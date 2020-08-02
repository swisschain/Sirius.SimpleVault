using System;
using Swisschain.Sirius.Sdk.Primitives;

namespace SimpleVault.Common.Persistence.Wallets
{
    public class WalletEntity
    {
        public long WalletGenerationRequestId { get; set; }

        public string BlockchainId { get; set; }

        public string ProtocolCode { get; set; }

        public NetworkType NetworkType { get; set; }

        public string Address { get; set; }

        public string PublicKey { get; set; }

        public string ScriptPublicKey { get; set; }

        public string PrivateKey { get; set; }
        
        public DateTimeOffset CreatedAt { get; set; }
    }
}
