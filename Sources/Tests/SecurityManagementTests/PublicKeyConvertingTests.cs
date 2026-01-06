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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;
using NUnit.Framework;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Parameters;
using Tuvi.Base32EConverterLib;
using Tuvi.Core.Dec;
using Tuvi.Core.Entities;
using Tuvi.Core.Utils;
using TuviPgpLibImpl;

namespace SecurityManagementTests
{
    public class PublicKeyConvertingTests
    {
        private sealed class TestNoOpResolver : IEppieNameResolver
        {
            public Task<string> ResolveAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<string>(null);
        }
        private readonly PublicKeyService _svc = PublicKeyService.CreateDefault(new TestNoOpResolver());
        private static TuviPgpContext InitializeTuviPgpContext()
        {
            var keyStorage = new MockPgpKeyStorage().Get();
            var context = new TuviPgpContext(keyStorage);
            context.LoadContextAsync().Wait();
            return context;
        }

        [Test]
        public void ECPubKeyConverting()
        {
            for (int i = 0; i < 50; i++)
            {
                var publicKey = EccPgpContext.GenerateEccPublicKey(TestData.MasterKey, 0, 0, 0, i);

                byte[] publicKeyAsBytes = publicKey.Q.GetEncoded(true);
                string emailName = Base32EConverter.ToEmailBase32(publicKeyAsBytes);
                var reconvertedPublicKeyAsBytes = Base32EConverter.FromEmailBase32(emailName);

                Assert.That(publicKeyAsBytes, Is.EqualTo(reconvertedPublicKeyAsBytes));
            }
        }

        [Test]
        public void ECPubKeyParametersConverting()
        {
            for (int i = 0; i < 50; i++)
            {
                var publicKey = EccPgpContext.GenerateEccPublicKey(TestData.MasterKey, 0, 0, 0, i);

                string emailName = _svc.Encode(publicKey);
                var reconvertedPublicKey = _svc.Decode(emailName);

                Assert.That(publicKey, Is.EqualTo(reconvertedPublicKey));
            }
        }

        [Test]
        public void PublicKeyImportTest()
        {
            const string EmailName = "agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c";
            var reconvertedPublicKey = _svc.Decode(EmailName);

            PgpPublicKeyRing publicKeyRing = EccPgpContext.CreatePgpPublicKeyRing(reconvertedPublicKey, reconvertedPublicKey, EmailName);

            using TuviPgpContext ctx = InitializeTuviPgpContext();
            ctx.Import(publicKeyRing);

            Assert.That(1, Is.EqualTo(ctx.PublicKeyRingBundle.Count), "Public key was not imported");
        }

        [Test]
        public void PublicKeyEncryptDecryptTest()
        {
            using Stream inputData = new MemoryStream();
            using Stream encryptedData = new MemoryStream();
            using var messageBody = new TextPart() { Text = TestData.TextContent };
            messageBody.WriteTo(inputData);
            inputData.Position = 0;

            const string EmailName = "ae5ky7ah5gepibreyyts88vcdenmhk786cmec8xyjburepk5bxufc";

            ECPublicKeyParameters reconvertedPublicKey = _svc.Decode(EmailName);

            PgpPublicKeyRing publicKeyRing = EccPgpContext.CreatePgpPublicKeyRing(reconvertedPublicKey, reconvertedPublicKey, EmailName);
            PgpPublicKey publicKey = publicKeyRing.GetPublicKeys().FirstOrDefault(x => x.IsEncryptionKey);

            using EccPgpContext ctx = InitializeTuviPgpContext();
            var encryptedMime = ctx.Encrypt(new List<PgpPublicKey> { publicKey }, inputData);

            ctx.GeneratePgpKeysByTagOld(TestData.MasterKey, TestData.GetAccount().GetPgpIdentity(), TestData.GetAccount().GetPgpIdentity());

            encryptedMime.WriteTo(encryptedData);
            encryptedData.Position = 0;

            var mime = ctx.Decrypt(encryptedData);
            var decryptedBody = mime as TextPart;

            Assert.That(
                TestData.TextContent.SequenceEqual(decryptedBody?.Text ?? string.Empty),
                Is.True,
                "Decrypted content is corrupted");
        }

        [Test]
        public void EmailNameConvertingNullEmailNameThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _svc.Decode(null), "Email name can not be a null.");
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(15)]
        public void ECPubKeyConvertingTooLongPubKeyThrowArgumentException(int childKeyNum)
        {
            var publicKey = EccPgpContext.GenerateEccPublicKey(TestData.MasterKey, 0, 0, 0, childKeyNum);

            byte[] publicKeyAsBytes = publicKey.Q.GetEncoded(false);

            Assert.Throws<ArgumentException>(() => Base32EConverter.ToEmailBase32(publicKeyAsBytes));
        }

        [TestCase("")]
        [TestCase("auubcdefg")]
        [TestCase("zxy8pt5roasd3mefe")]
        [TestCase("abracadabraabracadabraabracadabraabracadabraabracada")]
        [TestCase("abracadabraabracadabraabracadabraabracadabraabracadabr")]
        [TestCase("abracadabraabracadabraabracadabraabracadabraabracadabraabracadabraabracadabra")]
        public void EmailNameConvertingWrongEmailNameLengthThrowArgumentException(string emailName)
        {
            Assert.Throws<ArgumentException>(() => _svc.Decode(emailName), "Incorrect length of email name.");
        }

        [TestCase("abracadabraabracadabraabracadabraabracadabraabracadab")]
        [TestCase("adracadabraabracadabraabracadabraabracadabraabracadab")]
        [TestCase("apracadabraabracadabraabracadabraabracadabraabracadab")]
        [TestCase("atracadabraabracadabraabracadabraabracadabraabracadab")]
        [TestCase("a2racadabraabracadabraabracadabraabracadabraabracadab")]
        [TestCase("b2racadabraabracadabraabracadabraabracadabraabracadab")]
        [TestCase("r2racadabraabracadabraabracadabraabracadabraabracadab")]
        [TestCase("xgwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c")]
        [TestCase("6gwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c")]
        public void EmailNameConvertingWrongFormatThrowArgumentException(string emailName)
        {
            Assert.Throws<FormatException>(() => _svc.Decode(emailName), "Invalid point format. Encoded public key should start with 0x02 or 0x03.");
        }

        [Test]
        public void ToPublicKeyBase32ENullPublicKeyThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _svc.Encode((ECPublicKeyParameters)null));
        }

        [Test]
        public void ToPublicKeyBase32EWithMasterKeyAndDerivationParamsGeneratesCorrectBase32E()
        {
            var masterKey = TestData.MasterKey; // Assuming TestData.MasterKey is available as in existing tests
            const int Coin = 0;
            const int Account = 0;
            const int Channel = 0;
            const int Index = 1;

            // Generate expected public key parameters using the underlying method
            var expectedPublicKey = EccPgpContext.GenerateEccPublicKey(masterKey, Coin, Account, Channel, Index);
            var expectedBase32E = _svc.Encode(expectedPublicKey);

            var result = _svc.DeriveEncoded(masterKey, Coin, Account, Channel, Index);

            Assert.That(result, Is.EqualTo(expectedBase32E));
        }

        [Test]
        public void ToPublicKeyBase32ENullMasterKeyWithDerivationParamsThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _svc.DeriveEncoded(null, 0, 0, 0, 0));
        }

        [Test]
        public void ToPublicKeyBase32EWithMasterKeyAndKeyTagGeneratesCorrectBase32E()
        {
            var masterKey = TestData.MasterKey;
            const string KeyTag = "test-tag";

            // Generate expected public key parameters using the underlying method
            var expectedPublicKey = EccPgpContext.GenerateEccPublicKey(masterKey, KeyTag);
            var expectedBase32E = _svc.Encode(expectedPublicKey);

            var result = _svc.DeriveEncoded(masterKey, KeyTag);

            Assert.That(result, Is.EqualTo(expectedBase32E));
        }

        [Test]
        public void ToPublicKeyBase32ENullMasterKeyWithKeyTagThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _svc.DeriveEncoded(null, "test-tag"));
        }

        [Test]
        public void ToPublicKeyBase32ENullKeyTagThrowsArgumentNullException()
        {
            var masterKey = TestData.MasterKey;
            Assert.Throws<ArgumentException>(() => _svc.DeriveEncoded(masterKey, null));
        }

        [Test]
        public void ToPublicKeyBase32EEmptyKeyTagThrowsArgumentException()
        {
            var masterKey = TestData.MasterKey;
            Assert.Throws<ArgumentException>(() => _svc.DeriveEncoded(masterKey, string.Empty));
        }

        [Test]
        public async Task ToPublicKeyBase32EAsyncEppieNetworkReturnsDecentralizedAddress()
        {
            var publicKey = EccPgpContext.GenerateEccPublicKey(TestData.MasterKey, 0, 0, 0, 1);
            var publicKeyBase32E = _svc.Encode(publicKey);
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, publicKeyBase32E);

            var result = await _svc.GetEncodedByEmailAsync(email, default).ConfigureAwait(false);

            Assert.That(publicKeyBase32E, Is.EqualTo(email.DecentralizedAddress));
        }

        [Test]
        public void ToPublicKeyBase32EAsyncUnsupportedNetworkThrowsArgumentException()
        {
            var publicKey = EccPgpContext.GenerateEccPublicKey(TestData.MasterKey, 0, 0, 0, 1);
            var publicKeyBase32E = _svc.Encode(publicKey);
            Assert.Throws<ArgumentException>(() => EmailAddress.CreateDecentralizedAddress((NetworkType)999, publicKeyBase32E));
        }

        [Test]
        public void ToPublicKeyBase32EAsyncNullEmailThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() => _svc.GetEncodedByEmailAsync(null, default));
        }

        [Test]
        public async Task ToPublicKeyAsyncEppieNetworkReturnsPublicKeyParameters()
        {
            const string PublicKeyBase32E = "agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c";
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, PublicKeyBase32E);
            var expectedPublicKey = _svc.Decode(email.DecentralizedAddress);
            var result = await _svc.GetByEmailAsync(email, default).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(expectedPublicKey));
        }

        [Test]
        public void ToPublicKeyAsyncNullEmailThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() => _svc.GetByEmailAsync(null, default));
        }

        [Test]
        public void ToPublicKeyInvalidCurveOidThrowsException()
        {
            const string InvalidEncodedKey = "invalidbase32ethatdecodestowrongpoint";
            Assert.Throws<ArgumentException>(() => _svc.Decode(InvalidEncodedKey));
        }
    }
}
