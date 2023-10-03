using KeyDerivationLib;
using System.Globalization;
using System.Threading.Tasks;
using Tuvi.Core;

namespace Tuvi.Core.Impl.SecurityManagement
{
    internal class SeedValidator : ISeedValidator
    {
        public bool IsWordExistInDictionary(string word)
        {
            return MasterKeyFactory.IsWordExistInDictionary(SeedNormalizer.NormalizeWord(word));
        }

        public Task<bool> IsWordExistInDictionaryAsync(string word)
        {
            return Task.Run(() => MasterKeyFactory.IsWordExistInDictionary(SeedNormalizer.NormalizeWord(word)));
        }
    }

    internal static class SeedNormalizer
    {
        public static string NormalizeWord(string str)
        {
            CultureInfo culture = new CultureInfo("en-US");
            return str?.ToLower(culture).Trim() ?? string.Empty;
        }
    }
}
