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
using KeyDerivation.Keys;
using Org.BouncyCastle.Crypto.Parameters;
using Tuvi.Core.Dec;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Utils
{
    public interface IPublicKeyService
    {
        string Encode(ECPublicKeyParameters key);
        ECPublicKeyParameters Decode(string encoded);
        ECPublicKeyParameters Derive(MasterKey masterKey, int coin, int account, int channel, int index);
        ECPublicKeyParameters Derive(MasterKey masterKey, string keyTag);
        string DeriveEncoded(MasterKey masterKey, int coin, int account, int channel, int index);
        string DeriveEncoded(MasterKey masterKey, string keyTag);
        Task<ECPublicKeyParameters> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken);
        Task<string> GetEncodedByEmailAsync(EmailAddress email, CancellationToken cancellationToken);
        string DeriveNetworkAddress(MasterKey masterKey, NetworkType network, int coin, int account, int channel, int index);
    }

    /// <summary>
    /// Facade service for working with public keys (encoding, derivation, resolution).
    /// </summary>
    public sealed class PublicKeyService : IPublicKeyService
    {
        private readonly IEcPublicKeyCodec _codec;
        private readonly IEmailPublicKeyResolver _resolver;
        private readonly IKeyDerivationPublicKeyProvider _derivation;
        private readonly Dec.Ethereum.IEthereumClient _ethClient;

        internal sealed class NoOpEppieNameResolver : IEppieNameResolver
        {
            public Task<string> ResolveAsync(string name, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<string>(null);
            }
        }

        public static readonly IEppieNameResolver NoOpNameResolver = new NoOpEppieNameResolver();

        public PublicKeyService(IEcPublicKeyCodec codec, IEmailPublicKeyResolver resolver, IKeyDerivationPublicKeyProvider derivation, Dec.Ethereum.IEthereumClient ethClient)
        {
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _derivation = derivation ?? throw new ArgumentNullException(nameof(derivation));

            _ethClient = ethClient;
        }

        public string Encode(ECPublicKeyParameters key) => _codec.Encode(key);
        public ECPublicKeyParameters Decode(string encoded) => _codec.Decode(encoded);

        public ECPublicKeyParameters Derive(MasterKey masterKey, int coin, int account, int channel, int index) => _derivation.Derive(masterKey, coin, account, channel, index);
        public ECPublicKeyParameters Derive(MasterKey masterKey, string keyTag) => _derivation.Derive(masterKey, keyTag);

        public string DeriveEncoded(MasterKey masterKey, int coin, int account, int channel, int index) => Encode(Derive(masterKey, coin, account, channel, index));
        public string DeriveEncoded(MasterKey masterKey, string keyTag) => Encode(Derive(masterKey, keyTag));

        public async Task<ECPublicKeyParameters> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken)
        {
            if (email is null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            var encoded = await _resolver.ResolveAsync(email, cancellationToken).ConfigureAwait(false);
            return Decode(encoded);
        }

        public Task<string> GetEncodedByEmailAsync(EmailAddress email, CancellationToken cancellationToken)
        {
            if (email is null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            return _resolver.ResolveAsync(email, cancellationToken);
        }

        public string DeriveNetworkAddress(MasterKey masterKey, NetworkType network, int coin, int account, int channel, int index)
        {
            if (masterKey is null)
            {
                throw new ArgumentNullException(nameof(masterKey));
            }

            if (account < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(account));
            }

            switch (network)
            {
                case NetworkType.Eppie:
                    return DeriveEncoded(masterKey, coin, account, channel, index);
                case NetworkType.Bitcoin:
                    return Dec.Bitcoin.TestNet4.Tools.DeriveBitcoinAddress(masterKey, account, index);
                case NetworkType.Ethereum:
                    if (_ethClient is null)
                    {
                        throw new NotSupportedException("Ethereum client is not configured.");
                    }
                    return _ethClient.DeriveEthereumAddress(masterKey, account, index);
                default:
                    throw new NotSupportedException($"Unsupported network: {network}");
            }
        }

        /// <summary>
        /// Creates a default service wiring standard implementations (no explicit Ethereum API key provided).
        /// </summary>
        internal static PublicKeyService CreateDefault(IEppieNameResolver eppieNameResolver)
        {
            if (eppieNameResolver is null)
            {
                throw new ArgumentNullException(nameof(eppieNameResolver));
            }

            var codec = new Secp256k1CompressedBase32ECodec();
            var derivation = new EccKeyDerivationPublicKeyProvider();
            var composite = new CompositeEmailPublicKeyResolver(new Dictionary<NetworkType, IEmailPublicKeyResolver>
            {
                { NetworkType.Bitcoin, new BitcoinEmailPublicKeyResolver(new BitcoinPublicKeyFetcher()) },
                { NetworkType.Eppie, new EppieEmailPublicKeyResolver(codec, eppieNameResolver) },
                { NetworkType.Ethereum, new EthereumEmailPublicKeyResolver(new EthereumPublicKeyFetcher()) }
            });

            return new PublicKeyService(codec, composite, derivation, null);
        }

        /// <summary>
        /// Creates a default service wiring standard implementations.
        /// </summary>
        public static PublicKeyService CreateDefault(IEppieNameResolver eppieNameResolver, string etherscanApiKey, System.Net.Http.HttpClient httpClient)
        {
            if (eppieNameResolver is null)
            {
                throw new ArgumentNullException(nameof(eppieNameResolver));
            }
            if (httpClient is null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            var codec = new Secp256k1CompressedBase32ECodec();
            var derivation = new EccKeyDerivationPublicKeyProvider();
            var ethClient = Dec.Ethereum.EthereumClientFactory.Create(Dec.Ethereum.EthereumNetwork.MainNet, httpClient, etherscanApiKey);
            var composite = new CompositeEmailPublicKeyResolver(new Dictionary<NetworkType, IEmailPublicKeyResolver>
            {
                { NetworkType.Bitcoin, new BitcoinEmailPublicKeyResolver(new BitcoinPublicKeyFetcher()) },
                { NetworkType.Eppie, new EppieEmailPublicKeyResolver(codec, eppieNameResolver) },
                { NetworkType.Ethereum, new EthereumEmailPublicKeyResolver(new EthereumPublicKeyFetcher(ethClient)) }
            });

            return new PublicKeyService(codec, composite, derivation, ethClient);
        }
    }
}
