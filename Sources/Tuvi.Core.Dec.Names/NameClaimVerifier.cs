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
using System.Text;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Tuvi.Base32EConverterLib;

namespace Tuvi.Core.Dec.Names
{
    /// <summary>
    /// Verifies signatures for the claim-v1 name registration protocol.
    /// </summary>
    /// <remarks>
    /// A claim-v1 signature is verified by:
    /// <list type="number">
    /// <item><description>building the deterministic claim payload;</description></item>
    /// <item><description>hashing the payload with SHA-256;</description></item>
    /// <item><description>verifying an ECDSA signature (secp256k1) over the hash using the provided public key.</description></item>
    /// </list>
    /// </remarks>
    public static class NameClaimVerifier
    {
        /// <summary>
        /// Verifies a claim-v1 signature for a name and a public key.
        /// </summary>
        /// <param name="name">
        /// Name value as provided by the caller. It will be canonicalized as part of payload construction
        /// (see <see cref="NameClaim.BuildClaimV1Payload"/>).
        /// </param>
        /// <param name="publicKeyBase32E">Compressed secp256k1 public key encoded with Base32E (email-safe Base32).</param>
        /// <param name="signatureBase64">DER-encoded ECDSA signature (<c>r</c>, <c>s</c>) encoded as Base64.</param>
        /// <returns><c>true</c> when signature is valid for the constructed claim-v1 payload; otherwise <c>false</c>.</returns>
        public static bool VerifyClaimV1Signature(string name, string publicKeyBase32E, string signatureBase64)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(publicKeyBase32E) || string.IsNullOrWhiteSpace(signatureBase64))
            {
                return false;
            }

            // Signature is expected to be Base64 of ASN.1 DER sequence with two integers: (r, s).
            byte[] sig;
            try
            {
                sig = Convert.FromBase64String(signatureBase64);
            }
            catch (FormatException)
            {
                return false;
            }

            BigInteger r;
            BigInteger s;
            try
            {
                var seq = (Asn1Sequence)Asn1Object.FromByteArray(sig);
                if (seq.Count != 2)
                {
                    return false;
                }

                r = ((DerInteger)seq[0]).PositiveValue;
                s = ((DerInteger)seq[1]).PositiveValue;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidCastException || ex is System.IO.EndOfStreamException)
            {
                return false;
            }

            if (r.SignValue <= 0 || s.SignValue <= 0)
            {
                return false;
            }

            ECPublicKeyParameters pub;
            X9ECParameters curveParams;
            try
            {
                // Public key is provided as a Base32E-encoded compressed EC point on secp256k1.
                var curveOid = ECNamedCurveTable.GetOid("secp256k1");
                curveParams = ECNamedCurveTable.GetByOid(curveOid);

                var compressed = Base32EConverter.FromEmailBase32(publicKeyBase32E);
                var q = curveParams.Curve.DecodePoint(compressed);
                var domain = new ECDomainParameters(curveParams.Curve, curveParams.G, curveParams.N, curveParams.H);
                pub = new ECPublicKeyParameters(q, domain);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
            {
                return false;
            }

            var halfCurveOrder = curveParams.N.ShiftRight(1);
            if (s.CompareTo(halfCurveOrder) > 0)
            {
                return false;
            }

            // Payload is hashed prior to ECDSA verification to match protocol rules.
            var payload = NameClaim.BuildClaimV1Payload(name, publicKeyBase32E);
            var payloadUtf8 = Encoding.UTF8.GetBytes(payload);
            var hash = DigestUtilities.CalculateDigest("SHA-256", payloadUtf8);

            var verifier = new ECDsaSigner();
            verifier.Init(false, pub);
            return verifier.VerifySignature(hash, r, s);
        }
    }
}
