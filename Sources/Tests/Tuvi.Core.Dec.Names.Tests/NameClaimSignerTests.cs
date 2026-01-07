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
using NUnit.Framework;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Tuvi.Base32EConverterLib;

namespace Tuvi.Core.Dec.Names.Tests
{
    [TestFixture]
    public sealed class NameClaimSignerTests
    {
        private static (string PublicKeyBase32E, ECPrivateKeyParameters PrivateKey, X9ECParameters Curve) GenerateKeyMaterial()
        {
            var curve = SecNamedCurves.GetByName("secp256k1");
            var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

            var gen = new ECKeyPairGenerator();
            gen.Init(new ECKeyGenerationParameters(domain, new SecureRandom()));

            AsymmetricCipherKeyPair pair = gen.GenerateKeyPair();
            var priv = (ECPrivateKeyParameters)pair.Private;
            var pub = (ECPublicKeyParameters)pair.Public;

            var compressed = pub.Q.GetEncoded(true);
            var pubBase32E = Base32EConverter.ToEmailBase32(compressed);

            return (pubBase32E, priv, curve);
        }

        private static ECPrivateKeyParameters GenerateNonSecp256k1PrivateKey()
        {
            var curve = SecNamedCurves.GetByName("secp256r1");
            var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

            var gen = new ECKeyPairGenerator();
            gen.Init(new ECKeyGenerationParameters(domain, new SecureRandom()));

            AsymmetricCipherKeyPair pair = gen.GenerateKeyPair();
            return (ECPrivateKeyParameters)pair.Private;
        }

        [Test]
        public void SignClaimV1SignatureVerifies()
        {
            // Arrange
            var (publicKey, privateKey, _) = GenerateKeyMaterial();
            const string name = "testname";

            // Act
            var signature = NameClaimSigner.SignClaimV1(name, publicKey, privateKey);
            var verifies = NameClaimVerifier.VerifyClaimV1Signature(name, publicKey, signature);

            // Assert
            Assert.That(verifies, Is.True);
        }

        [Test]
        public void SignClaimV1ProducesLowS()
        {
            // Arrange
            var (publicKey, privateKey, curve) = GenerateKeyMaterial();
            const string name = "lowstest";
            var half = curve.N.ShiftRight(1);

            // Act
            var signature = NameClaimSigner.SignClaimV1(name, publicKey, privateKey);
            var der = Convert.FromBase64String(signature);
            var seq = (Asn1Sequence)Asn1Object.FromByteArray(der);
            var s = ((DerInteger)seq[1]).PositiveValue;

            // Assert
            Assert.That(seq.Count, Is.EqualTo(2));
            Assert.That(s.CompareTo(half) <= 0, Is.True);
        }

        [Test]
        public void SignClaimV1ProducesValidBase64DerSequence()
        {
            // Arrange
            var (publicKey, privateKey, _) = GenerateKeyMaterial();

            // Act
            var signature = NameClaimSigner.SignClaimV1("name", publicKey, privateKey);
            var der = Convert.FromBase64String(signature);
            var seq = (Asn1Sequence)Asn1Object.FromByteArray(der);
            var r = ((DerInteger)seq[0]).PositiveValue;
            var s = ((DerInteger)seq[1]).PositiveValue;

            // Assert
            Assert.That(seq.Count, Is.EqualTo(2));
            Assert.That(r.SignValue, Is.GreaterThan(0));
            Assert.That(s.SignValue, Is.GreaterThan(0));
        }

        [Test]
        public void SignClaimV1IsDeterministicForSameInputs()
        {
            // Arrange
            var (publicKey, privateKey, _) = GenerateKeyMaterial();

            // Act
            var sig1 = NameClaimSigner.SignClaimV1("det", publicKey, privateKey);
            var sig2 = NameClaimSigner.SignClaimV1("det", publicKey, privateKey);

            // Assert
            Assert.That(sig1, Is.EqualTo(sig2));
        }

        [Test]
        public void SignClaimV1SameCanonicalNameProducesSameSignature()
        {
            // Arrange
            var (publicKey, privateKey, _) = GenerateKeyMaterial();

            // Act
            var sig1 = NameClaimSigner.SignClaimV1("Alice", publicKey, privateKey);
            var sig2 = NameClaimSigner.SignClaimV1("  a l+i ce ", publicKey, privateKey);

            // Assert
            Assert.That(sig1, Is.EqualTo(sig2));
        }

        [Test]
        public void SignClaimV1ThrowsOnEmptyName()
        {
            // Arrange
            var (publicKey, privateKey, _) = GenerateKeyMaterial();

            // Act
            TestDelegate act = () => NameClaimSigner.SignClaimV1(string.Empty, publicKey, privateKey);

            // Assert
            Assert.That(act, Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void SignClaimV1ThrowsOnNullName()
        {
            // Arrange
            var (publicKey, privateKey, _) = GenerateKeyMaterial();

            // Act
            TestDelegate act = () => NameClaimSigner.SignClaimV1(null, publicKey, privateKey);

            // Assert
            Assert.That(act, Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void SignClaimV1ThrowsOnWhitespaceName()
        {
            // Arrange
            var (publicKey, privateKey, _) = GenerateKeyMaterial();

            // Act
            TestDelegate act = () => NameClaimSigner.SignClaimV1("   ", publicKey, privateKey);

            // Assert
            Assert.That(act, Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void SignClaimV1ThrowsOnEmptyPublicKey()
        {
            // Arrange
            var (_, privateKey, _) = GenerateKeyMaterial();

            // Act
            TestDelegate act = () => NameClaimSigner.SignClaimV1("name", string.Empty, privateKey);

            // Assert
            Assert.That(act, Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void SignClaimV1ThrowsOnNullPublicKey()
        {
            // Arrange
            var (_, privateKey, _) = GenerateKeyMaterial();

            // Act
            TestDelegate act = () => NameClaimSigner.SignClaimV1("name", null, privateKey);

            // Assert
            Assert.That(act, Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void SignClaimV1ThrowsOnWhitespacePublicKey()
        {
            // Arrange
            var (_, privateKey, _) = GenerateKeyMaterial();

            // Act
            TestDelegate act = () => NameClaimSigner.SignClaimV1("name", "   ", privateKey);

            // Assert
            Assert.That(act, Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void SignClaimV1ThrowsOnNullPrivateKey()
        {
            // Arrange
            const string name = "name";
            const string publicKey = "pub";

            // Act
            TestDelegate act = () => NameClaimSigner.SignClaimV1(name, publicKey, null);

            // Assert
            Assert.That(act, Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void SignClaimV1ThrowsOnNonSecp256k1PrivateKey()
        {
            // Arrange
            var (publicKey, _, _) = GenerateKeyMaterial();
            var nonSecpKey = GenerateNonSecp256k1PrivateKey();

            // Act
            TestDelegate act = () => NameClaimSigner.SignClaimV1("name", publicKey, nonSecpKey);

            // Assert
            Assert.That(act, Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void SignClaimV1WithLongNameProducesValidSignature()
        {
            // Arrange
            var (publicKey, privateKey, _) = GenerateKeyMaterial();
            var longName = new string('a', 1000);

            // Act
            var signature = NameClaimSigner.SignClaimV1(longName, publicKey, privateKey);
            var verifies = NameClaimVerifier.VerifyClaimV1Signature(longName, publicKey, signature);

            // Assert
            Assert.That(verifies, Is.True);
        }

        [Test]
        public void SignClaimV1DifferentNamesProduceDifferentSignatures()
        {
            // Arrange
            var (publicKey, privateKey, _) = GenerateKeyMaterial();

            // Act
            var sig1 = NameClaimSigner.SignClaimV1("alice", publicKey, privateKey);
            var sig2 = NameClaimSigner.SignClaimV1("bob", publicKey, privateKey);

            // Assert
            Assert.That(sig1, Is.Not.EqualTo(sig2));
        }

        [Test]
        public void SignClaimV1DifferentPublicKeysProduceDifferentSignatures()
        {
            // Arrange
            var (publicKey1, privateKey, _) = GenerateKeyMaterial();
            var (publicKey2, _, _) = GenerateKeyMaterial();

            // Act
            var sig1 = NameClaimSigner.SignClaimV1("name", publicKey1, privateKey);
            var sig2 = NameClaimSigner.SignClaimV1("name", publicKey2, privateKey);

            // Assert
            Assert.That(sig1, Is.Not.EqualTo(sig2));
        }

        [Test]
        public void SignClaimV1DifferentPrivateKeysProduceDifferentSignatures()
        {
            // Arrange
            var (publicKey, privateKey1, _) = GenerateKeyMaterial();
            var (_, privateKey2, _) = GenerateKeyMaterial();

            // Act
            var sig1 = NameClaimSigner.SignClaimV1("name", publicKey, privateKey1);
            var sig2 = NameClaimSigner.SignClaimV1("name", publicKey, privateKey2);

            // Assert
            Assert.That(sig1, Is.Not.EqualTo(sig2));
        }

        [Test]
        public void SignClaimV1WithSpecialCharactersInPublicKeyProducesValidSignature()
        {
            // Arrange
            var (publicKey, privateKey, _) = GenerateKeyMaterial();
            var specialPublicKey = publicKey + "==";

            // Act
            var signature = NameClaimSigner.SignClaimV1("name", specialPublicKey, privateKey);
            var verifies = NameClaimVerifier.VerifyClaimV1Signature("name", specialPublicKey, signature);

            // Assert
            Assert.That(verifies, Is.False); // Invalid public key should fail verification
            Assert.That(signature, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void SignClaimV1WithMinimalValidNameProducesValidSignature()
        {
            // Arrange
            var (publicKey, privateKey, _) = GenerateKeyMaterial();
            const string minimalName = "a";

            // Act
            var signature = NameClaimSigner.SignClaimV1(minimalName, publicKey, privateKey);
            var verifies = NameClaimVerifier.VerifyClaimV1Signature(minimalName, publicKey, signature);

            // Assert
            Assert.That(verifies, Is.True);
        }

        [Test]
        public void SignClaimV1AllowsNameThatCanonicalizesToTestSuffix()
        {
            // Arrange
            var (publicKey, privateKey, _) = GenerateKeyMaterial();
            const string name = "+ + +";

            // Act
            var signature = NameClaimSigner.SignClaimV1(name, publicKey, privateKey);
            var verifies = NameClaimVerifier.VerifyClaimV1Signature(name, publicKey, signature);

            // Assert
            Assert.That(verifies, Is.True);
        }

        [Test]
        public void SignClaimV1WithMismatchedKeyPairDoesNotVerifyAgainstClaimedPublicKey()
        {
            // Arrange
            var (publicKey1, privateKey1, _) = GenerateKeyMaterial();
            var (publicKey2, _, _) = GenerateKeyMaterial();

            // Act
            var signature = NameClaimSigner.SignClaimV1("name", publicKey2, privateKey1);
            var verifies = NameClaimVerifier.VerifyClaimV1Signature("name", publicKey2, signature);

            // Assert
            Assert.That(verifies, Is.False);
            Assert.That(signature, Is.Not.Null.And.Not.Empty);
            Assert.That(publicKey1, Is.Not.EqualTo(publicKey2));
        }
    }
}
