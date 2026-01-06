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
using TuviPgpLibImpl;

namespace SecurityManagementTests
{
    [TestFixture]
    public class Secp256k1CompressedBase32ECodecTests
    {
        private IEcPublicKeyCodec _codec;
        private MasterKey _masterKey;

        [SetUp]
        public void Setup()
        {
            _codec = new Secp256k1CompressedBase32ECodec();
            _masterKey = TestData.MasterKey;
        }

        [Test]
        public void EncodeNullThrows()
        {
            TestDelegate act = () => _codec.Encode(null);

            Assert.Throws<ArgumentNullException>(act);
        }

        [Test]
        public void DecodeNullThrows()
        {
            TestDelegate act = () => _codec.Decode(null);

            Assert.Throws<ArgumentNullException>(act);
        }

        [Test]
        public void RoundTripValid()
        {
            var original = EccPgpContext.GenerateEccPublicKey(_masterKey, 0, 0, 0, 2);

            var encoded = _codec.Encode(original);
            var decoded = _codec.Decode(encoded);

            Assert.That(decoded, Is.EqualTo(original));
        }

        [Test]
        public void DecodeWrongLengthThrows()
        {
            const string invalid = "abc";

            TestDelegate act = () => _codec.Decode(invalid);

            Assert.Throws<ArgumentException>(act);
        }

        [Test]
        public void DecodeWrongPrefixThrows()
        {
            const string valid = "agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c";
            var mutated = string.Concat("z", valid.AsSpan(1));

            Assert.Throws<FormatException>(() => _codec.Decode(mutated));
        }
    }
}
