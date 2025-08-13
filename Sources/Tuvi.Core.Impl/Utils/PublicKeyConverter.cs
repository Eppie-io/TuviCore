using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
using System;
using Tuvi.Base32EConverterLib;

namespace Tuvi.Core.Utils
{
    public static class PublicKeyConverter
    {
        private const int ExpectedEmailNameLength = 53;
        private const int CaseCompressionYTildeIsFalse = 2;
        private const int CaseCompressionYTildeIsTrue = 3;

        /// <summary>
        /// Converts public key into email's name using Base32EConverter.
        /// </summary>
        /// <param name="publicKey">Public key.</param>
        /// <returns>Email name.</returns>
        public static string ConvertPublicKeyToEmailName(ECPublicKeyParameters publicKey)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            byte[] publicKeyAsBytes = publicKey.Q.GetEncoded(true);
            return Base32EConverter.ToEmailBase32(publicKeyAsBytes);
        }

        /// <summary>
        /// Converts email name into public key
        /// </summary>
        /// <param name="name">Email name.</param>
        /// <returns>Public key.</returns>
        public static ECPublicKeyParameters ConvertEmailNameToPublicKey(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Length != ExpectedEmailNameLength)
            {
                throw new ArgumentException("Incorrect length of email name.", nameof(name));
            }

            const string BitcoinEllipticCurveName = "secp256k1";
            const string algorithm = "EC";

            DerObjectIdentifier curveOid = ECNamedCurveTable.GetOid(BitcoinEllipticCurveName);
            ECKeyGenerationParameters keyParams = new ECKeyGenerationParameters(curveOid, new SecureRandom());

            var encodedKey = Base32EConverter.FromEmailBase32(name);

            if (encodedKey[0] == CaseCompressionYTildeIsFalse || encodedKey[0] == CaseCompressionYTildeIsTrue)
            {
                ECCurve curve = keyParams.DomainParameters.Curve;
                var point = curve.DecodePoint(encodedKey);

                return new ECPublicKeyParameters(algorithm, point, keyParams.PublicKeyParamSet);
            }
            else
            {
                throw new FormatException("Wrong format. Encoded compressed public keys should start with 0x02 or 0x03.");
            }
        }
    }
}
