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

using NUnit.Framework;

namespace Tuvi.Core.Dec.Names.Tests
{
    [TestFixture]
    public sealed class NameClaimTests
    {
        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("   ", "")]
        public void CanonicalizeNameReturnsEmpty(string name, string expected)
        {
            // Arrange
            var input = name;

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void CanonicalizeNameTrimLowercaseRemoveSpaceAndPlusAndAppendTestSuffix()
        {
            // Arrange
            const string input = "  A+ l i + C E  ";

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo("alice.test"));
        }

        [Test]
        public void CanonicalizeNameDoesNotDuplicateTestSuffix()
        {
            // Arrange
            const string input = "alice.test";

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo("alice.test"));
        }

        [Test]
        public void CanonicalizeNameNormalizesSuffixCase()
        {
            // Arrange
            const string input = "ALICE.TEST";

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo("alice.test"));
        }

        [Test]
        public void CanonicalizeNameTrimsAfterSuffix()
        {
            // Arrange
            const string input = "alice.test  ";

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo("alice.test"));
        }

        [Test]
        public void BuildClaimV1PayloadUsesLfLineEndingsAndDeterministicFormat()
        {
            // Arrange
            const string name = "Alice";
            const string publicKey = "PUB";
            const string expectedPayload = "claim-v1\nname=alice.test\npublicKey=PUB";

            // Act
            var payload = NameClaim.BuildClaimV1Payload(name, publicKey);
            var hasCarriageReturn = payload.Split('\r').Length != 1;

            // Assert
            Assert.That(payload, Is.EqualTo(expectedPayload));
            Assert.That(hasCarriageReturn, Is.False);
        }

        [Test]
        public void BuildClaimV1PayloadDoesNotTrimOrNormalizePublicKey()
        {
            // Arrange
            const string name = "Alice";
            const string publicKey = "  PUB  ";
            const string expectedPayload = "claim-v1\nname=alice.test\npublicKey=  PUB  ";

            // Act
            var payload = NameClaim.BuildClaimV1Payload(name, publicKey);

            // Assert
            Assert.That(payload, Is.EqualTo(expectedPayload));
        }

        [Test]
        public void BuildClaimV1PayloadIsDeterministic()
        {
            // Arrange
            const string name = "Alice";
            const string publicKey = "PUB";

            // Act
            var payload1 = NameClaim.BuildClaimV1Payload(name, publicKey);
            var payload2 = NameClaim.BuildClaimV1Payload(name, publicKey);

            // Assert
            Assert.That(payload1, Is.EqualTo(payload2));
        }

        [Test]
        public void BuildClaimV1PayloadAllowsNullName()
        {
            // Arrange
            const string publicKey = "PUB";

            // Act
            var payload = NameClaim.BuildClaimV1Payload(null, publicKey);

            // Assert
            Assert.That(payload, Is.EqualTo("claim-v1\nname=\npublicKey=PUB"));
        }

        [Test]
        public void BuildClaimV1PayloadAllowsNullPublicKey()
        {
            // Arrange
            const string name = "Alice";

            // Act
            var payload = NameClaim.BuildClaimV1Payload(name, null);

            // Assert
            Assert.That(payload, Is.EqualTo("claim-v1\nname=alice.test\npublicKey="));
        }

        [Test]
        public void CanonicalizeNameWithMultiplePlusCharactersRemovesAll()
        {
            // Arrange
            const string input = "a+l+i+c+e";

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo("alice.test"));
        }

        [Test]
        public void CanonicalizeNameWithMixedCaseConvertsToLowercase()
        {
            // Arrange
            const string input = "AlIcE";

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo("alice.test"));
        }

        [Test]
        public void CanonicalizeNameWithOnlyPlusAndSpacesReturnsTestSuffix()
        {
            // Arrange
            const string input = "+ + +";

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo(".test"));
        }

        [Test]
        public void CanonicalizeNameWithTestInMiddleAppendsTestSuffix()
        {
            // Arrange
            const string input = "test.user";

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo("test.user.test"));
        }

        [Test]
        public void CanonicalizeNameWithPartialTestSuffixAppendsFullSuffix()
        {
            // Arrange
            const string input = "alice.tes";

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo("alice.tes.test"));
        }

        [Test]
        public void BuildClaimV1PayloadWithBothNullArgumentsReturnsValidPayload()
        {
            // Arrange
            // Act
            var payload = NameClaim.BuildClaimV1Payload(null, null);

            // Assert
            Assert.That(payload, Is.EqualTo("claim-v1\nname=\npublicKey="));
        }

        [Test]
        public void BuildClaimV1PayloadWithSpecialCharactersInPublicKeyPreservesCharacters()
        {
            // Arrange
            const string name = "Alice";
            const string publicKey = "abc+/=123";

            // Act
            var payload = NameClaim.BuildClaimV1Payload(name, publicKey);

            // Assert
            Assert.That(payload, Is.EqualTo("claim-v1\nname=alice.test\npublicKey=abc+/123"));
        }

        [Test]
        public void CanonicalizeNameWithTabCharacterDoesNotRemoveTab()
        {
            // Arrange
            const string input = "ali\tce";

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo("ali\tce.test"));
        }

        [Test]
        public void CanonicalizeNameWithNewlineCharacterDoesNotRemoveNewline()
        {
            // Arrange
            const string input = "ali\nce";

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo("ali\nce.test"));
        }

        [Test]
        public void CanonicalizeNameWithVeryLongInputHandlesCorrectly()
        {
            // Arrange
            var input = new string('a', 1000);

            // Act
            var result = NameClaim.CanonicalizeName(input);

            // Assert
            Assert.That(result, Is.EqualTo(new string('a', 1000) + ".test"));
        }

        [Test]
        public void BuildClaimV1PayloadWithNewlinesInPublicKeyPreservesNewlines()
        {
            // Arrange
            const string name = "Alice";
            const string publicKey = "line1\nline2";

            // Act
            var payload = NameClaim.BuildClaimV1Payload(name, publicKey);

            // Assert
            Assert.That(payload, Is.EqualTo("claim-v1\nname=alice.test\npublicKey=line1line2"));
        }

        [Test]
        public void BuildClaimV1PayloadWithWrongName()
        {
            const string name = "alice\npublicKey=INJECTED";
            const string publicKey = "PUB";

            var payload = NameClaim.BuildClaimV1Payload(name, publicKey);

            Assert.That(payload, Is.EqualTo("claim-v1\nname=alicepublickeyinjected.test\npublicKey=PUB"));
        }

        [Test]
        public void BuildClaimV1PayloadWithWrongKey()
        {
            const string name = "Alice";
            const string publicKey = "PUB\nname=hijack";

            var payload = NameClaim.BuildClaimV1Payload(name, publicKey);

            Assert.That(payload, Is.EqualTo("claim-v1\nname=alice.test\npublicKey=PUBnamehijack"));
        }
    }
}
