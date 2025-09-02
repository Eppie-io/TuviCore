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
using System.Threading.Tasks;
using KeyDerivation.Keys;
using NUnit.Framework;
using Tuvi.Core.Entities;
using Tuvi.Core.Utils;
using TuviPgpLibImpl;

namespace SecurityManagementTests
{
    [TestFixture]
    public class PublicKeyServiceTests
    {
        private PublicKeyService _svc;
        private MasterKey _masterKey;

        [SetUp]
        public void SetUp()
        {
            _svc = PublicKeyService.CreateDefault(PublicKeyService.NoOpNameResolver);
            _masterKey = TestData.MasterKey;
        }

        [Test]
        public void EncodeDecodeRoundTripSucceeds()
        {
            var original = EccPgpContext.GenerateEccPublicKey(_masterKey, 0, 0, 0, 0);

            var encoded = _svc.Encode(original);
            var decoded = _svc.Decode(encoded);

            Assert.That(decoded, Is.EqualTo(original));
        }

        [Test]
        public void EncodeNullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => _svc.Encode(null));
        }

        [Test]
        public void DecodeNullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => _svc.Decode(null));
        }

        [Test]
        public void DecodeWrongLengthThrows()
        {
            const string invalid = "abc"; // not 53 chars

            TestDelegate act = () => _svc.Decode(invalid);

            Assert.Throws<ArgumentException>(act);
        }

        [Test]
        public void DecodeWrongPrefixThrows()
        {
            const string valid = "agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c";
            var mutated = string.Concat("z", valid.AsSpan(1));

            Assert.Throws<FormatException>(() => _svc.Decode(mutated));
        }

        [Test]
        public void DeriveIndexedSucceeds()
        {
            var result = _svc.DeriveEncoded(_masterKey, 0, 0, 0, 5);

            Assert.That(result, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void DeriveTagSucceeds()
        {
            const string tag = "tag-1";

            var result = _svc.DeriveEncoded(_masterKey, tag);

            Assert.That(result, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void DeriveTagNullThrows()
        {
            TestDelegate act = () => _svc.DeriveEncoded(_masterKey, null);

            Assert.Throws<ArgumentException>(act);
        }

        [Test]
        public void DeriveTagEmptyThrows()
        {
            TestDelegate act = () => _svc.DeriveEncoded(_masterKey, string.Empty);

            Assert.Throws<ArgumentException>(act);
        }

        [Test]
        public void DeriveMasterKeyNullThrows()
        {
            TestDelegate act = () => _svc.DeriveEncoded(null, 0, 0, 0, 0);

            Assert.Throws<ArgumentNullException>(act);
        }

        [Test]
        public async Task ResolveEmailEppieSucceeds()
        {
            var expectedKey = EccPgpContext.GenerateEccPublicKey(_masterKey, 0, 0, 0, 1);
            var encoded = _svc.Encode(expectedKey);
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, encoded);

            var resolvedEncoded = await _svc.GetEncodedByEmailAsync(email, default).ConfigureAwait(false);
            var resolvedKey = await _svc.GetByEmailAsync(email, default).ConfigureAwait(false);

            Assert.That(resolvedEncoded, Is.EqualTo(encoded));
            Assert.That(resolvedKey, Is.EqualTo(expectedKey));
        }

        [Test]
        public void ResolveEmailNullThrows()
        {
            AsyncTestDelegate act1 = () => _svc.GetEncodedByEmailAsync(null, default);
            AsyncTestDelegate act2 = () => _svc.GetByEmailAsync(null, default);

            Assert.ThrowsAsync<ArgumentNullException>(act1);
            Assert.ThrowsAsync<ArgumentNullException>(act2);
        }
    }
}
