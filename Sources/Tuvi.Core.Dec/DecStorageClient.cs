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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core.Dec
{
    /// <summary>
    /// Interface for decentralized storage client operations.
    /// </summary>
    public interface IDecStorageClient : IDisposable
    {
        /// <summary>
        /// Sends a hash to the specified address using the decentralized service.
        /// </summary>
        /// <param name="address">The address to send to.</param>
        /// <param name="hash">The hash to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the service response.</returns>
        Task<string> SendAsync(string address, string hash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all hashes associated with the specified address.
        /// </summary>
        /// <param name="address">The address to list hashes for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the collection of hashes.</returns>
        Task<IEnumerable<string>> ListAsync(string address, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the data associated with the specified hash.
        /// </summary>
        /// <param name="hash">The hash to retrieve data for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the data as a byte array.</returns>
        Task<byte[]> GetAsync(string hash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads data to the decentralized service and returns the resulting hash.
        /// </summary>
        /// <param name="data">The data to upload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the hash of the uploaded data.</returns>
        Task<string> PutAsync(byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Claims a human-readable name for a specific public key using claim-v1.
        /// The method returns the public key currently bound to the name: the newly claimed key on success,
        /// or the existing key if the name is already taken.
        /// </summary>
        /// <param name="name">Name to claim.</param>
        /// <param name="publicKey">Public key (Base32E) to bind to the name.</param>
        /// <param name="signature">Signature over the claim-v1 payload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The public key currently bound to the name.</returns>
        Task<string> ClaimNameAsync(string name, string publicKey, string signature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves a human-readable name to its bound address.
        /// </summary>
        /// <param name="name">The human-readable name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Underlying address or error text / not-found indication.</returns>
        Task<string> GetAddressByNameAsync(string name, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Resolves human readable Eppie names to Base32E public keys.
    /// Placed in Dec layer to allow adapters over IDecStorageClient without depending on higher layers.
    /// </summary>
    public interface IEppieNameResolver
    {
        Task<string> ResolveAsync(string name, CancellationToken cancellationToken = default);
    }
}
