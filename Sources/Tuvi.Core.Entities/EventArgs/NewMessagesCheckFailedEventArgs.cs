using System;
using System.Collections.Generic;

namespace Tuvi.Core.Entities
{
    public class EmailFolderError
    {
        public EmailAddress Email { get; }
        public Folder Folder { get; }
        public Exception Exception { get; }

        public EmailFolderError(EmailAddress email, Folder folder, Exception exception)
        {
            if (email is null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            Email = email;
            Folder = folder;
            Exception = exception;
        }
    }

    public class NewMessagesCheckFailedEventArgs : EventArgs
    {
        public List<EmailFolderError> ErrorsCollection { get; } = new List<EmailFolderError>();

        public NewMessagesCheckFailedEventArgs(IEnumerable<EmailFolderError> errorsCollection)
        {
            if (errorsCollection != null)
            {
                ErrorsCollection.AddRange(errorsCollection);
            }
        }
    }
}
