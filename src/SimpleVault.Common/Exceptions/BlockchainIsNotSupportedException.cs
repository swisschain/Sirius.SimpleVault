using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleVault.Common.Exceptions
{
    public class BlockchainIsNotSupportedException : Exception
    {
        public BlockchainIsNotSupportedException(Exception innerException)
            : base(innerException.Message, innerException)
        {
        }
    }
}
