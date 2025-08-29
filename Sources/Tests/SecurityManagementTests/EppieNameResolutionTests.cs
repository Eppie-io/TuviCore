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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Dec;
using Tuvi.Core.Entities;
using Tuvi.Core.Utils;

namespace SecurityManagementTests
{
    [TestFixture]
    public class EppieNameResolutionTests
    {
        private sealed class FakeNameResolver : IEppieNameResolver
        {
            private readonly Func<string,string> _resolver;
            public FakeNameResolver(Func<string,string> resolver) => _resolver = resolver;
            public Task<string> ResolveAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(_resolver(name));
        }

        private const string ValidKey = "agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c"; // 53 chars Base32E

        [Test]
        public async Task ResolvesHumanReadableNameToKey()
        {
            var service = PublicKeyService.CreateDefault(new FakeNameResolver(name => name == "alias" ? ValidKey : null));
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, "alias", string.Empty);
            var encoded = await service.GetEncodedByEmailAsync(email, default).ConfigureAwait(false);
            Assert.That(encoded, Is.EqualTo(ValidKey));
        }

        [Test]
        public void ThrowsWhenNameNotFound()
        {
            var service = PublicKeyService.CreateDefault(new FakeNameResolver(_ => null));
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, "unknown", string.Empty);
            Assert.ThrowsAsync<NoPublicKeyException>(() => service.GetEncodedByEmailAsync(email, default));
        }

        [Test]
        public void ThrowsWhenResolvedValueInvalid()
        {
            var service = PublicKeyService.CreateDefault(new FakeNameResolver(_ => "invalid_key"));
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, "alias", string.Empty);
            Assert.ThrowsAsync<NoPublicKeyException>(() => service.GetEncodedByEmailAsync(email, default));
        }

        [Test]
        public async Task DirectKeyBypassesResolver()
        {
            bool invoked = false;
            var service = PublicKeyService.CreateDefault(new FakeNameResolver(_ => { invoked = true; return ValidKey; }));
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, ValidKey, string.Empty);
            var encoded = await service.GetEncodedByEmailAsync(email, default).ConfigureAwait(false);
            Assert.That(encoded, Is.EqualTo(ValidKey));
            Assert.That(invoked, Is.False, "Resolver should not be called for direct key");
        }
    }
}
