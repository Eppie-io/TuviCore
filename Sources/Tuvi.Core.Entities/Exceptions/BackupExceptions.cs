using System;

namespace Tuvi.Core.Entities.Exceptions
{
    public class BackupException : Exception
    {
        public BackupException()
        {
        }

        public BackupException(string message) : base(message)
        {
        }

        public BackupException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class BackupParsingException : BackupException
    {
        public BackupParsingException()
        {
        }

        public BackupParsingException(string message) : base(message)
        {
        }

        public BackupParsingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class NotBackupPackageException : BackupParsingException
    {
        public NotBackupPackageException()
        {
        }

        public NotBackupPackageException(string message) : base(message)
        {
        }

        public NotBackupPackageException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class UnknownBackupProtectionException : BackupParsingException
    {
        public UnknownBackupProtectionException()
        {
        }

        public UnknownBackupProtectionException(string message) : base(message)
        {
        }

        public UnknownBackupProtectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class BackupDeserializationException : BackupException
    {
        public BackupDeserializationException()
        {
        }

        public BackupDeserializationException(string message) : base(message)
        {
        }

        public BackupDeserializationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class BackupBuildingException : BackupException
    {
        public BackupBuildingException()
        {
        }

        public BackupBuildingException(string message) : base(message)
        {
        }

        public BackupBuildingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class BackupSerializationException : BackupException
    {
        public BackupSerializationException()
        {
        }

        public BackupSerializationException(string message) : base(message)
        {
        }

        public BackupSerializationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
