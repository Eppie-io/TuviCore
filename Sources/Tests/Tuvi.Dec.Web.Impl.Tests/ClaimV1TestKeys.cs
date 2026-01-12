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
using KeyDerivation;
using KeyDerivation.Keys;
using KeyDerivationLib;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Tuvi.Base32EConverterLib;
using TuviPgpLibImpl;

namespace Tuvi.Core.Dec.Web.Impl.Tests
{
    internal static class ClaimV1TestKeys
    {
        internal sealed class ClaimKeyMaterial
        {
            public string PublicKeyBase32E { get; }
            public ECPrivateKeyParameters PrivateKey { get; }

            public ClaimKeyMaterial(string publicKeyBase32E, ECPrivateKeyParameters privateKey)
            {
                PublicKeyBase32E = publicKeyBase32E;
                PrivateKey = privateKey;
            }
        }

        private sealed class TestKeyDerivationDetailsProvider : IKeyDerivationDetailsProvider
        {
            public string GetSaltPhrase() => "Salt";
            public int GetSeedPhraseLength() => 12;
            public System.Collections.Generic.Dictionary<SpecialPgpKeyType, string> GetSpecialPgpKeyIdentities() => throw new NotImplementedException();
        }

        // This is a test seed phrase, never use it outside of tests.
        private static readonly string[] TestSeedPhrase =
        {
            "abandon", "abandon", "abandon", "abandon",
            "abandon", "abandon", "abandon", "abandon",
            "abandon", "abandon", "abandon", "abandon"
        };

        private static MasterKey CreateMasterKey()
        {
            var factory = new MasterKeyFactory(new TestKeyDerivationDetailsProvider());
            factory.RestoreSeedPhrase(TestSeedPhrase);
            return factory.GetMasterKey();
        }

        private const int CoinType = 3630; // Eppie BIP44 coin type
        private const int Channel = 10;    // Email channel
        private const int KeyIndex = 0;    // Key index

        public static ClaimKeyMaterial GenerateKey(int accountIndex = 0)
        {
            if (accountIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(accountIndex));
            }

            using var masterKey = CreateMasterKey();

            // Public key (Base32E) via existing deterministic derivation
            var pub = EccPgpContext.GenerateEccPublicKey(masterKey, CoinType, accountIndex, Channel, KeyIndex);
            var publicKeyBase32E = Base32EConverter.ToEmailBase32(pub.Q.GetEncoded(true));

            // Matching private scalar for signing (secp256k1)
            using var dk = DerivationKeyFactory.CreatePrivateDerivationKeyBip44(masterKey, CoinType, accountIndex, Channel, KeyIndex);
            var priv = new ECPrivateKeyParameters(new BigInteger(1, dk.Scalar.ToArray()), Secp256k1.DomainParams);
            return new ClaimKeyMaterial(publicKeyBase32E, priv);
        }

        public static string SignClaimV1(string name, ClaimKeyMaterial key)
        {
            return Names.NameClaimSigner.SignClaimV1(name, key.PublicKeyBase32E, key.PrivateKey);
        }

        public static (string PublicKeyBase32E, string SignatureBase64) CreateSignature(string name)
        {
            var key = GenerateKey();
            var sig = SignClaimV1(name, key);
            return (key.PublicKeyBase32E, sig);
        }
    }
}
