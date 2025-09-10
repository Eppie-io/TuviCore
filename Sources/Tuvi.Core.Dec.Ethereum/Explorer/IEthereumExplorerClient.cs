// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2025 Eppie (https://eppie.io)                                    //
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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core.Dec.Ethereum.Explorer
{
    /// <summary>
    /// Abstraction for an Ethereum explorer client used to gather transactions.
    /// Implementations should never throw; they return empty results on errors.
    /// </summary>
    internal interface IEthereumExplorerClient
    {
        /// <summary>
        /// Returns address info (never null). On error returns an instance with empty outgoing list.
        /// </summary>
        Task<AddressInfo> GetAddressInfoAsync(string address, CancellationToken ct);
    }

    /// <summary>
    /// Simple container describing outgoing tx hashes for an address.
    /// </summary>
    internal sealed class AddressInfo
    {
        public string Address { get; }
        public IReadOnlyList<string> OutgoingTransactionHashes { get; }

        public AddressInfo(string address, IReadOnlyList<string> outgoing)
        {
            Address = address;
            OutgoingTransactionHashes = outgoing;
        }
    }
}
