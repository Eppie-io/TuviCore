using KeyDerivation.Keys;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
using System;
using System.Threading.Tasks;
using Tuvi.Base32EConverterLib;
using Tuvi.Core.Dec.Bitcoin.TestNet4;
using Tuvi.Core.Entities;
using TuviPgpLibImpl;

namespace Tuvi.Core.Utils
{
    /// <summary>
    /// Provides methods for converting public keys to and from Base32E format.
    /// </summary>
    public static class PublicKeyConverter
    {
        private const int ExpectedEmailNameLength = 53;
        private const int CaseCompressionYTildeIsFalse = 2;
        private const int CaseCompressionYTildeIsTrue = 3;

        /// <summary>
        /// Converts an EC public key to its Base32E email representation.
        /// </summary>
        /// <param name="publicKey">EC public key parameters.</param>
        /// <returns>Public key encoded in Base32E format.</returns>
        public static string ToPublicKeyBase32E(ECPublicKeyParameters publicKey)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            byte[] publicKeyAsBytes = publicKey.Q.GetEncoded(true);
            return Base32EConverter.ToEmailBase32(publicKeyAsBytes);
        }

        /// <summary>
        /// Generates a public key from a master key and derivation parameters, and converts it to Base32E format.
        /// </summary>
        public static string ToPublicKeyBase32E(MasterKey masterKey, int coin, int account, int channel, int index)
        {
            var publicKey = EccPgpContext.GenerateEccPublicKey(masterKey, coin, account, channel, index);
            return ToPublicKeyBase32E(publicKey);
        }

        /// <summary>
        /// Generates a public key from a master key and a key tag, and converts it to Base32E format.
        /// </summary>
        public static string ToPublicKeyBase32E(MasterKey masterKey, string keyTag)
        {
            var publicKey = EccPgpContext.GenerateEccPublicKey(masterKey, keyTag);
            return ToPublicKeyBase32E(publicKey);
        }

        /// <summary>
        /// Retrieves a public key associated with the given email asynchronously and returns it in Base32E format.
        /// </summary>
        public static Task<string> ToPublicKeyBase32EAsync(EmailAddress email)
        {
            if (email == null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            return GetPublicKeyAsync(email);
        }

        /// <summary>
        /// Retrieves a public key associated with the given email asynchronously and returns it as ECPublicKeyParameters.
        /// </summary>
        public static async Task<ECPublicKeyParameters> ToPublicKeyAsync(EmailAddress email)
        {
            if (email == null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            var publicKey = await GetPublicKeyAsync(email).ConfigureAwait(false);

            return ToPublicKey(publicKey);
        }

        /// <summary>
        /// Converts a Base32E-encoded public key string to ECPublicKeyParameters.
        /// </summary>
        /// <param name="publicKey">Public key in Base32E format.</param>
        /// <returns>EC public key parameters.</returns>
        public static ECPublicKeyParameters ToPublicKey(string publicKey)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            if (publicKey.Length != ExpectedEmailNameLength)
            {
                throw new ArgumentException("Incorrect length of email name.", nameof(publicKey));
            }

            const string BitcoinEllipticCurveName = "secp256k1";
            const string algorithm = "EC";

            DerObjectIdentifier curveOid = ECNamedCurveTable.GetOid(BitcoinEllipticCurveName);
            ECKeyGenerationParameters keyParams = new ECKeyGenerationParameters(curveOid, new SecureRandom());

            var encodedKey = Base32EConverter.FromEmailBase32(publicKey);

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

        /// <summary>
        /// Retrieves a public key string associated with the given email address from the appropriate network.
        /// </summary>
        /// <param name="email">Email address with network type.</param>
        /// <returns>Public key string in Base32E format.</returns>
        /// <exception cref="NoPublicKeyException">Thrown when a Bitcoin address does not have a public key.</exception>
        /// <exception cref="NotSupportedException">Thrown when the network type is not supported.</exception>
        private static async Task<string> GetPublicKeyAsync(EmailAddress email)
        {
            if (email.Network == NetworkType.Bitcoin)
            {
                var bitcoinAddress = email.DecentralizedAddress;
                var publicKey = await Tools.RetrievePublicKeyAsync(bitcoinAddress).ConfigureAwait(false);

                if (publicKey is null)
                {
                    throw new NoPublicKeyException(email, $"Public key is not found for the {bitcoinAddress} Bitcoin address");
                }

                return publicKey;
            }
            else if (email.Network == NetworkType.Eppie)
            {
                return email.DecentralizedAddress;
            }

            throw new NotSupportedException($"Network type {email.Network} is not supported for Decentralized MailBox.");
        }
    }
}
