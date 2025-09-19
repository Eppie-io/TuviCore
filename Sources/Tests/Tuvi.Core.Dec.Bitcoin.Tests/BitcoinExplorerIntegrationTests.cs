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

using NUnit.Framework;
using Tuvi.Base32EConverterLib;

namespace Tuvi.Core.Dec.Bitcoin.Tests
{
    [TestFixture]
    [Explicit("Integration tests are disabled for ci.")]
    [Category("Integration")]
    public sealed class BitcoinExplorerIntegrationTests
    {
        private static readonly HttpClient _http = new HttpClient();

        private static readonly string[] WellKnownAddresses = new[]
        {
            "1Q2TWHE3GMdB6BZKafqwxXtWAWgFt5Jvm3", // Hal Finney
            "1Adam3usQMbQWScA5AXnnDsRMeZeCh6ovu", // Adam Back
            "1XPTgDRhN8RFnzniWCddobD9iKZatrvH4", // Laszlo Hanyecz

            //"12cbQLTFMXRnSzktFkuoG3eHoMeFtpTu3S", // Satoshi Nakamoto
            //"1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa", // Satoshi Nakamoto
        };

        [Test]
        public async Task RetrievePublicKeyFromMainNetAddress()
        {
            foreach (var address in WellKnownAddresses)
            {
                string? base32 = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.MainNet, address, _http, CancellationToken.None).ConfigureAwait(false);
                if (string.IsNullOrEmpty(base32))
                {
                    TestContext.WriteLine($"No public key recovered for {address}");
                    continue;
                }

                var pubKey = Base32EConverter.FromEmailBase32(base32);
                Assert.That(pubKey.Length, Is.EqualTo(33));
                Assert.Pass($"Successfully retrieved public key for {address}. Base32E={base32}");
            }

            Assert.Inconclusive("No public key retrieved for any address.");
        }
    }
}
