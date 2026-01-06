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

using System.Net;
using NUnit.Framework;
using Tuvi.Core.Dec.Ethereum.Explorer;

namespace Tuvi.Core.Dec.Ethereum.Tests
{
    [TestFixture]
    public sealed class EtherscanClientPaginationTests
    {
        // Simulates Etherscan API for txlist with one rate-limit response followed by a page with two same-hash entries
        private sealed class RouterHandler : HttpMessageHandler
        {
            private int _txlistCalls;
            private readonly string _address;

            public RouterHandler(string address) => _address = address;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var uri = request.RequestUri?.ToString() ?? string.Empty;
                if (uri.Contains("action=txlist", StringComparison.OrdinalIgnoreCase))
                {
                    _txlistCalls++;
                    if (_txlistCalls == 1)
                    {
                        var rateLimited = "{\"status\":\"0\",\"message\":\"rate limit\",\"result\":[]}";
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(rateLimited)
                        });
                    }

                    var ok = "{\"status\":\"1\",\"message\":\"OK\",\"result\":[{\"hash\":\"0xabc\",\"from\":\"" + _address + "\"},{\"hash\":\"0xABC\",\"from\":\"" + _address + "\"}]}";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(ok)
                    });
                }

                // default empty object
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                });
            }
        }

        [Test]
        public async Task GetAddressInfoAsyncHandlesRateLimitAndDeduplication()
        {
            const string Address = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
            using var handler = new RouterHandler(Address);
            using var clientHttp = new HttpClient(handler, disposeHandler: true);
            var client = new EtherscanExplorerClient(clientHttp, EthereumNetworkConfig.MainNet.ExplorerApiBaseUrl, string.Empty);

            var info = await client.GetAddressInfoAsync(Address, CancellationToken.None).ConfigureAwait(false);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.OutgoingTransactionHashes.Count, Is.EqualTo(1));
            Assert.That(info.OutgoingTransactionHashes[0], Is.EqualTo("0xabc").IgnoreCase);
        }
    }
}
