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
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC;
using Tuvi.Base32EConverterLib;

namespace Tuvi.Core.Utils
{
    /// <summary>
    /// Encodes and decodes EC public keys to/from textual representation.
    /// </summary>
    public interface IEcPublicKeyCodec
    {
        string Encode(ECPublicKeyParameters publicKey);
        ECPublicKeyParameters Decode(string encoded);
    }

    /// <summary>
    /// secp256k1 compressed point encoded with Base32E (email friendly) codec.
    /// </summary>
    internal sealed class Secp256k1CompressedBase32ECodec : IEcPublicKeyCodec
    {
        private const string CurveName = "secp256k1";
        private const string Algorithm = "EC";
        private const int ExpectedEmailNameLength = 53;
        private const byte PrefixEven = 0x02;
        private const byte PrefixOdd = 0x03;
        private static readonly DerObjectIdentifier CurveOid = ECNamedCurveTable.GetOid(CurveName);
        private static readonly X9ECParameters CurveParams = ECNamedCurveTable.GetByOid(CurveOid);
        private static readonly ECCurve Curve = CurveParams.Curve;

        public string Encode(ECPublicKeyParameters publicKey)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            byte[] compressed = publicKey.Q.GetEncoded(true);
            return Base32EConverter.ToEmailBase32(compressed);
        }

        public ECPublicKeyParameters Decode(string encoded)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            if (encoded.Length != ExpectedEmailNameLength)
            {
                throw new ArgumentException("Incorrect length of encoded key.", nameof(encoded));
            }

            byte[] bytes = Base32EConverter.FromEmailBase32(encoded);
            if (bytes.Length == 0 || (bytes[0] != PrefixEven && bytes[0] != PrefixOdd))
            {
                throw new FormatException("Wrong format. Encoded compressed public keys should start with 0x02 or 0x03.");
            }

            var point = Curve.DecodePoint(bytes);
            return new ECPublicKeyParameters(Algorithm, point, CurveOid);
        }
    }
}
