using System;

namespace Tuvi.Core.Entities
{
    public class ConnectionException : Exception
    {
        public ConnectionException()
        {
        }

        public ConnectionException(string message)
            : base(message)
        {
        }

        public ConnectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class MailServiceIsNotConnectionException : Exception
    {
        public MailServiceIsNotConnectionException()
        {
        }

        public MailServiceIsNotConnectionException(string message)
            : base(message)
        {
        }

        public MailServiceIsNotConnectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class AuthentificationException : Exception
    {
        public EmailAddress Email { get; private set; }

        public AuthentificationException(EmailAddress email, string message, Exception innerException)
            : base(message, innerException)
        {
            Email = email;
        }

        public AuthentificationException()
        {
        }

        public AuthentificationException(string message)
            : base(message)
        {
        }
        public AuthentificationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class MailServiceIsNotAuthentificatedException : Exception
    {
        public MailServiceIsNotAuthentificatedException()
        {
        }

        public MailServiceIsNotAuthentificatedException(string message)
            : base(message)
        {
        }

        public MailServiceIsNotAuthentificatedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class AccountAlreadyExistInDatabaseException : Exception
    {
        public AccountAlreadyExistInDatabaseException()
        {
        }

        public AccountAlreadyExistInDatabaseException(string message)
            : base(message)
        {
        }

        public AccountAlreadyExistInDatabaseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class AccountIsNotExistInDatabaseException : Exception
    {
        public AccountIsNotExistInDatabaseException()
        {
        }

        public AccountIsNotExistInDatabaseException(string message)
            : base(message)
        {
        }

        public AccountIsNotExistInDatabaseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class MessageAlreadyExistInDatabaseException : Exception
    {
        public MessageAlreadyExistInDatabaseException()
        {
        }

        public MessageAlreadyExistInDatabaseException(string message)
            : base(message)
        {
        }

        public MessageAlreadyExistInDatabaseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class MessageIsNotExistException : Exception
    {
       public MessageIsNotExistException()
        {
        }

        public MessageIsNotExistException(string message) : base(message)
        {
        }

        public MessageIsNotExistException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "<Pending>")]
    public class ProtocolIsNotSupportedException : Exception
    {
        public MailProtocol Protocol { get; }
        public ProtocolIsNotSupportedException(MailProtocol protocol)
        {
            Protocol = protocol;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "<Pending>")]
    public class FolderIsNotExistException : Exception
    {
        public string Folder { get; }

        public FolderIsNotExistException(string folder)
        {
            Folder = folder;
        }
    }

    public class ContactAlreadyExistInDatabaseException : Exception
    {
        public ContactAlreadyExistInDatabaseException()
        {
        }

        public ContactAlreadyExistInDatabaseException(string message)
            : base(message)
        {
        }

        public ContactAlreadyExistInDatabaseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}