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
using System.Threading;
using System.Threading.Tasks;
using KeyDerivation.Keys;
using KeyDerivationLib;
using Microsoft.Extensions.Logging;
using Nethereum.Signer;
using Nethereum.Util;
using Tuvi.Base32EConverterLib;
using Tuvi.Core.Dec.Ethereum.Explorer;
using Tuvi.Core.Logging;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Tuvi.Core.Dec.Ethereum.Tests")]

namespace Tuvi.Core.Dec.Ethereum
{
    /// <summary>
    /// Default implementation of IEthereumClient with address/key derivation, formatting and public key retrieval helpers.
    /// </summary>
    internal sealed class EthereumClient : IEthereumClient
    {
        private static readonly ILogger Logger = LoggingExtension.Log<EthereumClient>();

        // BIP44 constants
        internal const int EthereumCoinType = 60;   // BIP44 coin type for Ethereum
        internal const int ExternalChange = 0;      // BIP44 external chain index

        // General crypto constants
        private const int Bytes32 = 32;
        private const int PubKeyRawLength = 64;            // X||Y
        private const int PubKeyUncompressedLength = 65;   // 0x04 || X || Y
        private const int PubKeyCompressedLength = 33;     // 0x02/0x03 || X
        private const int PrefixByteLength = 1;
        private const int LastByteIndex32 = Bytes32 - 1;

        private const byte PubKeyUncompressedPrefix = 0x04;
        private const byte PubKeyCompressedEvenPrefix = 0x02;
        private const byte PubKeyCompressedOddPrefix = 0x03;

        private const byte VParityBase = 27;   // 27 or 28 in legacy Ethereum v
        private const byte OneBitMask = 0x01;  // mask for least significant bit

        private const string HexPrefix = "0x";

        public EthereumNetworkConfig Network { get; }
        private readonly HttpClient _httpClient;

        public EthereumClient(EthereumNetworkConfig network, HttpClient httpClient = null)
        {
            Network = network ?? throw new ArgumentNullException(nameof(network));
            _httpClient = httpClient ?? new HttpClient();
        }

        public string DeriveEthereumAddress(MasterKey masterKey, int account, int index)
        {
            ValidateInputs(Network, masterKey, account, index);
            try
            {
                using (var k = DerivationKeyFactory.CreatePrivateDerivationKeyBip44(masterKey, EthereumCoinType, account, ExternalChange, index))
                {
                    if (k.Scalar.Length != Bytes32)
                    {
                        throw new InvalidOperationException("Derived scalar must be 32 bytes.");
                    }

                    return new EthECKey(k.Scalar.ToArray(), true).GetPublicAddress();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to derive Ethereum address.");
                throw new InvalidOperationException("Failed to derive Ethereum address.", ex);
            }
        }

        public string DeriveEthereumPrivateKeyHex(MasterKey masterKey, int account, int index)
        {
            ValidateInputs(Network, masterKey, account, index);
            try
            {
                using (var k = DerivationKeyFactory.CreatePrivateDerivationKeyBip44(masterKey, EthereumCoinType, account, ExternalChange, index))
                {
                    if (k.Scalar.Length != Bytes32)
                    {
                        throw new InvalidOperationException("Derived scalar must be 32 bytes.");
                    }

                    var hex = new EthECKey(k.Scalar.ToArray(), true).GetPrivateKey();
                    if (hex.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        hex = hex.Substring(HexPrefix.Length);
                    }

                    return hex;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to derive Ethereum private key.");
                throw new InvalidOperationException("Failed to derive Ethereum private key.", ex);
            }
        }

        public async Task<string> RetrievePublicKeyAsync(string address, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentNullException(nameof(address));
            }

            var explorer = new EtherscanExplorerClient(_httpClient, Network.ExplorerApiBaseUrl, Network.ApiKey);
            var info = await explorer.GetAddressInfoAsync(address, cancellationToken).ConfigureAwait(false);
            if (info is null || info.OutgoingTransactionHashes.Count == 0)
            {
                return string.Empty;
            }

            foreach (var txHash in info.OutgoingTransactionHashes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(txHash))
                {
                    continue;
                }

                var sigInfo = await explorer.TryGetSignatureInfoAsync(txHash, cancellationToken).ConfigureAwait(false);
                if (sigInfo is null || sigInfo.MessageHash is null || sigInfo.MessageHash.Length == 0)
                {
                    continue;
                }

                try
                {
                    var v = (byte)(VParityBase + (sigInfo.RecoveryId & OneBitMask));
                    var sig = EthECDSASignatureFactory.FromComponents(sigInfo.R, sigInfo.S, new[] { v });
                    var key = EthECKey.RecoverFromSignature(sig, sigInfo.MessageHash);
                    if (key is null)
                    {
                        continue;
                    }

                    var pub = key.GetPubKey();
                    if (pub != null && (pub.Length == PubKeyRawLength || (pub.Length == PubKeyUncompressedLength && pub[0] == PubKeyUncompressedPrefix)))
                    {
                        var base32 = EncodePublicKey(pub);

                        return base32;
                    }
                }
                catch (FormatException) { }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
            }

            return string.Empty;
        }

        public bool ValidateChecksum(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            try
            {
                return AddressUtil.Current.IsChecksumAddress(address);
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }
        }

        private static void ValidateInputs(EthereumNetworkConfig config, MasterKey masterKey, int account, int index)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (masterKey is null)
            {
                throw new ArgumentNullException(nameof(masterKey));
            }

            if (account < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(account));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        internal static string EncodePublicKey(byte[] publicKey)
        {
            if (publicKey is null || publicKey.Length == 0)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            var compressed = CompressPublicKeyIfNeeded(publicKey);

            return Base32EConverter.ToEmailBase32(compressed);
        }

        private static byte[] CompressPublicKeyIfNeeded(byte[] key)
        {
            if (key.Length == PubKeyCompressedLength && (key[0] == PubKeyCompressedEvenPrefix || key[0] == PubKeyCompressedOddPrefix))
            {
                return key;
            }

            byte[] xy;
            if (key.Length == PubKeyUncompressedLength && key[0] == PubKeyUncompressedPrefix)
            {
                xy = new byte[PubKeyRawLength];
                Buffer.BlockCopy(key, PrefixByteLength, xy, 0, PubKeyRawLength);
            }
            else if (key.Length == PubKeyRawLength)
            {
                xy = key;
            }
            else
            {
                throw new ArgumentException("Unsupported public key length for compression: " + key.Length);
            }

            var x = new byte[Bytes32];
            var y = new byte[Bytes32];
            Buffer.BlockCopy(xy, 0, x, 0, Bytes32);
            Buffer.BlockCopy(xy, Bytes32, y, 0, Bytes32);
            var prefix = (byte)(((y[LastByteIndex32] & OneBitMask) == 0) ? PubKeyCompressedEvenPrefix : PubKeyCompressedOddPrefix);
            var compressed = new byte[PubKeyCompressedLength];
            compressed[0] = prefix;
            Buffer.BlockCopy(x, 0, compressed, PrefixByteLength, Bytes32);

            return compressed;
        }
    }
}
