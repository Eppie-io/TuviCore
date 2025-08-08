using System;
using Tuvi.Core.Entities;
using TuviPgpLib.Entities;

namespace Tuvi.Core
{
    public static class AccountExtensions
    {
        public static string GetPgpUserIdentity(this Account account)
        {
            EmailAddress email = account?.Email;
            if (email is null)
            {
                throw new ArgumentNullException(nameof(account), "PGP user id is impossible to get.");
            }

            return email.Address;
        }

        public static string GetKeyTag(this Account account)
        {
            if (account?.Email is null)
            {
                throw new ArgumentNullException(nameof(account), "Key tag is impossible to get.");
            }

            return account.Email.GetKeyTag();
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

        public static string GetKeyTag(this EmailAddress emailAddress)
        {
            if (emailAddress is null)
            {
                throw new ArgumentNullException(nameof(emailAddress));
            }

            var keyTag = emailAddress.IsHybrid ? emailAddress.StandardAddress : emailAddress.Address;
            return keyTag;
        }
    }
}