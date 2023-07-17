using System;
using System.Collections.Generic;

namespace Tuvi.Core.Entities
{
    public class ReceivedMessageInfo
    {
        public EmailAddress Email { get; }
        public Folder Folder => Message.Folder;
        public Message Message { get; }

        public ReceivedMessageInfo(EmailAddress email, Message message)
        {
            Email = email;
            Message = message;
        }
    }

    public class MessagesReceivedEventArgs : EventArgs
    {
        public List<ReceivedMessageInfo> ReceivedMessages { get; }

        public MessagesReceivedEventArgs(List<ReceivedMessageInfo> receivedMessages)
        {
            ReceivedMessages = receivedMessages;
        }
    }
}
