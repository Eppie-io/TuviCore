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
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Entities;
using Tuvi.Core.Utils;

namespace SecurityManagementTests
{
    [TestFixture]
    public class EmailPublicKeyResolverTests
    {
        private sealed class MockEmptyBitcoinFetcher : IBitcoinPublicKeyFetcher
        {
            public Task<string> FetchAsync(string address) => Task.FromResult<string>(null);
        }

        [Test]
        public void BitcoinResolverPublicKeyMissingThrows()
        {
            var resolver = new BitcoinEmailPublicKeyResolver(new MockEmptyBitcoinFetcher());
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Bitcoin, "mydsbvVx5sTpf7h2WD5KxjVKzUAXZtC77i", string.Empty);

            AsyncTestDelegate act = () => resolver.ResolveAsync(email, default);

            Assert.ThrowsAsync<NoPublicKeyException>(act);
        }

        [Test]
        public async Task EppieResolverReturnsAddress()
        {
            var resolver = new EppieEmailPublicKeyResolver(new Secp256k1CompressedBase32ECodec(), PublicKeyService.NoOpNameResolver);
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, "agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c", string.Empty);

            var res = await resolver.ResolveAsync(email, default).ConfigureAwait(false);

            Assert.That(res, Is.EqualTo(email.DecentralizedAddress));
        }

        [Test]
        public void CompositeUnsupportedNetworkThrows()
        {
            var composite = new CompositeEmailPublicKeyResolver(new Dictionary<NetworkType, IEmailPublicKeyResolver>
            {
                { NetworkType.Eppie, new EppieEmailPublicKeyResolver(new Secp256k1CompressedBase32ECodec(), PublicKeyService.NoOpNameResolver) }
            });
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Bitcoin, "agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c", string.Empty);

            AsyncTestDelegate act = () => composite.ResolveAsync(email, default);

            Assert.ThrowsAsync<NotSupportedException>(act);
        }
    }
}
