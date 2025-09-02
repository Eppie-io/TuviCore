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

using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Entities;
using Tuvi.Core.Utils;

namespace SecurityManagementTests
{
    [TestFixture]
    public class HybridAddressResolutionTests
    {
        [Test]
        public async Task HybridAddressDoesNotInvokeNameResolver()
        {
            bool invoked = false;
            var resolver = new EppieNameResolverStub(_ => { invoked = true; return null; });
            var service = PublicKeyService.CreateDefault(resolver);
            // hybrid form: user+<key>@eppie to ensure Eppie network context
            var hybrid = new EmailAddress("user+agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c@eppie");
            var encoded = await service.GetEncodedByEmailAsync(hybrid, default).ConfigureAwait(false);
            Assert.That(encoded, Is.EqualTo(hybrid.DecentralizedAddress));
            Assert.That(invoked, Is.False);
        }

        private sealed class EppieNameResolverStub : Tuvi.Core.Dec.IEppieNameResolver
        {
            private readonly System.Func<string, string> _resolver;
            public EppieNameResolverStub(System.Func<string, string> resolver) { _resolver = resolver; }
            public Task<string> ResolveAsync(string name, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(_resolver(name));
        }
    }
}
