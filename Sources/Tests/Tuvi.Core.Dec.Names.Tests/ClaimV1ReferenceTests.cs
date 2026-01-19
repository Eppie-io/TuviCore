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
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Tuvi.Base32EConverterLib;

namespace Tuvi.Core.Dec.Names.Tests
{
    [TestFixture]
    public sealed class ClaimV1ReferenceTests
    {
        [Test]
        public void ClaimV1ReferenceVerifiesAndIsStable()
        {
            // Arrange
            const string name = "Al i+ce";

            var privScalar = new BigInteger("1", 16);
            var domain = Secp256k1Domain();
            var priv = new ECPrivateKeyParameters(privScalar, domain);

            var pubPoint = domain.G.Multiply(privScalar);
            var compressed = pubPoint.GetEncoded(true);
            var pubBase32E = Base32EConverter.ToEmailBase32(compressed);

            var expectedPayload = $"claim-v1\nname=alice.test\npublicKey={pubBase32E}";

            // Act
            var payload = NameClaim.BuildClaimV1Payload(name, pubBase32E);
            var signature1 = NameClaimSigner.SignClaimV1(name, pubBase32E, priv);
            var signature2 = NameClaimSigner.SignClaimV1(name, pubBase32E, priv);
            var verifies = NameClaimVerifier.VerifyClaimV1Signature(name, pubBase32E, signature1);

            // Assert
            Assert.That(payload, Is.EqualTo(expectedPayload));
            Assert.That(signature1, Is.EqualTo(signature2));
            Assert.That(verifies, Is.True);
        }

        [Test]
        public void ClaimV1ReferenceProducesDeterministicLowSAndValidDer()
        {
            // Arrange
            const string name = "Al i+ce";

            var privScalar = new BigInteger("1", 16);
            var domain = Secp256k1Domain();
            var priv = new ECPrivateKeyParameters(privScalar, domain);

            var pubPoint = domain.G.Multiply(privScalar);
            var compressed = pubPoint.GetEncoded(true);
            var pubBase32E = Base32EConverter.ToEmailBase32(compressed);

            // Act
            var signature1 = NameClaimSigner.SignClaimV1(name, pubBase32E, priv);
            var signature2 = NameClaimSigner.SignClaimV1(name, pubBase32E, priv);
            var der = Convert.FromBase64String(signature1);
            var seq = (Org.BouncyCastle.Asn1.Asn1Sequence)Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(der);
            var s = ((Org.BouncyCastle.Asn1.DerInteger)seq[1]).PositiveValue;
            var verifies = NameClaimVerifier.VerifyClaimV1Signature(name, pubBase32E, signature1);

            // Assert
            Assert.That(signature1, Is.EqualTo(signature2));
            Assert.That(verifies, Is.True);
            Assert.That(seq.Count, Is.EqualTo(2));
            Assert.That(s.CompareTo(domain.N.ShiftRight(1)) <= 0, Is.True);
        }

        private static ECDomainParameters Secp256k1Domain()
        {
            var curve = SecNamedCurves.GetByName("secp256k1");
            return new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
        }
    }
}
