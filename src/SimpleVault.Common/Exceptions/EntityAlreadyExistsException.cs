using System;

namespace SimpleVault.Common.Exceptions
{
    public class EntityAlreadyExistsException : Exception
    {
        public EntityAlreadyExistsException()
        {
        }

        public EntityAlreadyExistsException(Exception innerException)
            : base("Entity already exists.", innerException)
        {
        }
    }
}
