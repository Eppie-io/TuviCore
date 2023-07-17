using System;

namespace Tuvi.Core.Entities
{
    public class MessageDeletedEventArgs : EventArgs
    {
        public EmailAddress Email { get; }
        public Folder Folder { get; }
        public uint MessageID { get; }

        public MessageDeletedEventArgs(EmailAddress email, Folder folder, uint messageID)
        {
            Email = email;
            Folder = folder;
            MessageID = messageID;
        }
    }
}
