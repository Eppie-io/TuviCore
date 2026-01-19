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
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Tuvi.Base32EConverterLib;

namespace Tuvi.Core.Dec.Names.Tests
{
    [TestFixture]
    public sealed class NameClaimVerifierTests
    {
        private static (string PublicKeyBase32E, ECPrivateKeyParameters PrivateKey) GenerateKeyMaterial()
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

            return (pubBase32E, priv);
        }

        [TestCase(null, "pub", "sig")]
        [TestCase("", "pub", "sig")]
        [TestCase("   ", "pub", "sig")]
        [TestCase("name", null, "sig")]
        [TestCase("name", "", "sig")]
        [TestCase("name", "   ", "sig")]
        [TestCase("name", "pub", null)]
        [TestCase("name", "pub", "")]
        [TestCase("name", "pub", "   ")]
        public void VerifyClaimV1SignatureReturnsFalseOnEmptyArgs(string name, string publicKey, string signature)
        {
            // Arrange
            var n = name;
            var pk = publicKey;
            var sig = signature;

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature(n, pk, sig);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseOnNonBase64Signature()
        {
            // Arrange
            const string name = "name";
            const string publicKey = "pub";
            const string badSignature = "not base64";

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature(name, publicKey, badSignature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseOnInvalidDerSignature()
        {
            // Arrange
            const string name = "name";
            const string publicKey = "pub";
            var invalidDer = Convert.ToBase64String(new byte[] { 0x01, 0x02, 0x03 });

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature(name, publicKey, invalidDer);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseOnWrongDerSequenceElementCount()
        {
            // Arrange
            const string name = "name";
            const string publicKey = "pub";
            var seq = new DerSequence(new DerInteger(1));
            var signature = Convert.ToBase64String(seq.GetDerEncoded());

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature(name, publicKey, signature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseOnDerWithNonIntegerElements()
        {
            // Arrange
            const string name = "name";
            const string publicKey = "pub";
            var seq = new DerSequence(new DerUtf8String("r"), new DerUtf8String("s"));
            var signature = Convert.ToBase64String(seq.GetDerEncoded());

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature(name, publicKey, signature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseOnInvalidPublicKeyBase32E()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("name", pub, priv);

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", "***", signature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseOnInvalidCompressedPointBytes()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("name", pub, priv);
            var badCompressed = Base32EConverter.ToEmailBase32(new byte[] { 0x02, 0x01, 0x02 });

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", badCompressed, signature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseWhenNameDoesNotMatch()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("name", pub, priv);

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("other", pub, signature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseWhenPublicKeyDoesNotMatch()
        {
            // Arrange
            var (pub1, priv1) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("name", pub1, priv1);
            var (pub2, _) = GenerateKeyMaterial();

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", pub2, signature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseWhenSignatureIsModified()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("name", pub, priv);
            var sigBytes = Convert.FromBase64String(signature);
            sigBytes[0] ^= 0x01;
            var modifiedSignature = Convert.ToBase64String(sigBytes);

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", pub, modifiedSignature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureAllowsCanonicalizedName()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("Alice", pub, priv);

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("  a l+i ce ", pub, signature);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void VerifyClaimV1SignatureWithTruncatedSignatureReturnsFalse()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("name", pub, priv);
            var sigBytes = Convert.FromBase64String(signature);
            var truncated = new byte[sigBytes.Length / 2];
            Array.Copy(sigBytes, truncated, truncated.Length);
            var truncatedSignature = Convert.ToBase64String(truncated);

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", pub, truncatedSignature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureWithEmptyDerSequenceReturnsFalse()
        {
            // Arrange
            var (pub, _) = GenerateKeyMaterial();
            var emptySeq = new DerSequence();
            var signature = Convert.ToBase64String(emptySeq.GetDerEncoded());

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", pub, signature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureWithZeroRReturnsFalse()
        {
            // Arrange
            var (pub, _) = GenerateKeyMaterial();
            var seq = new DerSequence(new DerInteger(0), new DerInteger(1));
            var signature = Convert.ToBase64String(seq.GetDerEncoded());

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", pub, signature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureWithZeroSReturnsFalse()
        {
            // Arrange
            var (pub, _) = GenerateKeyMaterial();
            var seq = new DerSequence(new DerInteger(1), new DerInteger(0));
            var signature = Convert.ToBase64String(seq.GetDerEncoded());

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", pub, signature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureMultipleCallsSameResultIsDeterministic()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("name", pub, priv);

            // Act
            var result1 = NameClaimVerifier.VerifyClaimV1Signature("name", pub, signature);
            var result2 = NameClaimVerifier.VerifyClaimV1Signature("name", pub, signature);
            var result3 = NameClaimVerifier.VerifyClaimV1Signature("name", pub, signature);

            // Assert
            Assert.That(result1, Is.True);
            Assert.That(result2, Is.True);
            Assert.That(result3, Is.True);
        }

        [Test]
        public void VerifyClaimV1SignatureWithLongNameWorksCorrectly()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var longName = new string('a', 1000);
            var signature = NameClaimSigner.SignClaimV1(longName, pub, priv);

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature(longName, pub, signature);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void VerifyClaimV1SignatureWithTooShortPublicKeyBytesReturnsFalse()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("name", pub, priv);
            var shortKey = Base32EConverter.ToEmailBase32(new byte[] { 0x02, 0x01 });

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", shortKey, signature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureWithThreeElementDerSequenceReturnsFalse()
        {
            // Arrange
            var (pub, _) = GenerateKeyMaterial();
            var seq = new DerSequence(new DerInteger(1), new DerInteger(2), new DerInteger(3));
            var signature = Convert.ToBase64String(seq.GetDerEncoded());

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", pub, signature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseOnPaddedBase64Signature()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("name", pub, priv);
            var paddedSignature = signature + "AA==";

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", pub, paddedSignature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseOnHighSSignature()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("name", pub, priv);

            var der = Convert.FromBase64String(signature);
            var seq = (Asn1Sequence)Asn1Object.FromByteArray(der);
            var r = ((DerInteger)seq[0]).PositiveValue;
            var s = ((DerInteger)seq[1]).PositiveValue;
            var curve = SecNamedCurves.GetByName("secp256k1");
            var highS = curve.N.Subtract(s);
            var highSSeq = new DerSequence(new DerInteger(r), new DerInteger(highS));
            var highSignature = Convert.ToBase64String(highSSeq.GetDerEncoded());

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", pub, highSignature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseWhenPublicKeyIsOffCurve()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("name", pub, priv);
            var compressed = Base32EConverter.FromEmailBase32(pub);
            var invalidCompressed = new byte[compressed.Length];
            Array.Copy(compressed, invalidCompressed, compressed.Length);
            invalidCompressed[^1] ^= 0xFF;
            var invalidPub = Base32EConverter.ToEmailBase32(invalidCompressed);

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", invalidPub, signature);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void VerifyClaimV1SignatureReturnsFalseOnBase32EDecodableButInvalidCompressedPointPrefix()
        {
            // Arrange
            var (pub, priv) = GenerateKeyMaterial();
            var signature = NameClaimSigner.SignClaimV1("name", pub, priv);

            // Create Base32E that decodes to 33 bytes but starts with an invalid compressed point prefix.
            // Valid prefixes for compressed points are 0x02 or 0x03.
            var invalidCompressed = new byte[33];
            invalidCompressed[0] = 0x04;
            var invalidPub = Base32EConverter.ToEmailBase32(invalidCompressed);

            // Act
            var result = NameClaimVerifier.VerifyClaimV1Signature("name", invalidPub, signature);

            // Assert
            Assert.That(result, Is.False);
        }
    }
}
