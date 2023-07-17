using System;

namespace Tuvi.Core.Entities
{
    public static class StringHelper
    {
        /// <summary>
        /// Checks if two emails <paramref name="email1"/> and <paramref name="email2"/> are considered to be equal
        /// </summary>
        /// <param name="email1"></param>
        /// <param name="email2"></param>
        /// <returns></returns>
        public static bool AreEmailsEqual(string email1, string email2)
        {
            return string.Equals(email1, email2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Compares two emails <paramref name="email1"/> and <paramref name="email2"/>
        /// </summary>
        /// <param name="email1"></param>
        /// <param name="email2"></param>
        /// <returns></returns>
        public static int CompareEmails(string email1, string email2)
        {
            return string.Compare(email1, email2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the beginning of <paramref name="email"/> string matches the <paramref name="text"/>
        /// </summary>
        /// <param name="email"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool EmailStartsWith(string email, string text)
        {
            return email != null && email.StartsWith(text, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns a value indicating whether <paramref name="text"/> occures within <paramref name="email"/>
        /// </summary>
        /// <param name="email"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool EmailContains(string email, string text)
        {
            return StringContains(email, text, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns a value indicating whether <paramref name="subText"/> occures within <paramref name="text"/> according to <paramref name="comparison"/>
        /// </summary>
        /// <param name="text"></param>
        /// <param name="subText"></param>
        /// <param name="comparison"></param>
        /// <returns></returns>
        public static bool StringContains(string text, string subText, StringComparison comparison)
        {
            return string.IsNullOrEmpty(subText)
                || (text != null && text.IndexOf(subText, comparison) >= 0);
        }

        /// <summary>
        /// Returns a value indicating whether <paramref name="subText"/> occures within <paramref name="text"/> according to <paramref name="comparison"/> ignoring case
        /// </summary>
        /// <param name="text"></param>
        /// <param name="subText"></param>
        /// <returns></returns>
        public static bool StringContainsIgnoreCase(string text, string subText)
        {
            return string.IsNullOrEmpty(subText)
                || (text != null && text.IndexOf(subText, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        private static string DecEppiePostfix = "@dec.eppie.io";
        public static bool IsDecentralizedEmail(EmailAddress email)
        {
            return email.Address.IndexOf(DecEppiePostfix, StringComparison.CurrentCultureIgnoreCase) == email.Address.Length - DecEppiePostfix.Length;
        }

        public static string MakeDecentralizedEmail(string name)
        {
            return name + DecEppiePostfix;
        }

        public static string GetDecentralizedAddress(EmailAddress email)
        {
            int pos = email.Address.IndexOf(DecEppiePostfix, StringComparison.OrdinalIgnoreCase);
            if (pos == -1)
            {
                return string.Empty;
            }
            return email.Address.Substring(0, pos);
        }

        public static string BytesToHex(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", "");
        }

    }
}
