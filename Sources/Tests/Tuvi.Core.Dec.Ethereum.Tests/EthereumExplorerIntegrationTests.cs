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

using Nethereum.Signer;
using Nethereum.Util;
using NUnit.Framework;
using Tuvi.Core.Dec.Ethereum.Explorer;

namespace Tuvi.Core.Dec.Ethereum.Tests
{
    [TestFixture]
    [Category("Integration")]
    public sealed class EthereumExplorerIntegrationTests
    {
        // Network/clients
        private static readonly HttpClient _httpClient = new HttpClient();
        private string _apiKey = string.Empty;

        // Addresses and transaction samples
        private const string ActiveAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";

        // Known typed (0x02) transaction sample
        private const string SampleTxHash = "0xb3e9f5f24182d9c865f08dd295ff9d159498889a20247d337e37af634d1bc9af";
        private const string SamplePubUncompressed = "04e95ba0b752d75197a8bad8d2e6ed4b9eb60a1e8b08d257927d0df4f3ea6860992aac5e614a83f1ebe4019300373591268da38871df019f694f8e3190e493e711";
        private const string SampleAddress = ActiveAddress; // same owner

        // Legacy (pre-EIP-1559)
        private const string LegacyTxHash = "0x87a3bc85da972583e22da329aa109ea0db57c54a2eee359b2ed12597f5cb1a64";
        private const string LegacyTxFromAddress = "0X3C02CEBB49F6E8F1FC96158099FFA064BBFEE38B";

        // EIP-2930 (type 1)
        private const string Type1TxHash = "0xf05278df87df8ae63a27f4a509097a033dea55d55fbba848da029fc867ebdbbc";
        private const string Type1TxFromAddress = "0X264BD8291FAE1D75DB2C5F573B07FAA6715997B5";

        // Simple process-wide gate to avoid hitting rate limits when tests run concurrently
        private static readonly SemaphoreSlim RateGate = new SemaphoreSlim(1, 1);
        private static DateTime _lastCallUtc = DateTime.MinValue;
        private static async Task RateLimitDelayAsync()
        {
            await RateGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var since = DateTime.UtcNow - _lastCallUtc;
                var minGap = TimeSpan.FromMilliseconds(1100);
                if (since < minGap)
                {
                    await Task.Delay(minGap - since).ConfigureAwait(false);
                }
                _lastCallUtc = DateTime.UtcNow;
            }
            finally
            {
                RateGate.Release();
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _apiKey = ""; //ETHERSCAN_API_KEY
        }

        private void RequireApiKeyOptional()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                Assert.Inconclusive("ETHERSCAN_API_KEY not set");
            }
        }

        // ---------------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------------

        [Test]
        [Explicit("Hits external Etherscan API for typed transaction signature and key recovery")]
        public async Task RetrieveSignatureAndRecoverPublicKey()
        {
            RequireApiKeyOptional();
            var client = CreateExplorerClient();

            await RateLimitDelayAsync().ConfigureAwait(false);
            var sigInfo = await client.TryGetSignatureInfoAsync(SampleTxHash, CancellationToken.None).ConfigureAwait(false);
            Assert.That(sigInfo, Is.Not.Null, "Signature info null");
            Assert.That(sigInfo!.MessageHash, Is.Not.Null.And.Not.Empty, "Message hash empty");
            Assert.That(sigInfo.R, Is.Not.Null.And.Not.Empty, "R empty");
            Assert.That(sigInfo.S, Is.Not.Null.And.Not.Empty, "S empty");

            var v = (byte)(27 + (sigInfo.RecoveryId & 0x01));
            var sig = EthECDSASignatureFactory.FromComponents(sigInfo.R, sigInfo.S, new[] { v });
            var key = EthECKey.RecoverFromSignature(sig, sigInfo.MessageHash);
            Assert.That(key, Is.Not.Null, "Recovered key is null");

            var pub = key!.GetPubKey();
            Assert.That(pub, Is.Not.Null.And.Not.Empty, "Recovered pubkey empty");

            var uncompressedHex = ToUncompressedHex(pub);
            Assert.That(uncompressedHex, Is.EqualTo(SamplePubUncompressed).IgnoreCase, "Uncompressed pubkey mismatch");

            var xy = ExtractXY(pub);
            Assert.That(xy.Length, Is.EqualTo(64), "XY length invalid");
            var addr = AddressFromXY(xy);
            Assert.That(addr, Is.EqualTo(SampleAddress.ToUpperInvariant()), "Address mismatch");
        }

        [Test]
        [Explicit("Hits external Etherscan API")]
        public async Task GetTransactionsReturnsList()
        {
            RequireApiKeyOptional();
            var client = CreateExplorerClient();

            await RateLimitDelayAsync().ConfigureAwait(false);
            var info = await client.GetAddressInfoAsync(ActiveAddress, CancellationToken.None).ConfigureAwait(false);
            Assert.That(info, Is.Not.Null, "AddressInfo null");
            Assert.That(info.OutgoingTransactionHashes.Count, Is.Positive, "No transactions returned for active address");
        }

        [Test]
        [Explicit("Hits external Etherscan API")]
        public async Task PublicKeyExtractorDoesNotThrow()
        {
            RequireApiKeyOptional();
            var decClient = EthereumClientFactory.Create(EthereumNetwork.MainNet, _httpClient);
            var decClientWithKey = EthereumClientFactory.Create(decClient.Network.WithApiKey(_apiKey), _httpClient);

            await RateLimitDelayAsync().ConfigureAwait(false);
            var pk = await decClientWithKey.RetrievePublicKeyAsync(ActiveAddress, CancellationToken.None).ConfigureAwait(false);
            Assert.Pass("Extractor executed (pk length=" + (pk == null ? 0 : pk.Length) + ")");
        }

        [Test]
        [Explicit("Hits external Etherscan API")]
        public async Task RetrievePublicKeyApiReturnsExpectedAddress()
        {
            RequireApiKeyOptional();
            var decClient = EthereumClientFactory.Create(EthereumNetwork.MainNet, _httpClient);
            var decClientWithKey = EthereumClientFactory.Create(decClient.Network.WithApiKey(_apiKey), _httpClient);

            await RateLimitDelayAsync().ConfigureAwait(false);
            var base32 = await decClientWithKey.RetrievePublicKeyAsync(ActiveAddress, CancellationToken.None).ConfigureAwait(false);
            Assert.That(base32, Is.Not.Null.And.Not.Empty, "Base32 public key empty");

            byte[] keyBytes;
            try
            {
                keyBytes = Tuvi.Base32EConverterLib.Base32EConverter.FromEmailBase32(base32);
            }
            catch (FormatException ex)
            {
                Assert.Fail("Failed to decode Base32 public key: " + ex.Message); return;
            }
            catch (ArgumentException ex)
            {
                Assert.Fail("Failed to decode Base32 public key: " + ex.Message); return;
            }

            var uncompressed = EnsureUncompressedKey(keyBytes);
            var uncompressedHex = BytesToHex(uncompressed, prefixed: false);
            Assert.That(uncompressedHex, Is.EqualTo(SamplePubUncompressed).IgnoreCase, "Uncompressed key mismatch");

            var xy = new byte[64];
            Buffer.BlockCopy(uncompressed, 1, xy, 0, 64);
            var addr = AddressFromXY(xy);
            Assert.That(addr, Is.EqualTo(SampleAddress.ToUpperInvariant()), "Recovered address mismatch from API public key");
        }

        [Test]
        [Explicit("Hits external Etherscan API")]
        public async Task RetrievePublicKeyApiMatchesKnownUncompressed()
        {
            RequireApiKeyOptional();
            var decClient = EthereumClientFactory.Create(EthereumNetwork.MainNet, _httpClient);
            var decClientWithKey = EthereumClientFactory.Create(decClient.Network.WithApiKey(_apiKey), _httpClient);

            await RateLimitDelayAsync().ConfigureAwait(false);
            var base32 = await decClientWithKey.RetrievePublicKeyAsync(ActiveAddress, CancellationToken.None).ConfigureAwait(false);
            Assert.That(base32, Is.Not.Null.And.Not.Empty, "Base32 public key empty");

            byte[] keyBytes;
            try
            {
                keyBytes = Tuvi.Base32EConverterLib.Base32EConverter.FromEmailBase32(base32);
            }
            catch (FormatException ex)
            {
                Assert.Fail("Failed to decode Base32 public key: " + ex.Message); return;
            }
            catch (ArgumentException ex)
            {
                Assert.Fail("Failed to decode Base32 public key: " + ex.Message); return;
            }

            var uncompressed = EnsureUncompressedKey(keyBytes);
            var uncompressedHex = BytesToHex(uncompressed, prefixed: false);
            Assert.That(uncompressedHex, Is.EqualTo(SamplePubUncompressed).IgnoreCase, "Uncompressed key mismatch (direct retrieval test)");
        }

        [Test]
        [Explicit("Hits external Etherscan API for legacy transaction signature and key recovery")]
        public async Task RetrieveLegacySignatureAndRecoverPublicKey()
        {
            RequireApiKeyOptional();
            var client = CreateExplorerClient();

            await RateLimitDelayAsync().ConfigureAwait(false);
            var sigInfo = await client.TryGetSignatureInfoAsync(LegacyTxHash, CancellationToken.None).ConfigureAwait(false);
            Assert.That(sigInfo, Is.Not.Null, "Signature info null");
            Assert.That(sigInfo!.MessageHash, Is.Not.Null.And.Not.Empty, "Message hash empty");
            Assert.That(sigInfo.R, Is.Not.Null.And.Not.Empty, "R empty");
            Assert.That(sigInfo.S, Is.Not.Null.And.Not.Empty, "S empty");

            var v = (byte)(27 + (sigInfo.RecoveryId & 0x01));
            var sig = EthECDSASignatureFactory.FromComponents(sigInfo.R, sigInfo.S, new[] { v });
            var key = EthECKey.RecoverFromSignature(sig, sigInfo.MessageHash);
            Assert.That(key, Is.Not.Null, "Recovered key is null");

            var pub = key!.GetPubKey();
            Assert.That(pub, Is.Not.Null.And.Not.Empty, "Recovered pubkey empty");

            var xy = ExtractXY(pub);
            Assert.That(xy.Length, Is.EqualTo(64), "XY length invalid");
            var addr = AddressFromXY(xy);
            Assert.That(addr, Is.EqualTo(LegacyTxFromAddress), "Address mismatch (legacy)");
        }

        [Test]
        [Explicit("Hits external Etherscan API for EIP-2930 (type 1) transaction signature and key recovery")]
        public async Task RetrieveType1SignatureAndRecoverPublicKey()
        {
            RequireApiKeyOptional();
            var client = CreateExplorerClient();

            await RateLimitDelayAsync().ConfigureAwait(false);
            var sigInfo = await client.TryGetSignatureInfoAsync(Type1TxHash, CancellationToken.None).ConfigureAwait(false);
            Assert.That(sigInfo, Is.Not.Null, "Signature info null");
            Assert.That(sigInfo!.MessageHash, Is.Not.Null.And.Not.Empty, "Message hash empty");
            Assert.That(sigInfo.R, Is.Not.Null.And.Not.Empty, "R empty");
            Assert.That(sigInfo.S, Is.Not.Null.And.Not.Empty, "S empty");

            var v = (byte)(27 + (sigInfo.RecoveryId & 0x01));
            var sig = EthECDSASignatureFactory.FromComponents(sigInfo.R, sigInfo.S, new[] { v });
            var key = EthECKey.RecoverFromSignature(sig, sigInfo.MessageHash);
            Assert.That(key, Is.Not.Null, "Recovered key is null");

            var pub = key!.GetPubKey();
            Assert.That(pub, Is.Not.Null.And.Not.Empty, "Recovered pubkey empty");

            var xy = ExtractXY(pub);
            Assert.That(xy.Length, Is.EqualTo(64), "XY length invalid");
            var addr = AddressFromXY(xy);
            Assert.That(addr, Is.EqualTo(Type1TxFromAddress), "Address mismatch (type 1)");
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private EtherscanExplorerClient CreateExplorerClient()
            => new(_httpClient, EthereumNetworkConfig.MainNet.ExplorerApiBaseUrl, _apiKey);

        private static string ToUncompressedHex(byte[] pub)
        {
            // Produce a 65-byte uncompressed representation (0x04 || X || Y) and print as HEX
            if (pub == null || pub.Length == 0)
            {
                return string.Empty;
            }

            if (pub.Length == 64)
            {
                var withPrefix = new byte[65];
                withPrefix[0] = 0x04;
                Buffer.BlockCopy(pub, 0, withPrefix, 1, 64);

                return BytesToHex(withPrefix, prefixed: false);
            }

            if (pub.Length == 65)
            {
                return BytesToHex(pub, prefixed: false);
            }

            return string.Empty;
        }

        private static byte[] ExtractXY(byte[] pub)
        {
            if (pub.Length == 65)
            {
                var xy = new byte[64];
                Buffer.BlockCopy(pub, 1, xy, 0, 64);

                return xy;
            }

            if (pub.Length == 64)
            {
                return pub;
            }

            return Array.Empty<byte>();
        }

        private static string AddressFromXY(byte[] xy)
        {
            var hash = Sha3Keccack.Current.CalculateHash(xy);

            return ("0x" + BitConverter.ToString(hash, 12, 20).Replace("-", string.Empty, StringComparison.Ordinal)).ToUpperInvariant();
        }

        private static string BytesToHex(byte[] bytes, bool prefixed)
        {
            var hex = BitConverter.ToString(bytes).Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

            return prefixed ? ("0x" + hex) : hex;
        }

        private static byte[] EnsureUncompressedKey(byte[] keyBytes)
        {
            if (keyBytes.Length == 33 && (keyBytes[0] == 0x02 || keyBytes[0] == 0x03))
            {
                return DecompressSecp256k1(keyBytes);
            }

            if (keyBytes.Length == 65 && keyBytes[0] == 0x04)
            {
                return keyBytes;
            }

            if (keyBytes.Length == 64)
            {
                var uncompressed = new byte[65];
                uncompressed[0] = 0x04;
                Buffer.BlockCopy(keyBytes, 0, uncompressed, 1, 64);

                return uncompressed;
            }

            throw new InvalidOperationException("Unexpected recovered key length: " + keyBytes.Length);
        }

        private static byte[] DecompressSecp256k1(byte[] compressed)
        {
            if (compressed.Length != 33)
            {
                throw new ArgumentException("Invalid compressed key length");
            }

            byte prefix = compressed[0];
            if (prefix != 0x02 && prefix != 0x03)
            {
                throw new ArgumentException("Invalid compressed key prefix");
            }

            try
            {
                var parms = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1") ?? throw new InvalidOperationException("Curve parameters not found");
                var point = parms.Curve.DecodePoint(compressed);
                var bx = point.XCoord.ToBigInteger().ToByteArrayUnsigned();
                var by = point.YCoord.ToBigInteger().ToByteArrayUnsigned();

                if (bx.Length != 32)
                {
                    bx = PadLeft32(bx);
                }

                if (by.Length != 32)
                {
                    by = PadLeft32(by);
                }

                var uncompressed = new byte[65];
                uncompressed[0] = 0x04;
                Buffer.BlockCopy(bx, 0, uncompressed, 1, 32);
                Buffer.BlockCopy(by, 0, uncompressed, 33, 32);

                return uncompressed;
            }
            catch (Exception ex) when (ex is System.IO.FileNotFoundException || ex is TypeLoadException || ex is System.MissingMethodException || ex is Org.BouncyCastle.Crypto.CryptoException || ex is FormatException)
            {
                throw new InvalidOperationException("Failed to decompress secp256k1 key via BouncyCastle (manual math fallback removed).", ex);
            }
        }

        private static byte[] PadLeft32(byte[] src)
        {
            if (src.Length == 32)
            {
                return src;
            }

            if (src.Length > 32)
            {
                var trimmed = new byte[32];
                Buffer.BlockCopy(src, src.Length - 32, trimmed, 0, 32);

                return trimmed;
            }

            var dst = new byte[32];
            Buffer.BlockCopy(src, 0, dst, 32 - src.Length, src.Length);

            return dst;
        }
    }
}
