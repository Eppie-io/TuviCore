using System;
using TuviPgpLib.Entities;

namespace Tuvi.Core.Impl.SecurityManagement
{
    public class PgpArgumentNullException : CryptoContextException
    {
        public PgpArgumentNullException() : base()
        {
        }

        public PgpArgumentNullException(string parameterName) : base(parameterName)
        {
        }

        public PgpArgumentNullException(string parameterName, Exception innerException) : base(parameterName, innerException)
        {
        }
    }
}
