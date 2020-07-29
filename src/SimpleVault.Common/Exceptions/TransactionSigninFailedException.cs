using System;

namespace SimpleVault.Common.Exceptions
{
    public class TransactionSigninFailedException : Exception
    {
        public TransactionSigninFailedException()
        {
        }

        public TransactionSigninFailedException(string message)
            : base(message)
        {
        }

        public TransactionSigninFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
