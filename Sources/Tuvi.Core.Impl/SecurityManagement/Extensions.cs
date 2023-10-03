using System;
using System.Diagnostics.Contracts;
using Tuvi.Core.Entities;
using TuviPgpLib.Entities;

namespace Tuvi.Core.Impl.SecurityManagement
{
    public static class AccountExtensions
    {
        public static string GetPgpUserIdentity(this Account account)
        {
            EmailAddress email = account?.Email;
            if (email is null)
            {
                System.Diagnostics.Debug.Assert(false, "TuviPgpLib.AccountExtensions: Email is null.");
                throw new PgpArgumentNullException("PGP user id is impossible to get.", new ArgumentException($"Email is null."));
            }
            if (email.IsHybrid)
            {
                return email.StandardAddress.ToString();
            }
            return email.Address;
        }

        public static string GetPgpKeyTag(this Account account)
        {
            Contract.Requires(account != null);
            if (string.IsNullOrEmpty(account.KeyTag))
            {
                return account.Email.Address;
            }
            return account.KeyTag;
        }
    }

    public static class EmailAddressExtensions
    {
        public static UserIdentity ToUserIdentity(this EmailAddress emailAddress)
        {
            if (emailAddress is null)
            {
                throw new ArgumentNullException(nameof(emailAddress));
            }

            return new UserIdentity(emailAddress.Name, emailAddress.Address);
        }
    }
}
