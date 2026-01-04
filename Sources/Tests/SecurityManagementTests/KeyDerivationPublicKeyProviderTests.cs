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
using KeyDerivation.Keys;
using NUnit.Framework;
using Tuvi.Core.Utils;

namespace SecurityManagementTests
{
    [TestFixture]
    public class KeyDerivationPublicKeyProviderTests
    {
        private EccKeyDerivationPublicKeyProvider _provider;
        private MasterKey _masterKey;

        [SetUp]
        public void Setup()
        {
            _provider = new EccKeyDerivationPublicKeyProvider();
            _masterKey = TestData.MasterKey;
        }

        [Test]
        public void DeriveByIndexSucceeds()
        {
            var key = _provider.Derive(_masterKey, 0, 0, 0, 3);
            Assert.That(key, Is.Not.Null);
        }

        [Test]
        public void DeriveByIndexNullMasterThrows()
        {
            Assert.Throws<ArgumentNullException>(() => _provider.Derive(null, 0, 0, 0, 0));
        }

        [Test]
        public void DeriveByTagSucceeds()
        {
            var key = _provider.Derive(_masterKey, "tag-xyz");
            Assert.That(key, Is.Not.Null);
        }

        [Test]
        public void DeriveByTagNullMasterThrows()
        {
            Assert.Throws<ArgumentNullException>(() => _provider.Derive(null, "tag"));
        }

        [Test]
        public void DeriveByTagNullTagThrows()
        {
            Assert.Throws<ArgumentException>(() => _provider.Derive(_masterKey, (string)null));
        }

        [Test]
        public void DeriveByTagEmptyTagThrows()
        {
            Assert.Throws<ArgumentException>(() => _provider.Derive(_masterKey, string.Empty));
        }
    }
}
