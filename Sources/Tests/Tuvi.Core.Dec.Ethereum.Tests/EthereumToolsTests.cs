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

using KeyDerivation.Keys;
using KeyDerivationLib;
using NUnit.Framework;

namespace Tuvi.Core.Dec.Ethereum.Tests
{
    [TestFixture]
    public sealed class EthereumToolsTests : IDisposable
    {
        private static readonly string[] TestSeedPhrase = new[]
        {
            "apple","apple","apple","apple","apple","apple",
            "apple","apple","apple","apple","apple","apple"
        };

        private MasterKey? _masterKey; // initialized in SetUp
        private IEthereumClient _client = null!;
        private readonly HttpClient _httpClient = new HttpClient();
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _httpClient.Dispose();
            _masterKey?.Dispose();

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        [SetUp]
        public void Setup()
        {
            var factory = new MasterKeyFactory(new ImplementationDetailsProvider("Tuvi seed", "Tuvi.Package", "backup@test"));
            factory.RestoreSeedPhrase(TestSeedPhrase);
            _masterKey = factory.GetMasterKey();
            _client = EthereumClientFactory.Create(EthereumNetwork.MainNet, _httpClient);
        }


        [OneTimeTearDown]
        public void OneTimeTearDown() => Dispose();

        [Test]
        public void DeriveEthereumAddressNotNull()
        {
            var addr = _client.DeriveEthereumAddress(_masterKey!, 0, 0);
            Assert.That(addr, Is.Not.Null.And.Not.Empty);
            Assert.That(addr.StartsWith("0x", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void DeriveEthereumPrivateKeyNotNull()
        {
            var pk = _client.DeriveEthereumPrivateKeyHex(_masterKey!, 0, 0);
            Assert.That(pk, Is.Not.Null.And.Not.Empty);
            Assert.That(pk.Length, Is.EqualTo(64));
            Assert.That(pk, Does.Match("^[0-9a-fA-F]{64}$"));
        }

        [Test]
        public void ValidateChecksumWorks()
        {
            var addr = _client.DeriveEthereumAddress(_masterKey!, 0, 0);
            Assert.That(_client.ValidateChecksum(addr), Is.True);
        }

        [Test]
        public void DeterministicAddressSameInputs()
        {
            var a1 = _client.DeriveEthereumAddress(_masterKey!, 2, 7);
            var a2 = _client.DeriveEthereumAddress(_masterKey!, 2, 7);
            Assert.That(a1, Is.EqualTo(a2));
        }

        [Test]
        public void DifferentIndicesProduceDifferentAddresses()
        {
            var a1 = _client.DeriveEthereumAddress(_masterKey!, 0, 0);
            var a2 = _client.DeriveEthereumAddress(_masterKey!, 0, 1);
            Assert.That(a1, Is.Not.EqualTo(a2));
        }

        [Test]
        public void PrivateKeysDifferAcrossIndices()
        {
            var k0 = _client.DeriveEthereumPrivateKeyHex(_masterKey!, 0, 0);
            var k1 = _client.DeriveEthereumPrivateKeyHex(_masterKey!, 0, 1);
            Assert.That(k0, Is.Not.EqualTo(k1));
        }

        [Test]
        public void ValidateChecksumFalseForUppercase()
        {
            var addr = _client.DeriveEthereumAddress(_masterKey!, 0, 0);
            var upper = addr.ToUpperInvariant();
            Assume.That(upper, Is.Not.EqualTo(addr));
            Assert.That(_client.ValidateChecksum(upper), Is.False);
        }
    }
}
