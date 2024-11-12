using NUnit.Framework;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tuvi.Core.DataStorage.Impl;
using Tuvi.Core.Entities;
using Tuvi.Proton;
using Tuvi.Proton.Impl;

namespace Tuvi.Core.Mail.Impl.Tests
{
    #region Helpers
    struct MyRandomGenerator : IRandomGenerator
    {
        byte seed;

        public MyRandomGenerator(byte seed)
        {
            this.seed = seed;
        }

        public void AddSeedMaterial(byte[] seed)
        {

        }

        public void AddSeedMaterial(ReadOnlySpan<byte> seed)
        {

        }

        public void AddSeedMaterial(long seed)
        {

        }

        public void NextBytes(byte[] bytes)
        {
            NextBytes(bytes, 0, bytes.Length);
        }

        public void NextBytes(byte[] bytes, int start, int len)
        {
            NextBytes(bytes.AsSpan(start, len));
        }

        public void NextBytes(Span<byte> bytes)
        {
            for (int i = 0; i < bytes.Length; ++i)
            {
                bytes[i] = seed++;
            }
        }


    }

    #endregion

    public class ProtonMailBoxTests
    {
        private EmailAddress InternalAddress = new EmailAddress(MyOpenPgpContext.InternalUserId, "internal address");
        private static PgpKeyPair GenerateECDHKeys()
        {
            var generator = new MyRandomGenerator(0);
            var random = new SecureRandom(generator);
            X25519KeyPairGenerator dhKp = new X25519KeyPairGenerator();
            dhKp.Init(new X25519KeyGenerationParameters(random));

            return new PgpKeyPair(PublicKeyAlgorithmTag.ECDH, dhKp.GenerateKeyPair(), DateTime.UtcNow);
        }

        private static PgpKeyPair GenerateEdDsaKeys()
        {
            var generator = new MyRandomGenerator(0);
            var random = new SecureRandom(generator);
            Ed25519KeyPairGenerator dhKp = new Ed25519KeyPairGenerator();
            dhKp.Init(new Ed25519KeyGenerationParameters(random));

            return new PgpKeyPair(PublicKeyAlgorithmTag.EdDsa, dhKp.GenerateKeyPair(), DateTime.UtcNow);
        }

        private static byte[] Join(byte[] array1, byte[] array2)
        {
            var res = new byte[array1.Length + array2.Length];
            Array.Copy(array1, res, array1.Length);
            Array.Copy(array2, 0, res, array1.Length, array2.Length);
            return res;
        }

        [Test]
        public void SessionKeyTest()
        {
            var keyPairSender = GenerateECDHKeys();
            var keyPairRecipient = GenerateECDHKeys();

            string body = "my body";

            using var context = new MyOpenPgpContext();
            context.AddKeysToTemporalKeyRing(keyPairSender.PublicKey, keyPairSender.PrivateKey);

            (var decSessionKey, var encBody) = Crypto.EncryptAndSignSplit(context, InternalAddress, Encoding.UTF8.GetBytes(body));

            var encKeyRecepient = Crypto.EncryptSessionKey(keyPairRecipient.PublicKey, decSessionKey);
            var joined = Join(encKeyRecepient, encBody);


            var pgpF = new PgpObjectFactory(joined);
            var pgpObject = pgpF.NextPgpObject();
            var encList = pgpObject as PgpEncryptedDataList;
            var encP = (PgpPublicKeyEncryptedData)encList[0];

            var f = new PgpObjectFactory(encP.GetDataStream(keyPairRecipient.PrivateKey));
            pgpObject = f.NextPgpObject();
            if (pgpObject is PgpCompressedData compressed)
            {
                f = new PgpObjectFactory(compressed.GetDataStream());
            }

            var literals = f.FilterPgpObjects<PgpLiteralData>();
            string receiverBody = Encoding.UTF8.GetString(Streams.ReadAll(literals[0].GetDataStream()));
            Assert.IsTrue(body == receiverBody);
        }

        [Test]
        [TestCase("A15VUlxvQ+3e0bo9Jqg/7iFaDdN+YnToSxy4tHWWuNM=", "0koBgPHji3nHD4mRXl5bv+kLVzzS6wIr7wZb5/MKuSp2RfV+FgMpNWDJSkcXcCkkWOzd05dCywr0VK11EFuCLS41B7ZTVkcAiLSGAQ==", "My body")]
        [TestCase("AbVOjLdx22soYIj2RuJHvH7fRVzFlNruLvMPOrH6BWI=", "0sCAAWFptg6Jk8Z0bMUZ0B8sMe3lqPNSsBuYizitEfxyVPBhDXD/P4p0PVyM1KER1XqK0W3jl2tGTp6uFUqSARCZZBp8Bb3VllP3WrEDWyy3o8EGRc6PrYno+TpZ1v6HHxqI9IHMJb0CwFNqGFtI5c6xZ/u4Lt2vEW/Vfp53GigSg0QKSWb0FMdaGcMtnef3wuGJxMAAEB+Tz+1J8scTF22RdJkrgV77aC+X2UlcxMiP4B8IUDptjPt/K5K+ltMHKH4e9ZX5eRww2sAHIlBHHZewQOI8Qk0q8Tf9DEmqRcF+Ih6cWblbPvAr8Ijs9am2G3DHnV2RyDZRH/ex4ioxuCgYmROOw5qYorEQArp5cTmkGyA1gJX86SLZ3cU4J5cZoC7H+b0fc7hr7QsXf73x4PwD8cAIGJs/vjXou3B7D2R3D7g=", "<html><head></head><body><span style=\"font-family:Segoe UI; font-size: 10.5pt; color: #000000\">dddddddddddddd<br/>\r\n</span></body></html>")]
        public void PlainDataDecryptTest(string key, string encrypedBody, string body)
        {
            var keyPair = GenerateECDHKeys();
            var keyData = Convert.FromBase64String(key);

            var keyPacket = Crypto.EncryptSessionKey(keyPair.PublicKey, new DecrypedSessionKey() { Key = keyData, Algo = SymmetricKeyAlgorithmTag.Aes256 });
            var bodyPacket = Convert.FromBase64String(encrypedBody);
            using var context = new MyOpenPgpContext();
            context.AddKeysToTemporalKeyRing(keyPair.PublicKey, keyPair.PrivateKey);
            using (var outStream = new MemoryStream())
            {
                using (var armored = new ArmoredOutputStream(outStream))
                {
                    armored.Write(keyPacket);
                    armored.Write(bodyPacket);
                }
                outStream.Position = 0;
                var decryped = Streams.ReadAll(Crypto.DecryptArmoredStream(context, outStream, false));
                var decrypedString = Encoding.UTF8.GetString(decryped);
                Assert.IsTrue(decrypedString == body);
            }
        }

        const string Body1 = "<html><head></head><body><span style=\"font-family:Segoe UI; font-size: 10.5pt; color: #000000\">dddddddddddddd<br/>\r\n</span></body></html>";

        [Test]
        public void SignatureTest()
        {
            var keyPairSender = GenerateECDHKeys();

            using var context = new MyOpenPgpContext();
            context.AddKeysToTemporalKeyRing(keyPairSender.PublicKey, keyPairSender.PrivateKey);

            var (key1, encBody1) = Crypto.Split(Crypto.EncryptAndSign(context, InternalAddress, Encoding.UTF8.GetBytes(Body1)));
            using var stream = new MemoryStream(encBody1.ToArray());

            using (var outStream = new MemoryStream())
            {
                using (var armored = new ArmoredOutputStream(outStream))
                {
                    armored.Write(key1.ToArray());
                    armored.Write(encBody1.ToArray());
                }
                outStream.Position = 0;

                var decryped = Streams.ReadAll(Crypto.DecryptArmoredStream(context, outStream));
                var decrypedString = Encoding.UTF8.GetString(decryped);
                Assert.IsTrue(decrypedString == Body1);
            }
            Assert.IsTrue(key1.Length == 96);
            Assert.IsTrue(encBody1.Length >= 315);
        }

        [Test]
        public void DetachedSignatureTest()
        {
            var keyPairSender = GenerateEdDsaKeys();
            var body = "test body";
            var signature = Crypto.SignDetached(keyPairSender.PrivateKey, Encoding.UTF8.GetBytes(body));
            Assert.IsTrue(VerifySignature(keyPairSender.PublicKey, signature, Encoding.UTF8.GetBytes(body)));

        }


        private static bool VerifySignature(PgpPublicKey signer, byte[] signature, byte[] data)
        {
            using var signatureStream = new MemoryStream(signature);
            PgpObjectFactory pgpObjectFactory = new PgpObjectFactory(signatureStream);
            PgpObject pgpObject = pgpObjectFactory.NextPgpObject();
            PgpSignatureList pgpSignatureList = pgpObject as PgpSignatureList;
            if (pgpSignatureList is null)
            {
                return false;
            }
            bool res = true;
            for (int i = 0; i < pgpSignatureList.Count; ++i)
            {
                PgpSignature s = pgpSignatureList[i];
                s.InitVerify(signer);
                s.Update(data);
                res &= s.Verify();
            }
            return res;
        }

        [Test]
        public async Task ProtonMessageIDsTest()
        {
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var ids = new List<string>()
        {
            "1234567",
            "1234568"
        };
            await storage.AddMessageIDs(ids, default).ConfigureAwait(true);
            var storedIds = await storage.LoadMessageIDsAsync(default).ConfigureAwait(false);
            CollectionAssert.AreEquivalent(storedIds.Select(x => x.Key), ids);
        }

        [Test]
        public async Task ProtonNoMessagesTest()
        {
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var res = await storage.GetMessagesAsync("1", 0, default).ConfigureAwait(true);
            Assert.IsTrue(res.Count == 0);
        }

        [Test]
        public async Task DeleteByMessageIdTest()
        {
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var messages = new List<Proton.Message>()
        {
            CreateMessage("1234567", new List<string>(){"1", "5" }),
            CreateMessage("1234568", "1"),
        };
            await storage.AddMessageIDs(messages.Select(x => x.MessageId).Distinct().ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(messages, default).ConfigureAwait(true);
            var storedMessages1 = await storage.GetMessagesAsync("1", 0).ConfigureAwait(true); // should return all message with this label
            var storedMessages2 = await storage.GetMessagesAsync("5", 1).ConfigureAwait(true);
            Assert.IsTrue(storedMessages1.Count == 2);
            Assert.IsTrue(storedMessages2.Count == 1);
            CollectionAssert.AreEquivalent(storedMessages1, messages);
            await storage.DeleteMessageByMessageIdsAsync(new List<string>() { "1234567" }, default).ConfigureAwait(true);
            storedMessages1 = await storage.GetMessagesAsync("1", 0).ConfigureAwait(true);
            Assert.IsTrue(storedMessages1.Count == 1);
            var storedMessages3 = await storage.GetMessagesAsync("5", 1).ConfigureAwait(true);
            Assert.IsTrue(storedMessages3.Count == 0);
            Assert.IsTrue(storedMessages1[0].Equals(messages[1]));
        }

        [Test]
        public async Task GetMessagesTest()
        {
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var messages = new List<Proton.Message>()
        {
            CreateMessage("1", new List<string>(){"1", "5" }),
            CreateMessage("2", "1"),
            CreateMessage("3", new List<string>(){"1", "5" }),
        };
            await storage.AddMessageIDs(messages.Select(x => x.MessageId).Distinct().ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(messages, default).ConfigureAwait(true);

            var storedMessages1 = await storage.GetMessagesAsync("5", 0).ConfigureAwait(true);
            Assert.IsTrue(storedMessages1.Count == 2);
            Assert.IsTrue(storedMessages1[0].MessageId == "3");
            Assert.IsTrue(storedMessages1[1].MessageId == "1");

            messages[1].LabelIds = new List<string>() { "1", "5" };
            await storage.AddOrUpdateMessagesAsync(new List<Proton.Message>() { messages[1] }, default).ConfigureAwait(true);
            var storedMessages2 = await storage.GetMessagesAsync("5", 0).ConfigureAwait(true);
            Assert.IsTrue(storedMessages2.Count == 3);
            Assert.IsTrue(storedMessages2[0].MessageId == "3");
            Assert.IsTrue(storedMessages2[1].MessageId == "2");
            Assert.IsTrue(storedMessages2[2].MessageId == "1");

            var storedMessages3 = await storage.GetMessagesAsync("5", (uint)storedMessages2[0].Id, getEarlier: true, 1).ConfigureAwait(true);
            Assert.IsTrue(storedMessages3.Count == 1);
            Assert.IsTrue(storedMessages3[0].MessageId == "2");

            var storedMessages4 = await storage.GetMessagesAsync("5", (uint)storedMessages2[0].Id, getEarlier: false, 1).ConfigureAwait(true);
            Assert.IsTrue(storedMessages4.Count == 0);

            var storedMessages5 = await storage.GetMessagesAsync("5", (uint)storedMessages2[1].Id, getEarlier: false, 1).ConfigureAwait(true);
            Assert.IsTrue(storedMessages5.Count == 1);
            Assert.IsTrue(storedMessages5[0].MessageId == "3");

        }

        [Test]
        public async Task DeleteByIdTest()
        {
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var messages = new List<Proton.Message>()
        {
            CreateMessage("1234567", new List<string>(){"1", "5" }),
            CreateMessage("1234568", "1"),
        };
            await storage.AddMessageIDs(messages.Select(x => x.MessageId).Distinct().ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(messages, default).ConfigureAwait(true);
            var storedMessages1 = await storage.GetMessagesAsync("1", 0).ConfigureAwait(true);
            await storage.DeleteMessagesByIds(storedMessages1.Select(x => (uint)x.Id).ToList(), "5", default).ConfigureAwait(true);
            var storedMessages2 = await storage.GetMessagesAsync("1", 0).ConfigureAwait(true);
            CollectionAssert.AreEquivalent(storedMessages1, storedMessages2);
            await storage.DeleteMessagesByIds(storedMessages1.Select(x => (uint)x.Id).ToList(), "1", default).ConfigureAwait(true);
            var storedMessages3 = await storage.GetMessagesAsync("1", 0).ConfigureAwait(true);
            Assert.IsTrue(storedMessages3.Count == 0);
            var storedMessages4 = await storage.GetMessagesAsync("5", 1).ConfigureAwait(true);
            Assert.IsTrue(storedMessages4.Count == 0);
        }

        [Test]
        public void TestMultipartFormData()
        {
            byte[] keyPacketBytes = new byte[] { 61, 62, 63, 64 };
            byte[] dataPacketBytes = new byte[] { 71, 72, 73, 74 };
            byte[] signatureBytes = new byte[] { 41, 42, 43, 44 };
            byte[] bytes = Crypto.PackToMultipart("12346", "myfile", keyPacketBytes, dataPacketBytes, signatureBytes);

            var str = Encoding.UTF8.GetString(bytes);

            Assert.Pass();
        }

        [Test]
        public async Task TestMultipartFormData2()
        {
            byte[] keyPacketBytes = new byte[] { 61, 62, 63, 64 };
            byte[] dataPacketBytes = new byte[] { 71, 72, 73, 74 };
            byte[] signatureBytes = new byte[] { 41, 42, 43, 44 };
            byte[] bytes = await Crypto.PackToMultipartAsync("12346", "myfile", keyPacketBytes, dataPacketBytes, signatureBytes).ConfigureAwait(true);

            var str = Encoding.UTF8.GetString(bytes);

            Assert.Pass();
        }

        [Test]
        public async Task UpdateMessageTest()
        {
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var messages = new List<Proton.Message>()
        {
            CreateMessage("1234567", new List<string>(){"1", "5" }),
            CreateMessage("1234568", "1"),
        };
            await storage.AddMessageIDs(messages.Select(x => x.MessageId).Distinct().ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(messages, default).ConfigureAwait(true);
            var updatedMessages = new List<Proton.Message>()
        {
            messages[0],
            messages[1]
        };
            updatedMessages[0].Id = 0;
            updatedMessages[0].Unread = false;
            updatedMessages[1].Unread = false;
            updatedMessages[1].Subject = "New subject";
            updatedMessages[1].From = "new@from.box";
            updatedMessages[1].To = "new@to.box";
            updatedMessages[1].Cc = "new@cc.box";
            updatedMessages[1].Bcc = "new@bcc.box";
            await storage.AddOrUpdateMessagesAsync(updatedMessages).ConfigureAwait(true);
            var m1 = await storage.GetMessagesAsync("5", 2).ConfigureAwait(true);
            var m2 = await storage.GetMessagesAsync("1", 2).ConfigureAwait(true);
            Assert.IsTrue(m1.Count == 1);
            Assert.IsTrue(m2.Count == 2);
            Assert.AreEqual(m1[0], m2[1]);
            Assert.IsTrue(m1[0].Unread == false);
            Assert.IsTrue(m1[0].Unread == false);
            Assert.IsTrue(m2[1].Unread == false);
            Assert.IsTrue(m2[0].Subject == "New subject");
            Assert.IsTrue(m2[0].From == "new@from.box");
            Assert.IsTrue(m2[0].To == "new@to.box");
            Assert.IsTrue(m2[0].Cc == "new@cc.box");
            Assert.IsTrue(m2[0].Bcc == "new@bcc.box");
        }

        private static Proton.Message CreateMessage(string id, string labelId)
        {
            return CreateMessage(id, new List<string>() { labelId });
        }

        private static Proton.Message CreateMessage(string id, IReadOnlyList<string> labelIds)
        {
            var message = new Proton.Message()
            {
                Subject = "Test Subject",
                MessageId = id,
                Unread = true,
                Time = DateTimeOffset.Now,
                From = "sender@mail.box",
                To = "receiver@mail.box;receiver@mail.box:reseiver",
                Cc = "cc@mail.box;cc2@mail.box",
                Bcc = "bcc@mail.box;bcc2@mail.box",
                LabelIds = labelIds
            };
            return message;
        }

        private static async Task<IStorage> GetProtonStorageAsync()
        {
            const string fileName = "proton.db";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            var db = DataStorageProvider.GetDataStorage(fileName);
            await db.CreateAsync("123").ConfigureAwait(true);
            return db as IStorage;
        }
    }
}
