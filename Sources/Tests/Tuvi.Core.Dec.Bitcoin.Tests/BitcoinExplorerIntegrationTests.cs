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

using System.Text;
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
            "12cbQLTFMXRnSzktFkuoG3eHoMeFtpTu3S", // Satoshi Nakamoto
            //"1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa", // Satoshi Nakamoto (Coinbase Genesis)
        };

        private const string addressTestnet4 = "myzVrn9mTEmfprDx8BmqFiZRLzkxWhLCF4";
        private const string addressTestnet4wif = "cW7LPrwViphKRyRKfY3symdZKGxkAd7Xd2gHSRVsXzUssVDv5Cvo";

        [Test]
        public async Task RetrievePublicKeyFromMainNetAddress()
        {
            var failures = new List<string>();
            var successes = new List<string>();

            foreach (var address in WellKnownAddresses)
            {
                string base32 = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.MainNet, address, _http, CancellationToken.None).ConfigureAwait(false);
                if (string.IsNullOrEmpty(base32))
                {
                    failures.Add(address + " => no public key");
                    continue;
                }

                var pubKey = Base32EConverter.FromEmailBase32(base32);
                if (pubKey.Length != 33)
                {
                    failures.Add(address + " => invalid length " + pubKey.Length);
                }
                else
                {
                    successes.Add(address + " => " + base32);
                }
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Some addresses failed:");
                foreach (var f in failures) sb.AppendLine(" - " + f);
                sb.AppendLine("Successful:");
                foreach (var s in successes) sb.AppendLine(" + " + s);
                Assert.Fail(sb.ToString());
            }

            Assert.Pass("All addresses succeeded. " + string.Join(", ", successes));
        }

        [Test]
        public async Task RetrievePublicKeyFromTestNet4Address()
        {
            var address = addressTestnet4;

            string base32 = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, address, _http, CancellationToken.None).ConfigureAwait(false);
            Assert.That(base32, Is.Not.Null, "Public key should not be null or empty.");

            var pubKey = Base32EConverter.FromEmailBase32(base32);
            Assert.That(pubKey.Length, Is.EqualTo(33), "Public key length should be 33 bytes.");
        }

        [Test]
        public async Task BuildAndSignAndSendSpendAllTransaction()
        {
            const string address = addressTestnet4;

            // Build and sign transaction
            string txHex = await BitcoinToolsImpl.BuildAndSignSpendAllToSameAddressTransactionAsync(BitcoinNetworkConfig.TestNet4, address, addressTestnet4wif, _http, cancellation: CancellationToken.None).ConfigureAwait(false);
            Assert.That(txHex, Is.Not.Null.And.Not.WhiteSpace, "Failed to build and sign transaction (tx hex is null or empty).");

            TestContext.WriteLine($"Built transaction hex: {txHex}");

            // Broadcast
            string txid = await BitcoinToolsImpl.BroadcastTransactionAsync(BitcoinNetworkConfig.TestNet4, txHex, _http, CancellationToken.None).ConfigureAwait(false);
            Assert.That(txid, Is.Not.Null.And.Not.WhiteSpace, "Failed to broadcast transaction (no txid returned).");

            TestContext.WriteLine($"Transaction broadcasted. Returned txid: {txid}");
            Assert.Pass($"Transaction built, signed and broadcasted. TxId: {txid}");
        }
    }
}
