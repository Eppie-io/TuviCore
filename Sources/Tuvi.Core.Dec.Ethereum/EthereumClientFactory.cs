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

using System;
using System.Net.Http;

namespace Tuvi.Core.Dec.Ethereum
{
    /// <summary>
    /// Factory for creating <see cref="IEthereumClient"/> instances bound to a known network
    /// or to a custom <see cref="EthereumNetworkConfig"/>.
    /// </summary>
    public static class EthereumClientFactory
    {
        /// <summary>
        /// Creates an <see cref="IEthereumClient"/> for a predefined <see cref="EthereumNetwork"/>.
        /// </summary>
        /// <param name="network">Known target network.</param>
        /// <param name="httpClient">HttpClient.</param>
        /// <param name="apiKey">Optional API key.</param>
        /// <returns>Instantiated <see cref="IEthereumClient"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="network"/> is not supported.</exception>
        public static IEthereumClient Create(EthereumNetwork network, HttpClient httpClient, string apiKey = null)
        {
            EthereumNetworkConfig config;
            switch (network)
            {
                case EthereumNetwork.MainNet:
                    config = EthereumNetworkConfig.MainNet; break;
                case EthereumNetwork.Sepolia:
                    config = EthereumNetworkConfig.Sepolia; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(network));
            }
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                config = config.WithApiKey(apiKey);
            }
            return new EthereumClient(config, httpClient);
        }

        /// <summary>
        /// Creates an <see cref="IEthereumClient"/> bound to the provided <see cref="EthereumNetworkConfig"/> instance.
        /// </summary>
        /// <param name="config">Network configuration to use (not null).</param>
        /// <param name="httpClient">Optional HttpClient to reuse; if null, a new instance will be created.</param>
        /// <returns>Instantiated <see cref="IEthereumClient"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> is null.</exception>
        public static IEthereumClient Create(EthereumNetworkConfig config, HttpClient httpClient = null)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return new EthereumClient(config, httpClient);
        }
    }
}
