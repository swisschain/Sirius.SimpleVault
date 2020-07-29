using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleVault.Common.Domain
{
    public class Cursor
    {
        private Cursor(long cursor, string id)
        {
            CursorValue = cursor;
            Id = id;
        }

        public string Id { get; }

        public long CursorValue { get; }

        public static Cursor CreateForWallet(long cursor)
        {
            return new Cursor(cursor, WalletId);
        }

        public static Cursor CreateForTransaction(long cursor)
        {
            return new Cursor(cursor, TransactionId);
        }

        public static string WalletId => nameof(Wallet);

        public static string TransactionId => nameof(Transaction);

        public static Cursor Restore(long entityCursor, string entityId)
        {
            return new Cursor(entityCursor, entityId);
        }
    }
}
