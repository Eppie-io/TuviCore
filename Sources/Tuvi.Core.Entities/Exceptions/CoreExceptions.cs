using System;
using System.Collections.Generic;

namespace Tuvi.Core.Entities
{
    public class CoreException : Exception
    {
        public CoreException()
        {
        }

        public CoreException(string message) : base(message)
        {
        }

        public CoreException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class NewMessagesCheckFailedException : CoreException
    {
        public List<EmailFolderError> ErrorsCollection { get; } = new List<EmailFolderError>();

        public NewMessagesCheckFailedException()
        {
        }

        public NewMessagesCheckFailedException(IEnumerable<EmailFolderError> errorsCollection)
        {
            if (errorsCollection != null)
            {
                ErrorsCollection.AddRange(errorsCollection);
            }
        }

        public NewMessagesCheckFailedException(string message) : base(message)
        {
        }

        public NewMessagesCheckFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public NewMessagesCheckFailedException(IEnumerable<EmailFolderError> errorsCollection, Exception innerException) : base(string.Empty, innerException)
        {
            if (errorsCollection != null)
            {
                ErrorsCollection.AddRange(errorsCollection);
            }
        }
    }

    public class NoPublicKeyException : CoreException
    {
        public EmailAddress Email { get; }

        public NoPublicKeyException(EmailAddress email, string message) : base(message)
        {
            Email = email;
        }

        public NoPublicKeyException(EmailAddress email, Exception innerException) : base(string.Empty, innerException)
        {
            Email = email;
        }

        private NoPublicKeyException()
        {
        }

        private NoPublicKeyException(string message) : base(message)
        {
        }

        private NoPublicKeyException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
