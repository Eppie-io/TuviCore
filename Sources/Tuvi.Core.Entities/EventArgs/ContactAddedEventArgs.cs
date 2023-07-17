using System;

namespace Tuvi.Core.Entities
{
    public class ContactAddedEventArgs : EventArgs
    {
        public Contact Contact { get; }

        public ContactAddedEventArgs(Contact contact)
        {
            Contact = contact;
        }
    }
}
