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
using NUnit.Framework;
using Org.BouncyCastle.Crypto.Parameters;
using Tuvi.Core.Utils;

namespace SecurityManagementTests
{
    [TestFixture]
    public class NetworkPublicKeyRulesTests
    {
        private const string ValidEppieKey = "agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c"; // sample from other tests

        private sealed class FailingDecodeCodec : IEcPublicKeyCodec
        {
            public string Encode(ECPublicKeyParameters publicKey) => throw new NotSupportedException();
            public ECPublicKeyParameters Decode(string encoded) => throw new FormatException("decode failed");
        }

        private sealed class UnexpectedDecodeCodec : IEcPublicKeyCodec
        {
            public string Encode(ECPublicKeyParameters publicKey) => throw new NotSupportedException();
            public ECPublicKeyParameters Decode(string encoded) => throw new InvalidOperationException("Decode should not be called for syntactically invalid input");
        }

        [Test]
        public void IsSyntacticallyValidReturnsTrueForValidKey()
        {
            var rules = new EppieNetworkPublicKeyRules(new FailingDecodeCodec()); // we don't call decode here

            var result = rules.IsSyntacticallyValid(ValidEppieKey);

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsSyntacticallyValidReturnsFalseForWrongLength()
        {
            var rules = new EppieNetworkPublicKeyRules(new FailingDecodeCodec());
            var wrong = ValidEppieKey.Substring(0, 10);

            var result = rules.IsSyntacticallyValid(wrong);

            Assert.That(result, Is.False);
        }

        [Test]
        public void IsSyntacticallyValidReturnsFalseForInvalidCharacter()
        {
            var rules = new EppieNetworkPublicKeyRules(new FailingDecodeCodec());
            var invalid = ValidEppieKey.Replace('a', '0'); // '0' is not in Base32E alphabet

            var result = rules.IsSyntacticallyValid(invalid);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TrySemanticValidateReturnsTrueForValidKey()
        {
            var codec = new Secp256k1CompressedBase32ECodec();
            var rules = new EppieNetworkPublicKeyRules(codec);

            var result = rules.TrySemanticValidate(ValidEppieKey);

            Assert.That(result, Is.True);
        }

        [Test]
        public void TrySemanticValidateReturnsFalseWhenCodecThrows()
        {
            var rules = new EppieNetworkPublicKeyRules(new FailingDecodeCodec());

            var result = rules.TrySemanticValidate(ValidEppieKey);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryValidateReturnsTrueForValidKey()
        {
            var codec = new Secp256k1CompressedBase32ECodec();
            var rules = new EppieNetworkPublicKeyRules(codec);

            var result = rules.TryValidate(ValidEppieKey);

            Assert.That(result, Is.True);
        }

        [Test]
        public void TryValidateReturnsFalseForSyntacticallyInvalidKeyDoesNotInvokeDecode()
        {
            var rules = new EppieNetworkPublicKeyRules(new UnexpectedDecodeCodec());
            var invalid = ValidEppieKey + "x"; // wrong length

            var result = rules.TryValidate(invalid);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryValidateReturnsFalseForSemanticFailure()
        {
            var rules = new EppieNetworkPublicKeyRules(new FailingDecodeCodec());

            var result = rules.TryValidate(ValidEppieKey);

            Assert.That(result, Is.False);
        }

        [Test]
        public void BitcoinRulesBasicBehavior()
        {
            var rules = new BitcoinNetworkPublicKeyRules();
            const string value = "someAddress";

            var syntax = rules.IsSyntacticallyValid(value);
            var semantic = rules.TrySemanticValidate(value);
            var combined = rules.TryValidate(value);

            Assert.That(syntax, Is.True);
            Assert.That(semantic, Is.True);
            Assert.That(combined, Is.True);
        }

        [Test]
        public void BitcoinRulesRejectsEmpty()
        {
            var rules = new BitcoinNetworkPublicKeyRules();

            var syntax = rules.IsSyntacticallyValid(string.Empty);
            var combined = rules.TryValidate(string.Empty);

            Assert.That(syntax, Is.False);
            Assert.That(combined, Is.False);
        }
    }
}
