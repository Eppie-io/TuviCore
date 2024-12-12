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
            Assert.That(body == receiverBody, Is.True);
        }

        [Test]
        [TestCase("A15VUlxvQ+3e0bo9Jqg/7iFaDdN+YnToSxy4tHWWuNM=", "0koBgPHji3nHD4mRXl5bv+kLVzzS6wIr7wZb5/MKuSp2RfV+FgMpNWDJSkcXcCkkWOzd05dCywr0VK11EFuCLS41B7ZTVkcAiLSGAQ==", "My body")]
        [TestCase("AbVOjLdx22soYIj2RuJHvH7fRVzFlNruLvMPOrH6BWI=", "0sCAAWFptg6Jk8Z0bMUZ0B8sMe3lqPNSsBuYizitEfxyVPBhDXD/P4p0PVyM1KER1XqK0W3jl2tGTp6uFUqSARCZZBp8Bb3VllP3WrEDWyy3o8EGRc6PrYno+TpZ1v6HHxqI9IHMJb0CwFNqGFtI5c6xZ/u4Lt2vEW/Vfp53GigSg0QKSWb0FMdaGcMtnef3wuGJxMAAEB+Tz+1J8scTF22RdJkrgV77aC+X2UlcxMiP4B8IUDptjPt/K5K+ltMHKH4e9ZX5eRww2sAHIlBHHZewQOI8Qk0q8Tf9DEmqRcF+Ih6cWblbPvAr8Ijs9am2G3DHnV2RyDZRH/ex4ioxuCgYmROOw5qYorEQArp5cTmkGyA1gJX86SLZ3cU4J5cZoC7H+b0fc7hr7QsXf73x4PwD8cAIGJs/vjXou3B7D2R3D7g=", "<html><head></head><body><span style=\"font-family:Segoe UI; font-size: 10.5pt; color: #000000\">dddddddddddddd<br/>\r\n</span></body></html>")]
        public async Task PlainDataDecryptTest(string key, string encrypedBody, string body)
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
                using (var decryptedStream = Crypto.DecryptArmoredStream(context, outStream, false))
                {
                    using (var reader = new StreamReader(decryptedStream))
                    {
                        var decryptedString = await reader.ReadToEndAsync().ConfigureAwait(true);
                        Assert.That(decryptedString == body, Is.True);
                    }
                }
            }
        }

        const string Body1 = "<html><head></head><body><span style=\"font-family:Segoe UI; font-size: 10.5pt; color: #000000\">dddddddddddddd<br/>\r\n</span></body></html>";

        [Test]
        public async Task SignatureTest()
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

                using (var decryptedStream = Crypto.DecryptArmoredStream(context, outStream))
                {
                    using (var reader = new StreamReader(decryptedStream))
                    {
                        var decryptedString = await reader.ReadToEndAsync().ConfigureAwait(true);
                        Assert.That(decryptedString == Body1, Is.True);
                    }
                }
            }
            Assert.That(key1.Length == 96, Is.True);
            Assert.That(encBody1.Length >= 315, Is.True);
        }

        [Test]
        public void DetachedSignatureTest()
        {
            var keyPairSender = GenerateEdDsaKeys();
            var body = "test body";
            var signature = Crypto.SignDetached(keyPairSender.PrivateKey, Encoding.UTF8.GetBytes(body));
            Assert.That(VerifySignature(keyPairSender.PublicKey, signature, Encoding.UTF8.GetBytes(body)), Is.True);

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
            Assert.That(storedIds.Select(x => x.Key), Is.EquivalentTo(ids));
        }

        [Test]
        public async Task ProtonNoMessagesTest()
        {
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var res = await storage.GetMessagesAsync(0, "1", 0, true, 0).ConfigureAwait(true);
            Assert.That(res.Count == 0, Is.True);
        }

        private static Proton.Message CreateMessage(int accountId, string id, IReadOnlyList<string> labelIds)
        {
            var message = new Proton.Message()
            {
                Subject = "Test Subject",
                MessageId = id,
                AccountId = accountId,
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

        private static Proton.Message CreateMessage(int accountId, string id, string labelId)
        {
            return CreateMessage(accountId, id, new List<string>() { labelId });
        }

        [Test]
        public async Task DeleteByMessageIdTest()
        {
            var accountId = 0;
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var messages = new List<Proton.Message>()
            {
                CreateMessage(accountId, "1234567", new List<string>(){"1", "5" }),
                CreateMessage(accountId, "1234568", "1"),
            };
            await storage.AddMessageIDs(messages.Select(x => x.MessageId).Distinct().ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, messages, default).ConfigureAwait(true);
            var storedMessages1 = await storage.GetMessagesAsync(accountId, "1", 0, true, 0).ConfigureAwait(true); // should return all message with this label
            var storedMessages2 = await storage.GetMessagesAsync(accountId, "5", 0, true, 1).ConfigureAwait(true);
            Assert.That(storedMessages1.Count == 2, Is.True);
            Assert.That(storedMessages2.Count == 1, Is.True);
            Assert.That(storedMessages1, Is.EquivalentTo(messages));

            await storage.DeleteMessageByMessageIdsAsync(new List<string>() { "1234567" }, default).ConfigureAwait(true);
            storedMessages1 = await storage.GetMessagesAsync(accountId, "1", 0, true, 0).ConfigureAwait(true);
            Assert.That(storedMessages1.Count == 1, Is.True);
            var storedMessages3 = await storage.GetMessagesAsync(accountId, "5", 0, true, 1).ConfigureAwait(true);
            Assert.That(storedMessages3.Count == 0, Is.True);
            Assert.That(storedMessages1[0].Equals(messages[1]), Is.True);
        }

        [Test]
        public async Task GetMessagesTest()
        {
            var accountId = 0;
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var messages = new List<Proton.Message>()
            {
                CreateMessage(accountId, "1", new List<string>(){"1", "5" }),
                CreateMessage(accountId, "2", "1"),
                CreateMessage(accountId, "3", new List<string>(){"1", "5" }),
            };
            await storage.AddMessageIDs(messages.Select(x => x.MessageId).Distinct().ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, messages, default).ConfigureAwait(true);

            var storedMessages1 = await storage.GetMessagesAsync(accountId, "5", 0, true, 0).ConfigureAwait(true);
            Assert.That(storedMessages1.Count == 2, Is.True);
            Assert.That(storedMessages1[0].MessageId == "3", Is.True);
            Assert.That(storedMessages1[1].MessageId == "1", Is.True);

            messages[1].LabelIds = new List<string>() { "1", "5" };
            await storage.AddOrUpdateMessagesAsync(accountId, new List<Proton.Message>() { messages[1] }, default).ConfigureAwait(true);
            var storedMessages2 = await storage.GetMessagesAsync(accountId, "5", 0, true, 0).ConfigureAwait(true);
            Assert.That(storedMessages2.Count == 3, Is.True);
            Assert.That(storedMessages2[0].MessageId == "3", Is.True);
            Assert.That(storedMessages2[1].MessageId == "2", Is.True);
            Assert.That(storedMessages2[2].MessageId == "1", Is.True);

            var storedMessages3 = await storage.GetMessagesAsync(accountId, "5", (uint)storedMessages2[0].Id, getEarlier: true, 1).ConfigureAwait(true);
            Assert.That(storedMessages3.Count == 1, Is.True);
            Assert.That(storedMessages3[0].MessageId == "2", Is.True);

            var storedMessages4 = await storage.GetMessagesAsync(accountId, "5", (uint)storedMessages2[0].Id, getEarlier: false, 1).ConfigureAwait(true);
            Assert.That(storedMessages4.Count == 0, Is.True);

            var storedMessages5 = await storage.GetMessagesAsync(accountId, "5", (uint)storedMessages2[1].Id, getEarlier: false, 1).ConfigureAwait(true);
            Assert.That(storedMessages5.Count == 1, Is.True);
            Assert.That(storedMessages5[0].MessageId == "3", Is.True);

        }

        [Test]
        public async Task DeleteByIdTest()
        {
            var accountId = 0;
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var messages = new List<Proton.Message>()
        {
            CreateMessage(accountId, "1234567", new List<string>(){"1", "5" }),
            CreateMessage(accountId, "1234568", "1"),
        };
            await storage.AddMessageIDs(messages.Select(x => x.MessageId).Distinct().ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, messages, default).ConfigureAwait(true);
            var storedMessages1 = await storage.GetMessagesAsync(accountId, "1", 0, true, 0).ConfigureAwait(true);
            await storage.DeleteMessagesByIds(storedMessages1.Select(x => (uint)x.Id).ToList(), "5", default).ConfigureAwait(true);
            var storedMessages2 = await storage.GetMessagesAsync(accountId, "1", 0, true, 0).ConfigureAwait(true);
            Assert.That(storedMessages1, Is.EquivalentTo(storedMessages2));
            await storage.DeleteMessagesByIds(storedMessages1.Select(x => (uint)x.Id).ToList(), "1", default).ConfigureAwait(true);
            var storedMessages3 = await storage.GetMessagesAsync(accountId, "1", 0, true, 0).ConfigureAwait(true);
            Assert.That(storedMessages3.Count == 0, Is.True);
            var storedMessages4 = await storage.GetMessagesAsync(accountId, "5", 0, true, 1).ConfigureAwait(true);
            Assert.That(storedMessages4.Count == 0, Is.True);
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
            var accountId = 0;
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var messages = new List<Proton.Message>()
            {
                CreateMessage(accountId, "1234567", new List<string>(){"1", "5" }),
                CreateMessage(accountId, "1234568", "1"),
            };
            await storage.AddMessageIDs(messages.Select(x => x.MessageId).Distinct().ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, messages, default).ConfigureAwait(true);
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
            await storage.AddOrUpdateMessagesAsync(accountId, updatedMessages).ConfigureAwait(true);
            var m1 = await storage.GetMessagesAsync(accountId, "5", 0, true, 2).ConfigureAwait(true);
            var m2 = await storage.GetMessagesAsync(accountId, "1", 0, true, 2).ConfigureAwait(true);
            Assert.That(m1.Count == 1, Is.True);
            Assert.That(m2.Count == 2, Is.True);
            Assert.That(m1[0], Is.EqualTo(m2[1]));
            Assert.That(m1[0].Unread == false, Is.True);
            Assert.That(m1[0].Unread == false, Is.True);
            Assert.That(m2[1].Unread == false, Is.True);
            Assert.That(m2[0].Subject == "New subject", Is.True);
            Assert.That(m2[0].From == "new@from.box", Is.True);
            Assert.That(m2[0].To == "new@to.box", Is.True);
            Assert.That(m2[0].Cc == "new@cc.box", Is.True);
            Assert.That(m2[0].Bcc == "new@bcc.box", Is.True);
        }

        [Test]
        public async Task ProtonMessagesWithDifferentAccountsTest()
        {
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);

            var account1Id = 1;
            var account2Id = 2;

            var account1Messages = new List<Proton.Message>()
            {
                CreateMessage(account1Id, "123", new List<string>() { "1" }),
                CreateMessage(account1Id, "124", new List<string>() { "1", "2" })
            };
            var account2Messages = new List<Proton.Message>()
            {
                CreateMessage(account2Id, "125", new List<string>() { "3" }),
                CreateMessage(account2Id, "126", new List<string>() { "3", "4" })
            };

            await storage.AddOrUpdateMessagesAsync(account1Id, account1Messages, default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(account2Id, account2Messages, default).ConfigureAwait(true);

            var storedAccount1Messages = await storage.GetMessagesAsync(account1Id, "1", 0, true, 0).ConfigureAwait(true);
            Assert.That(storedAccount1Messages.Count, Is.EqualTo(2));
            Assert.That(storedAccount1Messages.Select(x => x.MessageId), Is.EquivalentTo(account1Messages.Select(x => x.MessageId)));

            var storedAccount2Messages = await storage.GetMessagesAsync(account2Id, "3", 0, true, 0).ConfigureAwait(true);
            Assert.That(storedAccount2Messages.Count, Is.EqualTo(2));
            Assert.That(storedAccount2Messages.Select(x => x.MessageId), Is.EquivalentTo(account2Messages.Select(x => x.MessageId)));
        }

        [Test]
        public async Task ProtonMessagesIsolationBetweenAccountsTest()
        {
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);

            var account1Id = 1;
            var account2Id = 2;

            var account1Message = CreateMessage(account1Id, "123", new List<string>() { "1" });
            var account2Message = CreateMessage(account2Id, "123", new List<string>() { "2" });

            await storage.AddOrUpdateMessagesAsync(account1Id, new List<Proton.Message> { account1Message }, default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(account2Id, new List<Proton.Message> { account2Message }, default).ConfigureAwait(true);

            var storedAccount1Messages = await storage.GetMessagesAsync(account1Id, "1", 0, true, 0).ConfigureAwait(true);
            var storedAccount2Messages = await storage.GetMessagesAsync(account2Id, "2", 0, true, 0).ConfigureAwait(true);

            Assert.That(storedAccount1Messages.Count, Is.EqualTo(1));
            Assert.That(storedAccount1Messages[0].MessageId, Is.EqualTo("123"));

            Assert.That(storedAccount2Messages.Count, Is.EqualTo(1));
            Assert.That(storedAccount2Messages[0].MessageId, Is.EqualTo("123"));

            Assert.That(storedAccount1Messages[0].Id, Is.Not.EqualTo(storedAccount2Messages[0].Id));
        }

    }
}
