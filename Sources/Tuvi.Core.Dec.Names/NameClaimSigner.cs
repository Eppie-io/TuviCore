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
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace Tuvi.Core.Dec.Names
{
    /// <summary>
    /// Creates signatures for the claim-v1 name registration protocol.
    /// </summary>
    /// <remarks>
    /// This class produces deterministic ECDSA signatures using RFC 6979 (HMAC-DRBG with SHA-256)
    /// and normalizes signatures to low-S form to prevent signature malleability.
    /// </remarks>
    public static class NameClaimSigner
    {
        /// <summary>
        /// Signs a claim-v1 payload (built from <paramref name="name"/> and <paramref name="publicKeyBase32E"/>)
        /// with the provided secp256k1 private key.
        /// </summary>
        /// <param name="name">
        /// Name value as provided by the caller. It will be canonicalized as part of payload construction
        /// using <see cref="NameClaim.CanonicalizeName"/> (lowercase, trimmed, ".test" suffix appended if missing).
        /// </param>
        /// <param name="publicKeyBase32E">Compressed secp256k1 public key encoded with Base32E (email-safe Base32).</param>
        /// <param name="privateKey">secp256k1 private key parameters.</param>
        /// <returns>DER-encoded ECDSA signature (r, s) encoded as Base64.</returns>
        /// <remarks>
        /// <para>
        /// The signature is computed over SHA-256 hash of the UTF-8 encoded payload built by
        /// <see cref="NameClaim.BuildClaimV1Payload"/>. The payload format is:
        /// <c>claim-v1\nname=&lt;canonicalized_name&gt;\npublicKey=&lt;key&gt;</c>.
        /// </para>
        /// <para>
        /// The S component of the signature is normalized to low-S form (S ≤ N/2) to reduce
        /// signature malleability, where N is the order of the secp256k1 curve.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="name"/> or <paramref name="publicKeyBase32E"/> is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="privateKey"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when signature generation fails.
        /// </exception>
        public static string SignClaimV1(string name, string publicKeyBase32E, ECPrivateKeyParameters privateKey)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name is empty.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(publicKeyBase32E))
            {
                throw new ArgumentException("Public key is empty.", nameof(publicKeyBase32E));
            }

            if (privateKey is null)
            {
                throw new ArgumentNullException(nameof(privateKey));
            }

            EnsureSecp256k1PrivateKey(privateKey);

            var payload = NameClaim.BuildClaimV1Payload(name, publicKeyBase32E);
            var payloadUtf8 = Encoding.UTF8.GetBytes(payload);
            var hash = DigestUtilities.CalculateDigest("SHA-256", payloadUtf8);

            var signer = new ECDsaSigner(new HMacDsaKCalculator(new Sha256Digest()));
            signer.Init(true, privateKey);
            var sig = signer.GenerateSignature(hash);

            if (sig is null || sig.Length != 2)
            {
                throw new InvalidOperationException("Failed to generate ECDSA signature.");
            }

            var r = sig[0];
            var s = sig[1];

            // Normalize S to low-S form to reduce malleability.
            BigInteger n = privateKey.Parameters.N;
            BigInteger halfCurveOrder = n.ShiftRight(1);
            if (s.CompareTo(halfCurveOrder) > 0)
            {
                s = n.Subtract(s);
            }

            var seq = new DerSequence(
                new DerInteger(r),
                new DerInteger(s));

            return Convert.ToBase64String(seq.GetDerEncoded());
        }

        private static void EnsureSecp256k1PrivateKey(ECPrivateKeyParameters privateKey)
        {
            if (privateKey.Parameters is null)
            {
                throw new ArgumentException("Private key must include domain parameters (secp256k1).", nameof(privateKey));
            }

            X9ECParameters curve = SecNamedCurves.GetByName("secp256k1")
                ?? throw new InvalidOperationException("Failed to load secp256k1 parameters.");

            if (!privateKey.Parameters.N.Equals(curve.N))
            {
                throw new ArgumentException("Private key must be a secp256k1 key.", nameof(privateKey));
            }
        }
    }
}
