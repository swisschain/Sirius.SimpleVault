using System;

namespace SimpleVault.Common.Exceptions
{
    public class DbUnavailableException : Exception
    {
        public DbUnavailableException()
        {
        }

        public DbUnavailableException(Exception innerException)
            : base("Database is unavailable.", innerException)
        {
        }
    }
}
