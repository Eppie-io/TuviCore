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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tuvi.Core.Dec.Azure.Tests
{
    [Ignore("Integration tests for the WebDecStorageClient are disabled for ci.")]
    public class WebDecStorageTests
    {
        private IDecStorageClient Client;

        private readonly CancellationToken _ct = CancellationToken.None;
        private readonly byte[] ByteData = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x1A, 0x1B, 0x1C, 0x1D, 0x1F, 0x20, 0x21, 0x22, 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x1A, 0x1B, 0x1C, 0x1D, 0x1F, 0x20, 0x21, 0x22 };
        private const string Address = "3C805EA90F9D0D54EC907B040B47DD";
        private const string DataHash = "A81F9D5B6713C805EA90F9D0D54EC907B040B47DD482A47222EB9B9D1A7D32AA";
        //private Uri Url = new Uri("http://localhost:7071/api");
        private Uri Url = new Uri("https://testnet2.eppie.io/api");

        [SetUp]
        public void Setup()
        {
            Client = DecStorageBuilder.CreateWebClient(Url);
        }

        [TearDown]
        public void Teardown()
        {
            Client.Dispose();
        }

        [Test]
        public async Task SendFunctionTest()
        {
            var hash = await Client.PutAsync(ByteData, _ct).ConfigureAwait(false);


            Assert.That(hash == DataHash, Is.True);
        }

        [Test]
        public async Task ListFunctionTest()
        {
            var hash = await Client.PutAsync(ByteData, _ct).ConfigureAwait(false);
            await Client.SendAsync(Address, hash, _ct).ConfigureAwait(false);
            var list = await Client.ListAsync(Address, _ct).ConfigureAwait(false);


            Assert.That(hash == DataHash, Is.True);
            Assert.That(list.Contains(hash), Is.True);
        }

        [Test]
        public async Task GetFunctionTest()
        {
            var hash = await Client.PutAsync(ByteData, _ct).ConfigureAwait(false);
            var data = await Client.GetAsync(hash, _ct).ConfigureAwait(false);


            Assert.That(hash == DataHash, Is.True);
            Assert.That(data.SequenceEqual(ByteData), Is.True);
        }

        [Test]
        public async Task ClaimNameFunctionTest()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            var name = string.Concat("testname", guidStr.AsSpan(0, 8));
            var claimResult = await Client.ClaimNameAsync(name, Address, _ct).ConfigureAwait(false);


            Assert.That(!string.IsNullOrWhiteSpace(claimResult), Is.True);
            Assert.That(claimResult, Is.EqualTo(Address));
        }

        [Test]
        public async Task GetAddressByNameFunctionTest()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            var name = string.Concat("testkey", guidStr.AsSpan(0, 8));
            var claimResult = await Client.ClaimNameAsync(name, Address, _ct).ConfigureAwait(false);
            var resolved = await Client.GetAddressByNameAsync(name, _ct).ConfigureAwait(false);


            Assert.That(claimResult, Is.EqualTo(Address));
            Assert.That(resolved, Is.EqualTo(Address));
        }

        [Test]
        public async Task ClaimNameAlreadyTakenTest()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            var name = string.Concat("testtakenname", guidStr.AsSpan(0, 4));
            var firstClaim = await Client.ClaimNameAsync(name, Address, _ct).ConfigureAwait(false);
            var secondClaim = await Client.ClaimNameAsync(name, Address, _ct).ConfigureAwait(false);
            var secondClaimUpper = secondClaim?.ToUpperInvariant();


            Assert.That(firstClaim, Is.EqualTo(Address));
            Assert.That(string.IsNullOrWhiteSpace(secondClaim) || (secondClaimUpper != null && secondClaimUpper.Contains("ALREADY", StringComparison.Ordinal)), Is.True);
        }
    }
}
