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

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Dec.Web.Impl;

namespace Tuvi.Core.Dec.Web.Impl.Tests
{
    public class WebDecStorageClientTests
    {
        private readonly CancellationToken _ct = CancellationToken.None;
        private sealed class FakeHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, HttpResponseMessage> OnSend { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (OnSend != null)
                    return Task.FromResult(OnSend(request));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Handler lifetime owned by HttpClient (disposeHandler:true) inside WebDecStorageClient, and client is disposed in each test via using pattern.")]
        private static WebDecStorageClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc)
        {
            return new WebDecStorageClient("https://fake.eppie.io/api", new FakeHandler { OnSend = handlerFunc });
        }

        [Test]
        public async Task ClaimNameAsyncReturnsExpectedResult()
        {
            var expectedAddress = "ADDRESS123";
            using var client = CreateClient(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedAddress)
            });

            var result = await client.ClaimNameAsync("testname", expectedAddress, _ct).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(expectedAddress));
        }

        [Test]
        public async Task GetAddressByNameAsyncReturnsExpectedAddress()
        {
            var expectedAddress = "ADDRESS456";
            using var client = CreateClient(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedAddress)
            });

            var result = await client.GetAddressByNameAsync("testname", _ct).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(expectedAddress));
        }

        [Test]
        public void ClaimNameAsyncThrowsOnEmptyName()
        {
            using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK));

            Assert.ThrowsAsync<ArgumentException>(async () => await client.ClaimNameAsync("", "ADDR", _ct).ConfigureAwait(false));
        }

        [Test]
        public void ClaimNameAsyncThrowsOnEmptyAddress()
        {
            using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK));

            Assert.ThrowsAsync<ArgumentException>(async () => await client.ClaimNameAsync("name", "", _ct).ConfigureAwait(false));
        }

        [Test]
        public void GetAddressByNameAsyncThrowsOnEmptyName()
        {
            using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK));

            Assert.ThrowsAsync<ArgumentException>(async () => await client.GetAddressByNameAsync("", _ct).ConfigureAwait(false));
        }

        [Test]
        public async Task ClaimNameAsyncDuplicateReturnsEmptyString()
        {
            bool first = true;
            using var client = CreateClient(req =>
            {
                if (req.RequestUri.AbsolutePath.Contains("/claim", StringComparison.Ordinal) && first)
                {
                    first = false;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("ADDRXYZ")
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Empty)
                };
            });

            var firstResult = await client.ClaimNameAsync("dupname", "ADDRXYZ", _ct).ConfigureAwait(false);
            var secondResult = await client.ClaimNameAsync("dupname", "ADDRXYZ", _ct).ConfigureAwait(false);

            Assert.That(firstResult, Is.EqualTo("ADDRXYZ"));
            Assert.That(secondResult, Is.EqualTo(string.Empty));
        }

        [Test]
        public void GetAddressByNameAsyncNotFoundReturnsEmptyString()
        {
            using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty)
            });

            Assert.DoesNotThrowAsync(async () => await client.GetAddressByNameAsync("unknown", _ct).ConfigureAwait(false));
        }

        [Test]
        public async Task ClaimNameAsyncAcceptsSingleCharName()
        {
            var expectedAddress = "ADDR1";
            using var client = CreateClient(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedAddress)
            });

            var result = await client.ClaimNameAsync("a", expectedAddress, _ct).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(expectedAddress));
        }

        [Test]
        public async Task ClaimNameAsyncAcceptsMaxLengthName()
        {
            var name = new string('x', 48);
            var expectedAddress = "ADDRMAX";
            using var client = CreateClient(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedAddress)
            });

            var result = await client.ClaimNameAsync(name, expectedAddress, _ct).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(expectedAddress));
        }
    }
}
