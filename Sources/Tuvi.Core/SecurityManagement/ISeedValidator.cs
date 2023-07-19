using System.Threading.Tasks;

namespace Tuvi.Core
{
    /// <summary>
    /// Validation of mnemonic seed phrase words dictionary.
    /// </summary>
    public interface ISeedValidator
    {
        /// <summary>
        /// Check if <paramref name="word"/> exist in english mnemonic dictionary.
        /// </summary>
        bool IsWordExistInDictionary(string word);

        /// <summary>
        /// Check if <paramref name="word"/> exist in english mnemonic dictionary.
        /// </summary>
        Task<bool> IsWordExistInDictionaryAsync(string word);
    }
}
