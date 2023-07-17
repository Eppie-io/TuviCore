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
}
