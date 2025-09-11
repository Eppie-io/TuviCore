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
using Tuvi.Core.Dec.Ethereum.Explorer;

namespace Tuvi.Core.Dec.Ethereum.Tests
{
    [TestFixture]
    public sealed class EtherscanClientSignatureNegativeTests
    {
        private sealed class StaticHandler : HttpMessageHandler
        {
            private readonly string _json;
            public StaticHandler(string json) { _json = json; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_json) });
            }
        }

        [Test]
        public async Task TryGetSignatureInfoAsyncUnknownTypedReturnsNull()
        {
            const string Json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"type\":\"0x3\"}}";
            using var handler = new StaticHandler(Json);
            using var http = new HttpClient(handler, disposeHandler: true);
            var client = new EtherscanExplorerClient(http, EthereumNetworkConfig.MainNet.ExplorerApiBaseUrl);

            var sig = await client.TryGetSignatureInfoAsync("0x00", CancellationToken.None).ConfigureAwait(false);
            Assert.That(sig, Is.Null);
        }

        [Test]
        public async Task TryGetSignatureInfoAsyncMissingResultReturnsNull()
        {
            const string Json = "{\"jsonrpc\":\"2.0\",\"id\":1}"; // no result
            using var handler = new StaticHandler(Json);
            using var http = new HttpClient(handler, disposeHandler: true);
            var client = new EtherscanExplorerClient(http, EthereumNetworkConfig.MainNet.ExplorerApiBaseUrl);

            var sig = await client.TryGetSignatureInfoAsync("0x00", CancellationToken.None).ConfigureAwait(false);
            Assert.That(sig, Is.Null);
        }
    }
}
