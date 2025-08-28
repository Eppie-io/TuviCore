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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.IO;
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
            var accountId = 1;
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var ids = new List<string>()
        {
            "1234567",
            "1234568"
        };
            await storage.AddMessageIDs(accountId, ids, default).ConfigureAwait(true);
            var storedIds = await storage.LoadMessageIDsAsync(accountId, default).ConfigureAwait(false);
            Assert.That(storedIds.Select(x => x.Key), Is.EquivalentTo(ids));
        }

        [Test]
        public async Task ProtonNoMessagesTest()
        {
            var accountId = 1;
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var res = await storage.GetMessagesAsync(accountId, "1", 0, true, 0).ConfigureAwait(true);
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
            var accountId = 1;
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var messages = new List<Proton.Message>()
            {
                CreateMessage(accountId, "1234567", new List<string>(){"1", "5" }),
                CreateMessage(accountId, "1234568", "1"),
            };
            await storage.AddMessageIDs(accountId, messages.Select(x => x.MessageId).Distinct().ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, messages, default).ConfigureAwait(true);
            var storedMessages1 = await storage.GetMessagesAsync(accountId, "1", 0, true, 0).ConfigureAwait(true); // should return all message with this label
            var storedMessages2 = await storage.GetMessagesAsync(accountId, "5", 0, true, 1).ConfigureAwait(true);
            Assert.That(storedMessages1.Count == 2, Is.True);
            Assert.That(storedMessages2.Count == 1, Is.True);
            Assert.That(storedMessages1, Is.EquivalentTo(messages));

            await storage.DeleteMessageByMessageIdsAsync(accountId, new List<string>() { "1234567" }, default).ConfigureAwait(true);
            storedMessages1 = await storage.GetMessagesAsync(accountId, "1", 0, true, 0).ConfigureAwait(true);
            Assert.That(storedMessages1.Count == 1, Is.True);
            var storedMessages3 = await storage.GetMessagesAsync(accountId, "5", 0, true, 1).ConfigureAwait(true);
            Assert.That(storedMessages3.Count == 0, Is.True);
            Assert.That(storedMessages1[0].Equals(messages[1]), Is.True);
        }

        [Test]
        public async Task GetMessagesTest()
        {
            var accountId = 1;
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var messages = new List<Proton.Message>()
            {
                CreateMessage(accountId, "1", new List<string>(){"1", "5" }),
                CreateMessage(accountId, "2", "1"),
                CreateMessage(accountId, "3", new List<string>(){"1", "5" }),
            };
            await storage.AddMessageIDs(accountId, messages.Select(x => x.MessageId).Distinct().ToList(), default).ConfigureAwait(true);
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
            var accountId = 1;
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var messages = new List<Proton.Message>()
        {
            CreateMessage(accountId, "1234567", new List<string>(){"1", "5" }),
            CreateMessage(accountId, "1234568", "1"),
        };
            await storage.AddMessageIDs(accountId, messages.Select(x => x.MessageId).Distinct().ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, messages, default).ConfigureAwait(true);
            var storedMessages1 = await storage.GetMessagesAsync(accountId, "1", 0, true, 0).ConfigureAwait(true);
            await storage.DeleteMessagesByIds(accountId, storedMessages1.Select(x => (uint)x.Id).ToList(), "5", default).ConfigureAwait(true);
            var storedMessages2 = await storage.GetMessagesAsync(accountId, "1", 0, true, 0).ConfigureAwait(true);
            Assert.That(storedMessages1, Is.EquivalentTo(storedMessages2));
            await storage.DeleteMessagesByIds(accountId, storedMessages1.Select(x => (uint)x.Id).ToList(), "1", default).ConfigureAwait(true);
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
            var accountId = 1;
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var messages = new List<Proton.Message>()
            {
                CreateMessage(accountId, "1234567", new List<string>(){"1", "5" }),
                CreateMessage(accountId, "1234568", "1"),
            };
            await storage.AddMessageIDs(accountId, messages.Select(x => x.MessageId).Distinct().ToList(), default).ConfigureAwait(true);
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

        // Repro test for SQLite "too many SQL variables" due to large IN clause generated by Contains on subquery
        [Test]
        public async Task ProtonGetMessagesTooManySqlVariablesRepro()
        {
            const int accountId = 1;
            const string label = "L";
            const int messageCount = 35000; // > 999 (SQLite default max variables)
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);

            var messages = new List<Proton.Message>(messageCount);
            for (int i = 0; i < messageCount; i++)
            {
                var id = (i + 1).ToString(CultureInfo.InvariantCulture);
                messages.Add(CreateMessage(accountId, id, label));
            }
            // Add message IDs (distinct) first
            await storage.AddMessageIDs(accountId, messages.Select(m => m.MessageId).ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, messages, default).ConfigureAwait(true);

            // Expect DataBaseException wrapping SQLiteException "too many SQL variables"
            Assert.DoesNotThrowAsync(async () =>
            {
                // count = 0 => request all labeled messages
                await storage.GetMessagesAsync(accountId, label, 0, true, 0).ConfigureAwait(true);
            });
        }

        [Test]
        public async Task ProtonGetMessagesByIdsTooManySqlVariablesRepro()
        {
            // Repro for overload GetMessagesAsync(accountId, IReadOnlyList<uint> ids, ...)
            // After chunking fix this should NOT throw even for a very large list of ids.
            const int accountId = 1;
            const string label = "XL_IDS";
            const int messageCount = 35000; // Large list beyond previous limits
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);

            var batch = new List<Proton.Message>(messageCount);
            for (int i = 0; i < messageCount; i++)
            {
                var id = (i + 1).ToString(CultureInfo.InvariantCulture);
                batch.Add(CreateMessage(accountId, id, label));
            }
            await storage.AddMessageIDs(accountId, batch.Select(m => m.MessageId).ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, batch, default).ConfigureAwait(true);

            var byLabel = await storage.GetMessagesAsync(accountId, label, 0, true, 0).ConfigureAwait(true);
            Assert.That(byLabel.Count, Is.EqualTo(messageCount));

            var ids = byLabel.Select(m => (uint)m.Id).ToList();

            // Should no longer throw DataBaseException (chunked internally)
            List<Proton.Message> byIds = null;
            Assert.DoesNotThrowAsync(async () =>
            {
                var res = await storage.GetMessagesAsync(accountId, ids, default).ConfigureAwait(true);
                byIds = res.ToList();
            });
            Assert.That(byIds.Count, Is.EqualTo(messageCount));
            // Order should correspond to input ids order
            for (int i = 0; i < ids.Count; i++)
            {
                Assert.That(byIds[i].Id, Is.EqualTo(ids[i]));
            }
        }

        [Test]
        public async Task ProtonGetMessagesKnownIdNoLaterMessages()
        {
            // Scenario: knownId provided with getEarlier = false, and there are no later (newer id) messages.
            // Expected: empty result set.
            const int accountId = 1;
            const string label = "EDGE_CASE";
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);

            var msgs = new List<Proton.Message>
            {
                CreateMessage(accountId, "10", label),
                CreateMessage(accountId, "20", label),
                CreateMessage(accountId, "30", label)
            };
            await storage.AddMessageIDs(accountId, msgs.Select(m => m.MessageId).ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, msgs, default).ConfigureAwait(true);

            var all = await storage.GetMessagesAsync(accountId, label, 0, true, 0).ConfigureAwait(true);
            Assert.That(all.Count, Is.EqualTo(3));
            var top = all[0]; // Ordered DESC by Id => highest Id

            var later = await storage.GetMessagesAsync(accountId, label, (uint)top.Id, getEarlier: false, count: 5).ConfigureAwait(true);
            Assert.That(later.Count, Is.Zero, "Should be no messages with Id greater than knownId when using getEarlier=false.");
        }

        [Test]
        public async Task ProtonGetMessagesDuplicateLabelIdsSingleResult()
        {
            // A message containing duplicate label ids should not appear multiple times in results.
            // We create a message with the same label repeated twice.
            const int accountId = 1;
            const string label = "DUP_LABEL";
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);

            var dup = CreateMessage(accountId, "M_DUP", new List<string> { label, label });
            await storage.AddMessageIDs(accountId, new List<string> { dup.MessageId }, default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, new List<Proton.Message> { dup }, default).ConfigureAwait(true);

            var list = await storage.GetMessagesAsync(accountId, label, 0, true, 0).ConfigureAwait(true);
            // If duplicate joins produce duplicates, Count would be > 1 – we assert correct behavior (single logical record)
            Assert.That(list.Count, Is.EqualTo(1), "Duplicate label associations should not duplicate the message in query results.");
            Assert.That(list[0].MessageId, Is.EqualTo("M_DUP"));
        }

        [Test]
        public async Task ProtonGetMessagesCountLimit()
        {
            const int accountId = 1;
            const string label = "LIM";
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var msgs = new List<Proton.Message>();
            for (int i = 0; i < 10; i++)
            {
                msgs.Add(CreateMessage(accountId, (i + 1).ToString(CultureInfo.InvariantCulture), label));
            }
            await storage.AddMessageIDs(accountId, msgs.Select(m => m.MessageId).ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, msgs, default).ConfigureAwait(true);

            var top5 = await storage.GetMessagesAsync(accountId, label, 0, true, 5).ConfigureAwait(true);
            Assert.That(top5.Count, Is.EqualTo(5));
            // Should be ordered DESC by Id
            for (int i = 0; i < top5.Count - 1; i++)
            {
                Assert.That(top5[i].Id, Is.GreaterThan(top5[i + 1].Id));
            }
        }

        [Test]
        public async Task ProtonUpdateMessageAddDuplicateLabelsShouldNotDuplicate()
        {
            const int accountId = 1;
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var message = CreateMessage(accountId, "UPD_DUP", new List<string> { "X" });
            await storage.AddMessageIDs(accountId, new List<string> { message.MessageId }, default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, new List<Proton.Message> { message }, default).ConfigureAwait(true);
            // Update message to include duplicate labels (some existing some new)
            message.LabelIds = new List<string> { "X", "X", "Y", "Y" };
            await storage.AddOrUpdateMessagesAsync(accountId, new List<Proton.Message> { message }, default).ConfigureAwait(true);

            var xList = await storage.GetMessagesAsync(accountId, "X", 0, true, 0).ConfigureAwait(true);
            var yList = await storage.GetMessagesAsync(accountId, "Y", 0, true, 0).ConfigureAwait(true);
            Assert.That(xList.Count, Is.EqualTo(1));
            Assert.That(yList.Count, Is.EqualTo(1));
            Assert.That(xList[0].MessageId, Is.EqualTo("UPD_DUP"));
            Assert.That(yList[0].MessageId, Is.EqualTo("UPD_DUP"));
        }

        [Test]
        public async Task ProtonGetMessagesIdsChunkBoundary()
        {
            // Verifies ordering for ids retrieval below chunk threshold
            const int accountId = 1;
            const string label = "BND";
            const int count = 950; // below 999 limit, single query path
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var list = new List<Proton.Message>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(CreateMessage(accountId, (i + 1).ToString(CultureInfo.InvariantCulture), label));
            }
            await storage.AddMessageIDs(accountId, list.Select(m => m.MessageId).ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, list, default).ConfigureAwait(true);

            var byLabel = await storage.GetMessagesAsync(accountId, label, 0, true, 0).ConfigureAwait(true);
            Assert.That(byLabel.Count, Is.EqualTo(count));
            var ids = byLabel.Select(m => (uint)m.Id).ToList();
            var byIds = await storage.GetMessagesAsync(accountId, ids, default).ConfigureAwait(true);
            for (int i = 0; i < ids.Count; i++)
            {
                Assert.That(byIds[i].Id, Is.EqualTo(ids[i]));
            }
        }

        [Test]
        public async Task ProtonGetMessagesKnownIdEarlierLarge()
        {
            const int accountId = 1;
            const string label = "EARLY";
            const int total = 2000; // Large enough to exercise join ordering
            using var storage = await GetProtonStorageAsync().ConfigureAwait(true);
            var list = new List<Proton.Message>(total);
            for (int i = 0; i < total; i++)
            {
                list.Add(CreateMessage(accountId, (i + 1).ToString(CultureInfo.InvariantCulture), label));
            }
            await storage.AddMessageIDs(accountId, list.Select(m => m.MessageId).ToList(), default).ConfigureAwait(true);
            await storage.AddOrUpdateMessagesAsync(accountId, list, default).ConfigureAwait(true);

            var all = await storage.GetMessagesAsync(accountId, label, 0, true, 0).ConfigureAwait(true);
            Assert.That(all.Count, Is.EqualTo(total));
            var mid = all[500]; // some item in middle (ordered DESC)
            // get earlier messages (older ids) than mid.Id limited to 25
            var earlier = await storage.GetMessagesAsync(accountId, label, (uint)mid.Id, getEarlier: true, count: 25).ConfigureAwait(true);
            Assert.That(earlier.Count, Is.EqualTo(25));
            foreach (var m in earlier)
            {
                Assert.That(m.Id, Is.LessThan(mid.Id));
            }
        }
    }
}
