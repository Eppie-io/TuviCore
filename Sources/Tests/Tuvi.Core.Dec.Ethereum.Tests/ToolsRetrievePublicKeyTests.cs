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

using System.Net;
using NUnit.Framework;

namespace Tuvi.Core.Dec.Ethereum.Tests
{
    [TestFixture]
    public sealed class ToolsRetrievePublicKeyTests
    {
        private sealed class RouterHandler : HttpMessageHandler
        {
            private readonly string _address;
            private int _pageCalls;
            private readonly string _sigJson;
            public RouterHandler(string address, string sigJson)
            {
                _address = address;
                _sigJson = sigJson;
            }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri?.ToString() ?? string.Empty;
                if (url.Contains("action=txlist", StringComparison.OrdinalIgnoreCase))
                {
                    _pageCalls++;
                    var list = _pageCalls == 1
                        ? $"{{\"status\":\"1\",\"message\":\"OK\",\"result\":[{{\"hash\":\"0xdead\",\"from\":\"{_address}\"}},{{\"hash\":\"0xbeef\",\"from\":\"{_address}\"}}]}}"
                        : "{\"status\":\"1\",\"message\":\"OK\",\"result\":[]}";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(list) });
                }
                if (url.Contains("eth_getTransactionByHash", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_sigJson) });
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
            }
        }

        [Test]
        public async Task RetrievePublicKeyInternalAsyncReturnsBase32OnFirstValidTx()
        {
            const string Address = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
            const string SigJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"type\":\"0x2\",\"chainId\":\"0x1\",\"nonce\":\"0x1\",\"gas\":\"0x5208\",\"maxFeePerGas\":\"0x3b9aca00\",\"maxPriorityFeePerGas\":\"0x3b9aca00\",\"to\":\"0x0000000000000000000000000000000000000001\",\"value\":\"0x0\",\"input\":\"0x\",\"v\":\"0x0\",\"r\":\"0x4ea77ebbdbc2194a8603b1d8e13a1a50a871a50d3155ee31ed74f7a320a98dab\",\"s\":\"0x23ec6ee5ce4a69ed11161570d6c464c1454eb016adb90d7e0aea772d319c3038\"}}";

            using var handler = new RouterHandler(Address, SigJson);
            using var http = new HttpClient(handler, disposeHandler: true);

            var base32 = await EthereumClientFactory.Create(EthereumNetwork.MainNet, http).RetrievePublicKeyAsync(Address, CancellationToken.None).ConfigureAwait(false);
            Assert.That(base32, Is.Not.Null.And.Not.Empty);

            // Validate it decodes to 33 bytes
            var decoded = Tuvi.Base32EConverterLib.Base32EConverter.FromEmailBase32(base32);
            Assert.That(decoded.Length, Is.EqualTo(33));
        }

        [Test]
        public async Task RetrievePublicKeyInternalAsyncSkipsInvalidAndReturnsEmpty()
        {
            const string Address = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
            const string SigBad = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"type\":\"0x2\",\"chainId\":\"0x1\"}}"; // missing fields

            using var handler = new RouterHandler(Address, SigBad);
            using var http = new HttpClient(handler, disposeHandler: true);

            var base32 = await EthereumClientFactory.Create(EthereumNetwork.MainNet, http).RetrievePublicKeyAsync(Address, CancellationToken.None).ConfigureAwait(false);
            Assert.That(base32, Is.Empty);
        }
    }
}
