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

        public static int GetCoinType(this Account account)
        {
            if (account?.Email is null)
            {
                throw new ArgumentNullException(nameof(account), "Coin type is impossible to get.");
            }

            return account.Email.GetCoinType();
        }

        public static int GetChannel(this Account account)
        {
            if (account?.Email is null)
            {
                throw new ArgumentNullException(nameof(account), "Channel is impossible to get.");
            }

            return account.Email.GetChannel();
        }

        public static int GetKeyIndex(this Account account)
        {
            if (account?.Email is null)
            {
                throw new ArgumentNullException(nameof(account), "Key index is impossible to get.");
            }

            return account.Email.GetKeyIndex();
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


        const int EppieCoinType = 3630;
        const int EmailChannel = 10;

        const int BitcoinCoinType = 0;
        const int Change = 0;

        const int KeyIndex = 0;

        public static int GetCoinType(this EmailAddress emailAddress)
        {
            if (emailAddress is null)
            {
                throw new ArgumentNullException(nameof(emailAddress));
            }

            if (emailAddress.IsHybrid)
            {
                throw new NotSupportedException("Hybrid email accounts don't have coin type. Use GetKeyTag() method instead.");
            }

            return GetCoinType(emailAddress.Network);
        }

        public static int GetCoinType(this NetworkType network)
        {
            switch (network)
            {
                case NetworkType.Bitcoin:
                    return BitcoinCoinType;
                case NetworkType.Eppie:
                    return EppieCoinType;
                default:
                    throw new NotSupportedException($"Unsupported network : {network}");
            }
        }

        public static int GetChannel(this EmailAddress emailAddress)
        {
            if (emailAddress is null)
            {
                throw new ArgumentNullException(nameof(emailAddress));
            }

            if (emailAddress.IsHybrid)
            {
                throw new NotSupportedException("Hybrid email accounts don't have channel. Use GetKeyTag() method instead.");
            }

            return GetChannel(emailAddress.Network);
        }

        public static int GetChannel(this NetworkType network)
        {
            switch (network)
            {
                case NetworkType.Bitcoin:
                    return Change;
                case NetworkType.Eppie:
                    return EmailChannel;
                default:
                    throw new NotSupportedException($"Unsupported network : {network}");
            }
        }

        public static int GetKeyIndex(this EmailAddress emailAddress)
        {
            if (emailAddress is null)
            {
                throw new ArgumentNullException(nameof(emailAddress));
            }

            if (emailAddress.IsHybrid)
            {
                throw new NotSupportedException("Hybrid email accounts don't have key index. Use GetKeyTag() method instead.");
            }

            return GetKeyIndex(emailAddress.Network);
        }

        public static int GetKeyIndex(this NetworkType network)
        {
            return KeyIndex;
        }
    }
}