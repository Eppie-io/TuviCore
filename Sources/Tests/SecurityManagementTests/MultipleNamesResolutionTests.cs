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

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Dec;
using Tuvi.Core.Entities;
using Tuvi.Core.Utils;

namespace SecurityManagementTests
{
    [TestFixture]
    public class MultipleNamesResolutionTests
    {
        private sealed class StubResolver : IEppieNameResolver
        {
            public Task<string> ResolveAsync(string name, System.Threading.CancellationToken cancellationToken = default)
            {
                return Task.FromResult(name switch
                {
                    "alias1" => "agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c",
                    "alias2" => "agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2d",
                    _ => null
                });
            }
        }

        [SetUp]
        public void Setup() { }

        [Test]
        public async Task ResolvesMultipleNamesIndependently()
        {
            var resolver = new StubResolver();
            var service = PublicKeyService.CreateDefault(resolver);
            var emails = new[]{
                EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, "alias1"),
                EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, "alias2")
            };
            var keys = await Task.WhenAll(emails.Select(e => service.GetEncodedByEmailAsync(e, default))).ConfigureAwait(false);
            Assert.That(keys[0], Is.EqualTo("agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c"));
            Assert.That(keys[1], Is.EqualTo("agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2d"));
        }
    }
}
