using System;

namespace Tuvi.Core.Entities
{
    public class AccountEventArgs : EventArgs
    {
        public Account Account { get; }

        public AccountEventArgs(Account account)
        {
            Account = account;
        }
    }
}
