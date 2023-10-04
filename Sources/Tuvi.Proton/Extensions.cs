using System.Diagnostics.Contracts;
using System.Linq;
using Tuvi.Core.Entities;

namespace Tuvi.Proton
{
    public static class Extensions
    {
        private static readonly string[] ProtonSuffixes = new[] { "@proton.me", "@protonmail.com", "@proton.local" };
        public static bool IsProton(this EmailAddress emailAddress)
        {
            Contract.Requires(emailAddress != null);
            return ProtonSuffixes.Any(x => emailAddress.Address.EndsWith(x, System.StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
