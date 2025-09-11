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

namespace Tuvi.Core.Dec.Ethereum
{
    /// <summary>
    /// Immutable configuration of an Ethereum network used by the DEC client:
    /// includes chain id, a human-friendly name and an explorer API base endpoint.
    /// </summary>
    public sealed class EthereumNetworkConfig
    {
        /// <summary>
        /// Gets a short identifier of the network (e.g., "mainnet", "sepolia").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the root URI of an Etherscan-compatible explorer API to query transactions.
        /// </summary>
        public Uri ExplorerApiBaseUrl { get; }

        /// <summary>
        /// Gets the EIP-155 chain id for the network as <see cref="EthereumNetwork"/>.
        /// </summary>
        public EthereumNetwork ChainId { get; }

        /// <summary>
        /// Gets a human-readable descriptive network name.
        /// </summary>
        public string HumanName { get; }

        /// <summary>
        /// Gets the API key to use with the configured explorer (optional, may be empty).
        /// </summary>
        public string ApiKey { get; }

        /// <summary>
        /// Initializes a new <see cref="EthereumNetworkConfig"/> instance.
        /// </summary>
        /// <param name="name">Short unique network name.</param>
        /// <param name="explorerApiBaseUrl">Base URL of Etherscan-compatible explorer API.</param>
        /// <param name="chainId">EIP-155 chain id as <see cref="EthereumNetwork"/>.</param>
        /// <param name="humanName">Human-friendly name. If null, falls back to <paramref name="name"/>.</param>
        /// <param name="apiKey">Optional explorer API key (may be empty).
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> or <paramref name="explorerApiBaseUrl"/> is null.</exception>
        public EthereumNetworkConfig(string name, Uri explorerApiBaseUrl, EthereumNetwork chainId, string humanName, string apiKey = "")
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ExplorerApiBaseUrl = explorerApiBaseUrl ?? throw new ArgumentNullException(nameof(explorerApiBaseUrl));
            ChainId = chainId;
            HumanName = humanName ?? name;
            ApiKey = apiKey ?? string.Empty;
        }

        /// <summary>
        /// Returns a copy of this configuration with a new API key value.
        /// </summary>
        public EthereumNetworkConfig WithApiKey(string apiKey)
            => new EthereumNetworkConfig(Name, ExplorerApiBaseUrl, ChainId, HumanName, apiKey ?? string.Empty);

        /// <summary>
        /// Predefined configuration for Ethereum mainnet (chainId=1) using api.etherscan.io.
        /// </summary>
        public static readonly EthereumNetworkConfig MainNet = new EthereumNetworkConfig(
            name: "mainnet",
            explorerApiBaseUrl: new Uri("https://api.etherscan.io/api", UriKind.Absolute),
            chainId: EthereumNetwork.MainNet,
            humanName: "Ethereum Mainnet");

        /// <summary>
        /// Predefined configuration for the Ethereum Sepolia test network (chainId=11155111) using api-sepolia.etherscan.io.
        /// </summary>
        public static readonly EthereumNetworkConfig Sepolia = new EthereumNetworkConfig(
            name: "sepolia",
            explorerApiBaseUrl: new Uri("https://api-sepolia.etherscan.io/api", UriKind.Absolute),
            chainId: EthereumNetwork.Sepolia,
            humanName: "Ethereum Sepolia Testnet");
    }
}
