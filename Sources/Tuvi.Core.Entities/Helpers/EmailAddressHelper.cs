namespace Tuvi.Core.Entities
{
    public static class EmailAddressHelper
    {
        public static string GetKeyFromEmail(EmailAddress email)
        {
            return email?.Address.ToUpperInvariant() ?? string.Empty;
        }
    }
}
