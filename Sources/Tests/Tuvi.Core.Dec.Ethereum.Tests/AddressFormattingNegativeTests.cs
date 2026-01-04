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

using NUnit.Framework;

namespace Tuvi.Core.Dec.Ethereum.Tests
{
    [TestFixture]
    public sealed class AddressFormattingNegativeTests
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private IEthereumClient _client = null!;

        [SetUp]
        public void SetUp()
        {
            _client = EthereumClientFactory.Create(EthereumNetwork.MainNet, _httpClient);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ValidateChecksumNullOrEmptyReturnsFalse(string? value)
        {
            Assert.That(_client.ValidateChecksum(value!), Is.False);
        }

        [Test]
        public void ValidateChecksumInvalidCharactersReturnsFalse()
        {
            Assert.That(_client.ValidateChecksum("0xZZZ"), Is.False);
        }
    }
}
