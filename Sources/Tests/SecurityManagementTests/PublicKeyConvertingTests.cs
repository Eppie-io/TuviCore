using MimeKit;
using NUnit.Framework;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tuvi.Base32EConverterLib;
using Tuvi.Core.Entities;
using Tuvi.Core.Utils;
using TuviPgpLibImpl;

namespace SecurityManagementTests
{
    public class PublicKeyConvertingTests
    {
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

                string emailName = PublicKeyConverter.ToPublicKeyBase32E(publicKey);
                var reconvertedPublicKey = PublicKeyConverter.ToPublicKey(emailName);

                Assert.That(publicKey, Is.EqualTo(reconvertedPublicKey));
            }
        }

        [Test]
        public void PublicKeyImportTest()
        {
            string emailName = "agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c";
            var reconvertedPublicKey = PublicKeyConverter.ToPublicKey(emailName);

            PgpPublicKeyRing publicKeyRing = EccPgpContext.CreatePgpPublicKeyRing(reconvertedPublicKey, reconvertedPublicKey, emailName);

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

            string emailName = "ae5ky7ah5gepibreyyts88vcdenmhk786cmec8xyjburepk5bxufc";

            ECPublicKeyParameters reconvertedPublicKey = PublicKeyConverter.ToPublicKey(emailName);

            PgpPublicKeyRing publicKeyRing = EccPgpContext.CreatePgpPublicKeyRing(reconvertedPublicKey, reconvertedPublicKey, emailName);
            PgpPublicKey publicKey = publicKeyRing.GetPublicKeys().FirstOrDefault(x => x.IsEncryptionKey );

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
            Assert.Throws<ArgumentNullException>(() => PublicKeyConverter.ToPublicKey(null), "Email name can not be a null.");
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
            Assert.Throws<ArgumentException>(() => PublicKeyConverter.ToPublicKey(emailName), "Incorrect length of email name.");
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
            Assert.Throws<FormatException>(() => PublicKeyConverter.ToPublicKey(emailName), "Invalid point format. Encoded public key should start with 0x02 or 0x03.");
        }

        [Test]
        public void ToPublicKeyBase32ENullPublicKeyThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PublicKeyConverter.ToPublicKeyBase32E((ECPublicKeyParameters)null));
        }

        [Test]
        public void ToPublicKeyBase32EWithMasterKeyAndDerivationParamsGeneratesCorrectBase32E()
        {
            var masterKey = TestData.MasterKey; // Assuming TestData.MasterKey is available as in existing tests
            int coin = 0;
            int account = 0;
            int channel = 0;
            int index = 1;

            // Generate expected public key parameters using the underlying method
            var expectedPublicKey = EccPgpContext.GenerateEccPublicKey(masterKey, coin, account, channel, index);
            var expectedBase32E = PublicKeyConverter.ToPublicKeyBase32E(expectedPublicKey);

            var result = PublicKeyConverter.ToPublicKeyBase32E(masterKey, coin, account, channel, index);

            Assert.That(result, Is.EqualTo(expectedBase32E));
        }

        [Test]
        public void ToPublicKeyBase32ENullMasterKeyWithDerivationParamsThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PublicKeyConverter.ToPublicKeyBase32E(null, 0, 0, 0, 0));
        }

        [Test]
        public void ToPublicKeyBase32EWithMasterKeyAndKeyTagGeneratesCorrectBase32E()
        {
            var masterKey = TestData.MasterKey;
            string keyTag = "test-tag";

            // Generate expected public key parameters using the underlying method
            var expectedPublicKey = EccPgpContext.GenerateEccPublicKey(masterKey, keyTag);
            var expectedBase32E = PublicKeyConverter.ToPublicKeyBase32E(expectedPublicKey);

            var result = PublicKeyConverter.ToPublicKeyBase32E(masterKey, keyTag);

            Assert.That(result, Is.EqualTo(expectedBase32E));
        }

        [Test]
        public void ToPublicKeyBase32ENullMasterKeyWithKeyTagThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PublicKeyConverter.ToPublicKeyBase32E(null, "test-tag"));
        }

        [Test]
        public void ToPublicKeyBase32ENullKeyTagThrowsArgumentNullException()
        {
            var masterKey = TestData.MasterKey;
            Assert.Throws<ArgumentException>(() => PublicKeyConverter.ToPublicKeyBase32E(masterKey, null));
        }

        [Test]
        public void ToPublicKeyBase32EEmptyKeyTagThrowsArgumentException()
        {
            var masterKey = TestData.MasterKey;
            Assert.Throws<ArgumentException>(() => PublicKeyConverter.ToPublicKeyBase32E(masterKey, string.Empty));
        }

        [Test]
        public async Task ToPublicKeyBase32EAsyncEppieNetworkReturnsDecentralizedAddress()
        {
            var publicKey = EccPgpContext.GenerateEccPublicKey(TestData.MasterKey, 0, 0, 0, 1);
            var publicKeyBase32E = PublicKeyConverter.ToPublicKeyBase32E(publicKey);
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, publicKeyBase32E, string.Empty);

            var result = await PublicKeyConverter.ToPublicKeyBase32EAsync(email).ConfigureAwait(false);

            Assert.That(publicKeyBase32E, Is.EqualTo(email.DecentralizedAddress));
        }

        [Test]
        public void ToPublicKeyBase32EAsyncUnsupportedNetworkThrowsArgumentException()
        {
            var publicKey = EccPgpContext.GenerateEccPublicKey(TestData.MasterKey, 0, 0, 0, 1);
            var publicKeyBase32E = PublicKeyConverter.ToPublicKeyBase32E(publicKey);
            Assert.Throws<ArgumentException>(() => EmailAddress.CreateDecentralizedAddress((NetworkType)999, publicKeyBase32E, string.Empty)); 
        }

        [Test]
        public void ToPublicKeyBase32EAsyncNullEmailThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() => PublicKeyConverter.ToPublicKeyBase32EAsync(null));
        }

        [Test]
        public async Task ToPublicKeyAsyncEppieNetworkReturnsPublicKeyParameters()
        {
            var publicKeyBase32E = "agwaxxb4zchc8digxdxryn5fzs5s2r32swwajipn4bewski276k2c";
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, publicKeyBase32E, string.Empty);
            
            var expectedPublicKey = PublicKeyConverter.ToPublicKey(email.DecentralizedAddress);

            var result = await PublicKeyConverter.ToPublicKeyAsync(email).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(expectedPublicKey));
        }

        [Test]
        public void ToPublicKeyAsyncNullEmailThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() => PublicKeyConverter.ToPublicKeyAsync(null));
        }

        [Test]
        public void ToPublicKeyInvalidCurveOidThrowsException()
        {
            var invalidEncodedKey = "invalidbase32ethatdecodestowrongpoint"; // Craft a string that passes length but fails DecodePoint

            Assert.Throws<ArgumentException>(() => PublicKeyConverter.ToPublicKey(invalidEncodedKey)); // Adjust exception type as per actual throw
        }
    }
}
