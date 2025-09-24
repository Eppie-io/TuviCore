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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Dec;
using Tuvi.Core.Dec.Bitcoin.TestNet4;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Utils
{
    /// <summary>
    /// Resolves a public key (Base32E encoded) bound to an EmailAddress for decentralized networks.
    /// </summary>
    public interface IEmailPublicKeyResolver
    {
        Task<string> ResolveAsync(EmailAddress email, CancellationToken cancellationToken);
    }

    public interface IBitcoinPublicKeyFetcher
    {
        Task<string> FetchAsync(string address);
    }

    internal sealed class BitcoinPublicKeyFetcher : IBitcoinPublicKeyFetcher
    {
        public Task<string> FetchAsync(string address)
        {
            return Tools.RetrievePublicKeyAsync(address);
        }
    }

    internal sealed class BitcoinEmailPublicKeyResolver : IEmailPublicKeyResolver
    {
        private readonly IBitcoinPublicKeyFetcher _fetcher;
        public BitcoinEmailPublicKeyResolver(IBitcoinPublicKeyFetcher fetcher)
        {
            _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
        }

        public async Task<string> ResolveAsync(EmailAddress email, CancellationToken cancellationToken)
        {
            if (email is null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            var bitcoinAddress = email.DecentralizedAddress;
            var publicKey = await _fetcher.FetchAsync(bitcoinAddress).ConfigureAwait(false);
            if (publicKey is null)
            {
                throw new NoPublicKeyException(email, $"Public key is not found for the {bitcoinAddress} Bitcoin address");
            }

            return publicKey;
        }
    }

    internal sealed class EppieEmailPublicKeyResolver : IEmailPublicKeyResolver
    {
        private readonly IEcPublicKeyCodec _codec;
        private readonly IEppieNameResolver _nameResolver;
        private readonly INetworkPublicKeyRules _rules;

        public EppieEmailPublicKeyResolver(IEcPublicKeyCodec codec, IEppieNameResolver nameResolver)
        {
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
            _rules = NetworkPublicKeyRulesFactory.Create(NetworkType.Eppie, _codec);
        }

        public Task<string> ResolveAsync(EmailAddress email, CancellationToken cancellationToken)
        {
            return ResolveInternalAsync(email, cancellationToken);
        }

        private async Task<string> ResolveInternalAsync(EmailAddress email, CancellationToken ct)
        {
            if (email is null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            var segment = email.DecentralizedAddress;
            if (string.IsNullOrWhiteSpace(segment))
            {
                throw new NoPublicKeyException(email, "Eppie address segment is empty.");
            }

            if (_rules.IsValid(segment))
            {
                return segment;
            }

            var resolved = await _nameResolver.ResolveAsync(segment, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(resolved))
            {
                throw new NoPublicKeyException(email, $"Public key not found for {segment}.");
            }

            if (!_rules.IsValid(resolved))
            {
                throw new NoPublicKeyException(email, $"Resolved value for {segment} has invalid format.");
            }

            return resolved;
        }
    }

    public interface IEthereumPublicKeyFetcher
    {
        Task<string> FetchAsync(string address, CancellationToken cancellationToken);
    }

    internal sealed class EthereumPublicKeyFetcher : IEthereumPublicKeyFetcher
    {
        private readonly Dec.Ethereum.IEthereumClient _client;
        private static readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();

        public EthereumPublicKeyFetcher()
            : this(Dec.Ethereum.EthereumClientFactory.Create(Dec.Ethereum.EthereumNetwork.MainNet, _httpClient))
        {
        }

        public EthereumPublicKeyFetcher(Dec.Ethereum.IEthereumClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<string> FetchAsync(string address, CancellationToken cancellationToken)
        {
            return _client.RetrievePublicKeyAsync(address, cancellationToken);
        }
    }

    internal sealed class EthereumEmailPublicKeyResolver : IEmailPublicKeyResolver
    {
        private readonly IEthereumPublicKeyFetcher _fetcher;

        public EthereumEmailPublicKeyResolver(IEthereumPublicKeyFetcher fetcher)
        {
            _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
        }

        public async Task<string> ResolveAsync(EmailAddress email, CancellationToken cancellationToken)
        {
            if (email is null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            var address = email.DecentralizedAddress;
            var pubKey = await _fetcher.FetchAsync(address, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(pubKey))
            {
                throw new NoPublicKeyException(email, $"Public key is not found for the {address} Ethereum address");
            }

            return pubKey;
        }
    }

    internal sealed class CompositeEmailPublicKeyResolver : IEmailPublicKeyResolver
    {
        private readonly Dictionary<NetworkType, IEmailPublicKeyResolver> _map;

        public CompositeEmailPublicKeyResolver(Dictionary<NetworkType, IEmailPublicKeyResolver> map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public Task<string> ResolveAsync(EmailAddress email, CancellationToken cancellationToken)
        {
            if (email is null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            if (_map.TryGetValue(email.Network, out var resolver))
            {
                return resolver.ResolveAsync(email, cancellationToken);
            }

            throw new NotSupportedException($"Network type {email.Network} is not supported for Decentralized MailBox.");
        }
    }
}
