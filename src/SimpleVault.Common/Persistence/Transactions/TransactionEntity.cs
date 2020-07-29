using System;
using System.Collections.Generic;
using Swisschain.Sirius.Sdk.Primitives;

namespace SimpleVault.Common.Persistence.Transactions
{
    public class TransactionEntity
    {
        public long TransactionSigningRequestId { get; set; }

        public string BlockchainId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public byte[] SignedTransaction { get; set; }

        public IReadOnlyCollection<string> SigningAddresses { get; set; }

        public string ProtocolCode { get; set; }

        public NetworkType NetworkType { get; set; }

        public string TransactionId { get; set; }
    }
}
