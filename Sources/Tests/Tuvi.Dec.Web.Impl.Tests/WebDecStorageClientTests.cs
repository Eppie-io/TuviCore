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
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Dec.Names;

namespace Tuvi.Core.Dec.Web.Impl.Tests
{
    public class WebDecStorageClientTests
    {
        private readonly CancellationToken _ct = CancellationToken.None;

        private static (string PublicKeyBase32E, string SignatureBase64) CreateValidSignature(string name)
        {
            return ClaimV1TestKeys.CreateSignature(name);
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, HttpResponseMessage> OnSend { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (OnSend != null)
                {
                    return Task.FromResult(OnSend(request));
                }

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
            var name = "testname";
            var (expectedPublicKey, signature) = CreateValidSignature(name);

            using var client = CreateClient(req =>
            {
                Assert.That(req.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(req.RequestUri.AbsolutePath, Does.Contain("/claim"));

                var json = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                Assert.That(json, Does.Contain("\"NameCanonical\""));
                Assert.That(json, Does.Contain(NameClaim.CanonicalizeName(name)));

                Assert.That(json, Does.Contain("\"PublicKey\""));
                Assert.That(json, Does.Contain(expectedPublicKey));

                Assert.That(json, Does.Contain("\"Signature\""));

                using var doc = JsonDocument.Parse(json);
                var sigInJson = doc.RootElement.GetProperty("Signature").GetString();
                Assert.That(sigInJson, Is.EqualTo(signature));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(expectedPublicKey)
                };
            });

            var result = await client.ClaimNameAsync(name, expectedPublicKey, signature, _ct).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(expectedPublicKey));
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

            Assert.ThrowsAsync<ArgumentException>(async () => await client.ClaimNameAsync("", "ADDR", "sig", _ct).ConfigureAwait(false));
        }

        [Test]
        public void ClaimNameAsyncThrowsOnEmptyAddress()
        {
            using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK));

            Assert.ThrowsAsync<ArgumentException>(async () => await client.ClaimNameAsync("name", "", "sig", _ct).ConfigureAwait(false));
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
            var name = "dupname";
            var (publicKey, signature) = CreateValidSignature(name);

            using var client = CreateClient(req =>
            {
                if (req.RequestUri.AbsolutePath.Contains("/claim", StringComparison.Ordinal) && first)
                {
                    first = false;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(publicKey)
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Empty)
                };
            });

            var firstResult = await client.ClaimNameAsync(name, publicKey, signature, _ct).ConfigureAwait(false);
            var secondResult = await client.ClaimNameAsync(name, publicKey, signature, _ct).ConfigureAwait(false);

            Assert.That(firstResult, Is.EqualTo(publicKey));
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
            var name = "a";
            var (expectedPublicKey, signature) = CreateValidSignature(name);

            using var client = CreateClient(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedPublicKey)
            });

            var result = await client.ClaimNameAsync(name, expectedPublicKey, signature, _ct).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(expectedPublicKey));
        }

        [Test]
        public async Task ClaimNameAsyncAcceptsMaxLengthName()
        {
            var name = new string('x', 48);
            var (expectedPublicKey, signature) = CreateValidSignature(name);

            using var client = CreateClient(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedPublicKey)
            });

            var result = await client.ClaimNameAsync(name, expectedPublicKey, signature, _ct).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(expectedPublicKey));
        }
    }
}
