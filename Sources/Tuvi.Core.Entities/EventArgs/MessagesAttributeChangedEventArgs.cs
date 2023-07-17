using System;
using System.Collections.Generic;

namespace Tuvi.Core.Entities
{
    public class MessagesAttributeChangedEventArgs
        : EventArgs
    {
        public EmailAddress Email { get; }
        public Folder Folder { get; }
        public IReadOnlyList<Message> Messages { get; }

        public MessagesAttributeChangedEventArgs(EmailAddress email, Folder folder, IReadOnlyList<Message> messages)
        {
            Email = email;
            Folder = folder;
            Messages = messages;
        }
    }
}
