using System;
using TuviPgpLib.Entities;

namespace Tuvi.Core.Backup
{
    public class BackupVerificationException : BackupDataProtectionException
    {
        public BackupVerificationException()
        {
        }

        public BackupVerificationException(string message) : base(message)
        {
        }

        public BackupVerificationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class BackupDataProtectionException : CryptoContextException
    {
        public BackupDataProtectionException()
        {
        }

        public BackupDataProtectionException(string message) : base(message)
        {
        }

        public BackupDataProtectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
