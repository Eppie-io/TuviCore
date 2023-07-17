using System;
using System.Collections.Generic;

namespace Tuvi.Core.Entities
{
    public class FolderMessagesReceivedEventArgs : EventArgs
    {
        public EmailAddress AccountEmail { get; }
        public Folder Folder { get; }
        public IReadOnlyList<Message> Messages { get; }


        public FolderMessagesReceivedEventArgs(EmailAddress accountEmail, Folder folder, IReadOnlyList<Message> messages)
        {
            AccountEmail = accountEmail;
            Folder = folder;
            Messages = messages;
        }
    }
}
