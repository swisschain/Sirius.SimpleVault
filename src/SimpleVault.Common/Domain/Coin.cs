using System;
using System.Collections.Generic;
using System.Text;
using Swisschain.Sirius.Sdk.Primitives;

namespace SimpleVault.Common.Domain
{
    public sealed class Coin : IEquatable<Coin>
    {
        public Coin(CoinId id,
            BlockchainAsset asset,
            decimal value,
            string address,
            string redeem)
        {
            Id = id;
            Asset = asset;
            Value = value;
            Address = address;
            Redeem = redeem;
        }

        public CoinId Id { get; }
        public BlockchainAsset Asset { get; }
        public decimal Value { get; }
        public string Address { get; }
        public string Redeem { get; }

        public bool Equals(Coin other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Equals(Id, other.Id) && Equals(Asset, other.Asset)
                                        && Value == other.Value
                                        && Address == other.Address
                                        && Redeem == other.Redeem;
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is Coin other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Asset, Value, Address, Redeem);
        }
    }
}
