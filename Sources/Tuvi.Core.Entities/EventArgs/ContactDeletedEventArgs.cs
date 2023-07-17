using System;

namespace Tuvi.Core.Entities
{
    public class ContactDeletedEventArgs : EventArgs
    {
        public EmailAddress ContactEmail { get; }

        public ContactDeletedEventArgs(EmailAddress contactEmail)
        {
            ContactEmail = contactEmail;
        }
    }
}
