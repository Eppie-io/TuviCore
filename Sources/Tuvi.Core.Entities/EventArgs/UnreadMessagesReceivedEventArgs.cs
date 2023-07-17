using System;

namespace Tuvi.Core.Entities
{
    public class UnreadMessagesReceivedEventArgs : EventArgs
    {
        public EmailAddress Email { get; }
        public Folder Folder { get; }

        public UnreadMessagesReceivedEventArgs(EmailAddress email, Folder folder)
        {
            Email = email;
            Folder = folder;
        }
    }
}
