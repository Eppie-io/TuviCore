using System;

namespace Tuvi.Core.Entities
{
    public class DataBaseException : Exception
    {
        public DataBaseException()
        {
        }

        public DataBaseException(string message)
            : base(message)
        {
        }

        public DataBaseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class DataBasePasswordException : DataBaseException
    {
        public DataBasePasswordException()
        {
        }

        public DataBasePasswordException(string message)
            : base(message)
        {
        }

        public DataBasePasswordException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class DataBaseAlreadyExistsException : DataBaseException
    {
        public DataBaseAlreadyExistsException()
        {
        }

        public DataBaseAlreadyExistsException(string message)
            : base(message)
        {
        }

        public DataBaseAlreadyExistsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class DataBaseNotCreatedException : DataBaseException
    {
        public DataBaseNotCreatedException()
        {
        }

        public DataBaseNotCreatedException(string message)
            : base(message)
        {
        }

        public DataBaseNotCreatedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class NoCollectionException : DataBaseException
    {
        public NoCollectionException()
        {
        }

        public NoCollectionException(string message) : base(message)
        {
        }

        public NoCollectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class DataBaseMigrationException : DataBaseException
    {
        public DataBaseMigrationException()
        {
        }

        public DataBaseMigrationException(string message) : base(message)
        {
        }

        public DataBaseMigrationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
