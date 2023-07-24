using System;

namespace Tuvi.Core.Dec
{
    public class DecException : Exception
    {
        public DecException() : base()
        {
        }

        public DecException(string message) : base(message)
        {
        }

        public DecException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
