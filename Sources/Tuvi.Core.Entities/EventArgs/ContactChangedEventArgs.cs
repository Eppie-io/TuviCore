using System;

namespace Tuvi.Core.Entities
{
    public class ContactChangedEventArgs : EventArgs
    {
        public Contact Contact { get; }

        public ContactChangedEventArgs(Contact contact)
        {
            Contact = contact;
        }
    }
}
