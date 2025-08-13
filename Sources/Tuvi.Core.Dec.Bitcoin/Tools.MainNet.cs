using KeyDerivation.Keys;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core.Dec.Bitcoin.MainNet
{
    /// <summary>
    /// Provides utility methods for Bitcoin-related operations, such as address and key derivation,
    /// and public key retrieval from the blockchain for MainNet.
    /// </summary>
    public static class Tools
    {
        static BitcoinNetworkConfig NetworkConfig = BitcoinNetworkConfig.MainNet;
        static HttpClient HttpClient = new HttpClient();

        /// <summary>
        /// Derives a Bitcoin address from the given master key using BIP44 derivation path.
        /// Uses hardened paths for account level as recommended by BIP44.
        /// </summary>
        /// <param name="masterKey">The master key to derive from.</param>
        /// <param name="account">The account index (must be between 0 and 2^31-1).</param>
        /// <param name="index">The address index (must be between 0 and 2^31-1).</param>
        /// <returns>The derived Bitcoin address as a string.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="masterKey"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="account"/> or <paramref name="index"/> is out of range.</exception>
        /// <exception cref="InvalidOperationException">Thrown if derivation fails.</exception>
        public static string DeriveBitcoinAddress(MasterKey masterKey, int account, int index)
        {
            return BitcoinToolsImpl.DeriveBitcoinAddress(NetworkConfig, masterKey, account, index);
        }

        /// <summary>
        /// Derives the Wallet Import Format (WIF) secret key from the given master key using BIP44 derivation path.
        /// Uses hardened paths for account level as recommended by BIP44.
        /// </summary>
        /// <param name="masterKey">The master key to derive from.</param>
        /// <param name="account">The account index (must be between 0 and 2^31-1).</param>
        /// <param name="index">The address index (must be between 0 and 2^31-1).</param>
        /// <returns>The derived secret key in WIF format as a string.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="masterKey"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="account"/> or <paramref name="index"/> is out of range.</exception>
        /// <exception cref="InvalidOperationException">Thrown if derivation fails.</exception>
        public static string DeriveBitcoinSecretKeyWif(MasterKey masterKey, int account, int index)
        {
            return BitcoinToolsImpl.DeriveBitcoinSecretKeyWif(NetworkConfig, masterKey, account, index);
        }

        /// <summary>
        /// Retrieves the public key associated with a Bitcoin address by inspecting blockchain transactions.
        /// Note: This method supports only legacy (P2PKH) addresses and requires the address to have at least one spent transaction.
        /// If the address has no spent outputs or uses a modern address type (e.g., SegWit or Taproot), the public key cannot be retrieved.
        /// </summary>
        /// <param name="address">The Bitcoin address to retrieve the public key for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>The public key encoded in Base32E format, or null if not found.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="address"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="address"/> is invalid for the network.</exception>
        /// <exception cref="HttpRequestException">Thrown if the API request fails.</exception>
        /// <exception cref="JsonException">Thrown if JSON deserialization fails.</exception>
        public static Task<string> RetrievePublicKeyAsync(string address, CancellationToken cancellationToken = default)
        {
            return BitcoinToolsImpl.RetrievePublicKeyAsync(NetworkConfig, address, HttpClient, cancellationToken);
        }
    }
}