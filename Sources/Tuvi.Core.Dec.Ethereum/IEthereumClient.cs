// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2026 Eppie (https://eppie.io)                                    //
//                                                                              //
//   Licensed under the Apache License, Version 2.0 (the "License"),            //
//   you may not use this file except in compliance with the License.           //
//   You may obtain a copy of the License at                                    //
//                                                                              //
//       http://www.apache.org/licenses/LICENSE-2.0                             //
//                                                                              //
//   Unless required by applicable law or agreed to in writing, software        //
//   distributed under the License is distributed on an "AS IS" BASIS,          //
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.   //
//   See the License for the specific language governing permissions and        //
//   limitations under the License.                                             //
//                                                                              //
// ---------------------------------------------------------------------------- //

using System.Threading;
using System.Threading.Tasks;
using KeyDerivation.Keys;

namespace Tuvi.Core.Dec.Ethereum
{
    /// <summary>
    /// Defines a high-level API for Ethereum-related operations used by the DEC layer.
    /// Implementations are expected to be thread-safe and reusable across calls.
    /// </summary>
    public interface IEthereumClient
    {
        /// <summary>
        /// Gets the network configuration used by this client instance (e.g., mainnet, sepolia, API base URL, chain id, API key).
        /// </summary>
        EthereumNetworkConfig Network { get; }

        /// <summary>
        /// Derives a checksummed Ethereum address for the BIP-44 path m/44'/60'/{account}'/0/{index}.
        /// </summary>
        /// <param name="masterKey">Root master key to derive from.</param>
        /// <param name="account">BIP-44 account index (must be non-negative).</param>
        /// <param name="index">Address index on the external chain (must be non-negative).</param>
        /// <returns>Checksummed Ethereum address starting with 0x.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="masterKey"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if <paramref name="account"/> or <paramref name="index"/> is negative.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown if the derived key data is invalid.</exception>
        string DeriveEthereumAddress(MasterKey masterKey, int account, int index);

        /// <summary>
        /// Derives a private key in hex form (without 0x) for the BIP-44 path m/44'/60'/{account}'/0/{index}.
        /// </summary>
        /// <param name="masterKey">Root master key to derive from.</param>
        /// <param name="account">BIP-44 account index (must be non-negative).</param>
        /// <param name="index">Address index on the external chain (must be non-negative).</param>
        /// <returns>Hex string of 64 characters representing the private key.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="masterKey"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if <paramref name="account"/> or <paramref name="index"/> is negative.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown if the derived key data is invalid.</exception>
        string DeriveEthereumPrivateKeyHex(MasterKey masterKey, int account, int index);

        /// <summary>
        /// Retrieves the Base32E-encoded compressed public key of an account by analyzing outgoing signed transactions on-chain.
        /// Returns an empty string if the key cannot be recovered (e.g., no suitable transactions).
        /// </summary>
        /// <param name="address">Ethereum address to inspect (0x-prefixed). Not null or whitespace.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Base32E-encoded compressed public key string, or empty string if not recoverable.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="address"/> is null or whitespace.</exception>
        /// <exception cref="Tuvi.Core.Dec.Ethereum.Explorer.ApiRateLimitExceededException">
        /// Thrown if the remote explorer reports a rate limit while reading transaction details.
        /// </exception>
        Task<string> RetrievePublicKeyAsync(string address, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the EIP-55 checksum of an Ethereum address.
        /// </summary>
        /// <param name="address">Ethereum address to validate.</param>
        /// <returns>True if the address has a valid EIP-55 checksum; otherwise, false. Null/whitespace returns false.</returns>
        bool ValidateChecksum(string address);
    }
}
