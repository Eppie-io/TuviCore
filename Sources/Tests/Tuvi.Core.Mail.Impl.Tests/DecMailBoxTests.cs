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
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KeyDerivation.Keys;
using KeyDerivationLib;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using Org.BouncyCastle.Crypto.Parameters;
using Tuvi.Core.DataStorage;
using Tuvi.Core.DataStorage.Impl;
using Tuvi.Core.Dec;
using Tuvi.Core.Dec.Impl;
using Tuvi.Core.Entities;
using Tuvi.Core.Mail.Impl.Protocols;
using Tuvi.Core.Utils;
using TuviPgpLib;
using TuviPgpLibImpl;

namespace Tuvi.Core.Mail.Impl.Tests
{
    internal class MessageEqualityComparer : IEqualityComparer<Message>
    {
        public bool Equals(Message x, Message y)
        {
            return DecMailBoxTests.CompareMessages(x, y) == 0;
        }

        public int GetHashCode([DisallowNull] Message obj)
        {
            return (int)obj.Id;
        }
    }

    internal class MessageComparer : IComparer<Message>
    {
        public int Compare(Message x, Message y)
        {
            if (x == null && y == null)
            {
                return 0;
            }
            if (x == null && y != null)
            {
                return -1;
            }
            if (x != null && y == null)
            {
                return 1;
            }
            return DecMailBoxTests.CompareMessages(x, y);
        }

        public int Compare(object x, object y)
        {
            return Compare(x as Message, y as Message);
        }
    }

    public class DecMailBoxTests
    {
        private static Mock<IDecStorageClient> CreateDecClient()
        {
            var decMessages = new Dictionary<string, string>();
            var decData = new Dictionary<string, byte[]>();
            var client = new Mock<IDecStorageClient>();
            client.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((string address, string hash, CancellationToken ct) =>
                  {
                      decMessages[hash] = address;
                      return hash;
                  });
            client.Setup(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(
                (string address, CancellationToken ct) =>
                {
                    return decMessages
                            .Where(x => x.Value == address)
                            .Select(x => x.Key)
                            .ToList();
                });
            client.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(
                (string hash, CancellationToken ct) =>
                {
                    return decData[hash];
                });
            client.Setup(x => x.PutAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(
                (byte[] data, CancellationToken ct) =>
                {
                    var hash = GetSHA256(data);
                    decData[hash] = data;
                    return hash;
                });
            return client;
        }

        private static Mock<IDecStorageClient> CreateFaultyDecClient()
        {
            var decMessages = new Dictionary<string, Dictionary<string, byte[]>>();
            var client = new Mock<IDecStorageClient>();
            client.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((string address, string hash, CancellationToken ct) =>
                  {
                      throw new DecException();
                  });
            client.Setup(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(
                (string address, CancellationToken ct) =>
                {
                    throw new DecException();
                });
            client.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(
                (string hash, CancellationToken ct) =>
                {
                    throw new DecException();
                });
            client.Setup(x => x.PutAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(
                (byte[] data, CancellationToken ct) =>
                {
                    throw new DecException();
                });
            return client;
        }

        private static readonly IPublicKeyService _publicKeyService = PublicKeyService.CreateDefault(PublicKeyService.NoOpNameResolver);

        static MasterKey _masterKey;
        private static Mock<IDecStorage> CreateKeyStorageMock1()
        {
            _masterKey = EncryptionTestsData.CreateTestMasterKeyForSeed(TestSeedPhrase1);
            return CreateKeyStorageMock(_masterKey);
        }

        private Task<IDataStorage> CreateKeyStorage1Async()
        {
            return CreateKeyStorageAsync(TestSeedPhrase1);
        }

        private int _storageId;

        private async Task<IDataStorage> CreateKeyStorageAsync(string[] seedPhrase)
        {
            var keyFactory = new MasterKeyFactory(new ImplementationDetailsProvider("Tuvi seed", "Tuvi.Package", "backup@test"));
            keyFactory.RestoreSeedPhrase(seedPhrase);
            using var masterKey = keyFactory.GetMasterKey();
            string path = $"test{_storageId++}.db";
            File.Delete(path);
            var storage = DataStorageProvider.GetDataStorage(path);
            await storage.CreateAsync("123").ConfigureAwait(false);
            await storage.InitializeMasterKeyAsync(masterKey, default).ConfigureAwait(false);
            return storage;
        }

        private static Mock<IDecStorage> CreateKeyStorageMock(MasterKey masterKey)
        {
            var messages = new Dictionary<EmailAddress, List<DecMessage>>();
            var storage = new Mock<IDecStorage>();
            storage.Setup(x => x.GetMasterKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(masterKey);
            storage.Setup(x => x.GetDecMessagesAsync(It.IsAny<EmailAddress>(),
                                         It.IsAny<Folder>(),
                                         It.IsAny<int>(),
                                         It.IsAny<CancellationToken>()))
                .ReturnsAsync((EmailAddress email, Folder folder, int c, CancellationToken ct) =>
                {
                    return messages[email].Where(x => x.FolderAttributes == FolderAttributes.Inbox).ToList();
                });
            storage.Setup(x => x.AddDecMessageAsync(It.IsAny<EmailAddress>(),
                                        It.IsAny<DecMessage>(),
                                        It.IsAny<CancellationToken>())).Callback((EmailAddress email, DecMessage message, CancellationToken ct) =>
                                        {
                                            if (!messages.ContainsKey(email))
                                            {
                                                messages[email] = new List<DecMessage>();
                                            }
                                            var emailMessages = messages[email];
                                            if (!emailMessages.Contains(message))
                                            {
                                                emailMessages.Add(message);
                                            }
                                        });
            return storage;
        }

        private static Message CreateMessage(EmailAddress from, EmailAddress to)
        {
            return CreateMessage(from, to, 890354, TimeSpan.Zero);
        }

        private static Message CreateMessage(EmailAddress from, EmailAddress to, uint id, TimeSpan timeSpan)
        {
            Message message = new Message();
            message.From.Add(from);
            message.To.Add(to);
            message.Subject = $"This is test TuviMail send message {id}";
            message.TextBody = $"Text of the test message {id}";
            message.Id = id;
            message.Date = DateTime.Now.Add(timeSpan);
            message.Folder = new Folder("Inbox", FolderAttributes.Inbox);
            return message;
        }

        public static int CompareMessages(Message a, Message b)
        {
            Contract.Assert(a != null && b != null);
            if (AreEqualMessages(a, b))
            {
                return 0;
            }
            return a.Date.CompareTo(b.Date);
        }

        private static bool AreEqualMessages(Message a, Message b)
        {
            return //a.Id == b.Id &&
                   a.Date == b.Date &&
                   a.Subject == b.Subject &&
                   a.Folder.Equals(b.Folder) &&
                   a.PreviewText == b.PreviewText &&
                   a.HtmlBody == b.HtmlBody &&
                   a.TextBody == b.TextBody &&
                   a.IsFlagged == b.IsFlagged &&
                   a.IsMarkedAsRead == b.IsMarkedAsRead &&
                   a.Protection.Type == b.Protection.Type &&
                   AreAddressesEqual(a.From, b.From) &&
                   AreAddressesEqual(a.To, b.To) &&
                   AreAddressesEqual(a.ReplyTo, b.ReplyTo) &&
                   AreAddressesEqual(a.Cc, b.Cc) &&
                   AreAddressesEqual(a.Bcc, b.Bcc);
        }

        private static bool AreAddressesEqual(IEnumerable<EmailAddress> left, IEnumerable<EmailAddress> right)
        {
            if (left == right)
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            return Enumerable.SequenceEqual(left, right);
        }

        private static (int, EmailAddress) GetAddress1(string addressType)
        {
            var index = 1;
            if (addressType == HybridAddressType)
            {
                return (index, new EmailAddress("test+agrutu67edu83skwcj4fkzd4n4xf2dadm9wwrzezh5s9t859sbier@test.com", "test@test.com"));
            }

            if (addressType == DecentralizedAddressType)
            {
                return (index, EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, "aft5f6u8uf42sfjb9buhzbra3rdbc3rdwggwdrwqtfgvegktxh8cc"));
            }

            throw new ArgumentException($"Unknown address type: {addressType}");
        }

        private static async Task<(int, EmailAddress)> GetAddress1Async(IKeyStorage keyStorage, string addressType)
        {
            var index = 1;
            if (addressType == HybridAddressType)
            {
                var keyTag = "test1@test.com";
                var pubKey = await GetDecAddressAsync(keyStorage, keyTag).ConfigureAwait(true);
                var address = new EmailAddress("test1+" + pubKey + "@test.com", keyTag);
                return (index, address);
            }

            if (addressType == DecentralizedAddressType)
            {
                var pubKey = await GetDecAddressAsync(keyStorage, index).ConfigureAwait(true);
                var address = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, pubKey);
                return (index, address);
            }

            throw new ArgumentException($"Unknown address type: {addressType}");
        }

        private static async Task<(int, EmailAddress)> GetAddress2Async(IKeyStorage keyStorage, string addressType)
        {
            var index = 2;
            if (addressType == HybridAddressType)
            {
                var keyTag = "test2@test.com";
                var pubKey = await GetDecAddressAsync(keyStorage, keyTag).ConfigureAwait(true);
                var address = new EmailAddress("test2+" + pubKey + "@test.com", keyTag);
                return (index, address);
            }

            if (addressType == DecentralizedAddressType)
            {
                var pubKey = await GetDecAddressAsync(keyStorage, index).ConfigureAwait(true);
                var address = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, pubKey);
                return (index, address);
            }

            throw new ArgumentException($"Unknown address type: {addressType}");
        }

        private static async Task<(int, EmailAddress)> GetAddress3Async(IKeyStorage keyStorage, string addressType)
        {
            var index = 3;
            if (addressType == HybridAddressType)
            {
                var keyTag = "test3@test.com";
                var pubKey = await GetDecAddressAsync(keyStorage, keyTag).ConfigureAwait(true);
                var address = new EmailAddress("test3+" + pubKey + "@test.com", keyTag);
                return (index, address);
            }

            if (addressType == DecentralizedAddressType)
            {
                var pubKey = await GetDecAddressAsync(keyStorage, index).ConfigureAwait(true);
                var address = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, pubKey);
                return (index, address);
            }

            throw new ArgumentException($"Unknown address type: {addressType}");
        }

        private static readonly string[] TestSeedPhrase1 = {
            "apple", "apple", "apple", "apple", "apple", "apple",
            "apple", "apple", "apple", "apple", "apple", "apple"
        };

        private static string GetSHA256(byte[] data)
        {
            using var stream = new MemoryStream(data);
            using (var func = System.Security.Cryptography.SHA256.Create())
            {
                return StringHelper.BytesToHex(func.ComputeHash(stream));
            }
        }

        private static async Task<string> GetDecAddressAsync(IKeyStorage storage, string keyTag)
        {
            var masterKey = await storage.GetMasterKeyAsync().ConfigureAwait(false);

            var publicKeyPar = EccPgpContext.GenerateEccPublicKey(masterKey, keyTag);
            var pubKey = PublicKeyService.CreateDefault(PublicKeyService.NoOpNameResolver).Encode(publicKeyPar);

            return pubKey;
        }

        const int CoinType = 3630; // Eppie coin type
        const int Channel = 10; // Email channel
        const int AccountIndex = 10; // Account index
        const int KeyIndex = 0; // Key index

        private static async Task<string> GetDecAddressAsync(IKeyStorage storage, int accountIndex)
        {
            var masterKey = await storage.GetMasterKeyAsync().ConfigureAwait(false);

            var publicKeyPar = EccPgpContext.GenerateEccPublicKey(masterKey, CoinType, accountIndex, Channel, KeyIndex);
            var pubKey = PublicKeyService.CreateDefault(PublicKeyService.NoOpNameResolver).Encode(publicKeyPar);

            return pubKey;
        }

        [Test]
        [Category("Dec")]
        [TestCase("agrutu67edu83skwcj4fkzd4n4xf2dadm9wwrzezh5s9t859sbier", "test{0}@test.com")]
        [TestCase("ahucekc2b364jcjnxigfnk3sqjixnahmrizhjcewmvn4munk7wm5d", "test1{0}@test.com")]
        [TestCase("agz5qyvbusf5tgwaawnn5zcntegpywzhuq2fzdc53it5mduxk9e77", "test2{0}@test.com")]
        public async Task DecTransportEncryptionDecryptionTest(string address, string keyTagTemplate)
        {
            var storage = CreateKeyStorageMock1();
            var protector = new PgpDecProtector(storage.Object, _publicKeyService);
            var data = "data to encrypt";
            // encryption
            var keyTag = string.Format(CultureInfo.InvariantCulture, keyTagTemplate, "");
            var pubKey = await GetDecAddressAsync(storage.Object, keyTag).ConfigureAwait(true);
            var emailAddress = string.Format(CultureInfo.InvariantCulture, keyTagTemplate, $"+{pubKey}");
            var address2 = new EmailAddress(emailAddress, keyTag);
            var ecnryptedData = await protector.EncryptAsync(address, data, default).ConfigureAwait(true);

            var parts = address2.Address.Split('+')[1].Split('@');
            Assert.That(address, Is.EqualTo(parts[0]));

            // decryption
            var account = new Account
            {
                Email = address2
            };
            var decryptedData = await protector.DecryptAsync(account, ecnryptedData, default).ConfigureAwait(true);
            Assert.That(decryptedData, Is.EqualTo(data));
        }

        [Test]
        [Category("Dec")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(100)]
        [TestCase(111)]
        [TestCase(999)]
        [TestCase(1000)]
        [TestCase(10000)]
        public async Task DecTransportEncryptionDecryptionTest(int accountIndex)
        {
            var storage = CreateKeyStorageMock1();
            var protector = new PgpDecProtector(storage.Object, _publicKeyService);
            var data = "data to encrypt";
            // encryption            
            var pubKey = await GetDecAddressAsync(storage.Object, accountIndex).ConfigureAwait(true);
            var address2 = new EmailAddress(pubKey + "@eppie", pubKey);
            var ecnryptedData = await protector.EncryptAsync(address2.DecentralizedAddress, data, default).ConfigureAwait(true);

            // decryption
            var account = new Account
            {
                DecentralizedAccountIndex = accountIndex,
                Email = address2
            };
            var decryptedData = await protector.DecryptAsync(account, ecnryptedData, default).ConfigureAwait(true);
            Assert.That(decryptedData, Is.EqualTo(data));
        }

        const string HybridAddressType = "Hybrid";
        const string DecentralizedAddressType = "Decentralized";

        [Test]
        [Category("Dec")]
        [TestCase(HybridAddressType)]
        [TestCase(DecentralizedAddressType)]
        public async Task DecMessageEncryptionDecryptionTest(string addressType)
        {
            var senderStorage = CreateKeyStorageMock1();
            var receiverStorage = CreateKeyStorageMock1();
            (var index1, var senderAddress) = await GetAddress1Async(senderStorage.Object, addressType).ConfigureAwait(true);
            (var index2, var receiverAddress) = await GetAddress2Async(receiverStorage.Object, addressType).ConfigureAwait(true);
            using var senderContext = await CreateDecPgpContextAsync(senderStorage.Object, senderAddress, index1).ConfigureAwait(true); ;

            var pubKeys = senderContext.EnumeratePublicKeys().ToList();

            Message message = CreateMessage(senderAddress, receiverAddress);
            var senderMessageProtector = MessageProtectorCreator.GetMessageProtector(senderContext, _publicKeyService);
            var encryptedMessage = await senderMessageProtector.EncryptAsync(message, default).ConfigureAwait(true);

            using var receiverContext = await CreateDecPgpContextAsync(receiverStorage.Object, receiverAddress, index2).ConfigureAwait(true);

            var receiverMessageProtector = MessageProtectorCreator.GetMessageProtector(receiverContext, _publicKeyService);
            var decryptedMessage = await receiverMessageProtector.TryVerifyAndDecryptAsync(encryptedMessage).ConfigureAwait(true);
            Assert.That(decryptedMessage, Is.EqualTo(message));
            Assert.That(decryptedMessage.Protection.SignaturesInfo.Count, Is.Zero);
        }

        [Test]
        [Category("Dec")]
        [TestCase(HybridAddressType)]
        [TestCase(DecentralizedAddressType)]
        public async Task DecMessageSignatureTest(string addressType)
        {
            var senderStorage = CreateKeyStorageMock1();
            var receiverStorage = CreateKeyStorageMock1();
            (var index1, var senderAddress) = await GetAddress1Async(senderStorage.Object, addressType).ConfigureAwait(true);
            (var index2, var receiverAddress) = await GetAddress2Async(receiverStorage.Object, addressType).ConfigureAwait(true);
            using var senderContext = await CreateDecPgpContextAsync(senderStorage.Object, senderAddress, index1).ConfigureAwait(true);

            var pubKeys = senderContext.EnumeratePublicKeys().ToList();

            Message message = CreateMessage(senderAddress, receiverAddress);
            var senderMessageProtector = MessageProtectorCreator.GetMessageProtector(senderContext, _publicKeyService);
            var encryptedMessage = await senderMessageProtector.SignAsync(message, default).ConfigureAwait(true);

            using var receiverContext = await CreateDecPgpContextAsync(receiverStorage.Object, receiverAddress, index2).ConfigureAwait(true);
            await receiverContext.TryToAddDecPublicKeysAsync(new[] { senderAddress }, _publicKeyService, default).ConfigureAwait(true);
            var receiverMessageProtector = MessageProtectorCreator.GetMessageProtector(receiverContext, _publicKeyService);
            var decryptedMessage = await receiverMessageProtector.TryVerifyAndDecryptAsync(encryptedMessage).ConfigureAwait(true);
            Assert.That(decryptedMessage, Is.EqualTo(message));
            Assert.That(decryptedMessage.Protection.SignaturesInfo.Count, Is.EqualTo(1));
            Assert.That(decryptedMessage.Protection.SignaturesInfo[0].IsVerified, Is.True);
        }

        [Test]
        [Category("Dec")]
        [TestCase(HybridAddressType)]
        [TestCase(DecentralizedAddressType)]
        public async Task DecMessageSignAndEncryptTest(string addressType)
        {
            using var senderStorage = await CreateKeyStorage1Async().ConfigureAwait(true);
            using var receiverStorage = await CreateKeyStorage1Async().ConfigureAwait(true); // same seed but another key
            (var index1, var senderAddress) = await GetAddress1Async(senderStorage, addressType).ConfigureAwait(true);
            (var index2, var receiverAddress) = await GetAddress2Async(receiverStorage, addressType).ConfigureAwait(true);
            using var senderContext = await CreateDecPgpContextAsync(senderStorage, senderAddress, index1).ConfigureAwait(true); ;

            var pubKeys = senderContext.EnumeratePublicKeys().ToList();

            Message message = CreateMessage(senderAddress, receiverAddress);
            var senderMessageProtector = MessageProtectorCreator.GetMessageProtector(senderContext, _publicKeyService);
            var encryptedMessage = await senderMessageProtector.SignAndEncryptAsync(message, default).ConfigureAwait(true);
            Assert.That(message.TextBody, Is.Null);
            Assert.That(message.HtmlBody, Is.Null);

            // emulate dec transport layer
            var receiverEncryptedMessage = EmulateDecTransport(encryptedMessage);

            Assert.That(AreEqualMessages(encryptedMessage, receiverEncryptedMessage), Is.True);

            using var receiverContext = await CreateDecPgpContextAsync(receiverStorage, receiverAddress, index2).ConfigureAwait(true);
            await receiverContext.TryToAddDecPublicKeysAsync(new[] { senderAddress }, _publicKeyService, default).ConfigureAwait(true);
            var receiverMessageProtector = MessageProtectorCreator.GetMessageProtector(receiverContext, _publicKeyService);
            var decryptedMessage = await receiverMessageProtector.TryVerifyAndDecryptAsync(receiverEncryptedMessage).ConfigureAwait(true);
            Assert.That(AreEqualMessages(decryptedMessage, message), Is.False);
            decryptedMessage.ClearUnprotectedBody();
            Assert.That(AreEqualMessages(decryptedMessage, message), Is.True);
            Assert.That(decryptedMessage.Protection.SignaturesInfo.Count == 1, Is.True);
            Assert.That(decryptedMessage.Protection.SignaturesInfo[0].IsVerified, Is.True);
        }

        private static Message EmulateDecTransport(Message messageToSend)
        {
            var rawMessage = new DecMessageRaw(messageToSend);
            var data = JsonConvert.SerializeObject(rawMessage);
            var receivedRawMessage = JsonConvert.DeserializeObject<DecMessageRaw>(data);
            var message = receivedRawMessage.ToMessage();
            message.Folder = new Folder("Inbox", FolderAttributes.Inbox);
            return message;
        }

        [Test]
        [TestCase(HybridAddressType)]
        [TestCase(DecentralizedAddressType)]
        public async Task DecAddressTranslationAsync(string addressType)
        {
            var storage = CreateKeyStorageMock1();
            (var index1, var address) = await GetAddress1Async(storage.Object, addressType).ConfigureAwait(true);
            using var context = await CreateDecPgpContextAsync(storage.Object, address, index1).ConfigureAwait(true);
            var publicKey = context.EnumeratePublicKeys().Where(x => !x.IsMasterKey && x.IsEncryptionKey).First();
            var s1 = PublicKeyService.CreateDefault(PublicKeyService.NoOpNameResolver).Encode(publicKey.GetKey() as ECPublicKeyParameters);
            Assert.That(s1, Is.EqualTo(address.DecentralizedAddress));
        }

        private static async Task<TuviPgpContext> CreateDecPgpContextAsync(IDecStorage storage, EmailAddress emailAddress, int accountIndex)
        {
            var context = new TuviPgpContext(storage);
            await context.LoadContextAsync().ConfigureAwait(true);
            var masterKey = await storage.GetMasterKeyAsync(default).ConfigureAwait(true);
            if (emailAddress.IsHybrid)
            {
                context.GeneratePgpKeysByTag(masterKey, emailAddress.Address, emailAddress.StandardAddress);
            }
            else
            {
                context.GeneratePgpKeysByBip44(masterKey, emailAddress.Address, CoinType, accountIndex, Channel, KeyIndex);
            }
            return context;
        }

        [Test]
        [TestCase(HybridAddressType)]
        [TestCase(DecentralizedAddressType)]
        public async Task SendReceiveDecSelfTest(string addressType)
        {
            (var index, var address) = GetAddress1(addressType);
            Message message = CreateMessage(address, address);
            var storage = CreateKeyStorageMock1();
            var client = CreateDecClient();
            Account account = CreateAccount(addressType, address, index);
            using var mailBox = new DecMailBox(account, storage.Object, client.Object, new PgpDecProtector(storage.Object, _publicKeyService), _publicKeyService);
            var folders = await mailBox.GetFoldersStructureAsync(default).ConfigureAwait(true);
            Assert.That(folders, Is.Not.Null);
            var sent = folders.Where(x => x.IsSent).FirstOrDefault();
            Assert.That(sent, Is.Not.Null);
            message.Folder = sent;
            var inbox = await mailBox.GetDefaultInboxFolderAsync(default).ConfigureAwait(true);
            Assert.That(inbox, Is.Not.Null);
            Assert.DoesNotThrowAsync(async () => await mailBox.SendMessageAsync(message, default).ConfigureAwait(true));
            client.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            var messages = await mailBox.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true);
            client.Verify(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            Assert.That(messages.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestDecMessage()
        {
            var senderAddress = new EmailAddress("234567890@dec.email.io");
            var receiverAddress = new EmailAddress("234567890@dec.email.io");
            var messageHash = "9876543";

            var coreMessage = CreateMessage(senderAddress, receiverAddress);
            coreMessage.Folder = new Folder("Folder Name", FolderAttributes.None);
            var decMessage = new DecMessage(messageHash, coreMessage);
            var restoredMessage = decMessage.ToMessage();
            Assert.That(AreEqualMessages(coreMessage, restoredMessage), Is.True);
        }

        [Test]
        [TestCase(HybridAddressType)]
        [TestCase(DecentralizedAddressType)]
        public async Task SendReceiveDifferentAccountsSameSeedTest(string addressType)
        {
            (var index1, var senderAddress) = GetAddress1(addressType);
            using var senderStorage = await CreateKeyStorage1Async().ConfigureAwait(true);
            using var receiverStorage = await CreateKeyStorage1Async().ConfigureAwait(true); // same seed but another key
            (var index2, var receiverAddress) = await GetAddress2Async(receiverStorage, addressType).ConfigureAwait(true);

            Message message = CreateMessage(senderAddress, receiverAddress);
            var client = CreateDecClient(); // shared client to emulate transport layer
            Assert.That(senderAddress, Is.Not.EqualTo(receiverAddress));

            using var sender = CreateDecMailBox(senderAddress, client.Object, senderStorage, index1);

            var senderFolders = await sender.GetFoldersStructureAsync(default).ConfigureAwait(true);
            Assert.That(senderFolders, Is.Not.Null);

            var sent = senderFolders.Where(x => x.IsSent).FirstOrDefault();
            Assert.That(sent, Is.Not.Null);
            message.Folder = sent;
            Assert.DoesNotThrowAsync(async () => await sender.SendMessageAsync(message, default).ConfigureAwait(true));
            client.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            await CheckReceiverMessageAsync(receiverAddress, message, client, receiverStorage, index2).ConfigureAwait(true);
        }

        [Test]
        [TestCase(HybridAddressType)]
        [TestCase(DecentralizedAddressType)]
        public async Task SendReceiveTwoMessagesTest(string addresstype)
        {
            using var senderStorage = await CreateKeyStorage1Async().ConfigureAwait(true);
            using var receiverStorage = await CreateKeyStorage1Async().ConfigureAwait(true); // same seed but another key
            (var index1, var senderAddress) = await GetAddress1Async(senderStorage, addresstype).ConfigureAwait(true);
            (var index2, var receiverAddress) = await GetAddress2Async(receiverStorage, addresstype).ConfigureAwait(true);

            Message message1 = CreateMessage(senderAddress, receiverAddress, 12, TimeSpan.Zero);
            Message message2 = CreateMessage(senderAddress, receiverAddress, 145, TimeSpan.FromSeconds(1));
            var client = CreateDecClient(); // shared client to emulate transport layer;

            using var sender = CreateDecMailBox(senderAddress, client.Object, senderStorage, index1);

            var senderFolders = await sender.GetFoldersStructureAsync(default).ConfigureAwait(true);
            Assert.That(senderFolders, Is.Not.Null);

            var sent = senderFolders.Where(x => x.IsSent).FirstOrDefault();
            Assert.That(sent, Is.Not.Null);
            message1.Folder = sent;
            message2.Folder = sent;
            Assert.DoesNotThrowAsync(async () => await sender.SendMessageAsync(message1, default).ConfigureAwait(true));
            Assert.DoesNotThrowAsync(async () => await sender.SendMessageAsync(message2, default).ConfigureAwait(true));
            client.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));

            using var receiver = CreateDecMailBox(receiverAddress, client.Object, receiverStorage, index2);
            var receiverFolders = await receiver.GetFoldersStructureAsync(default).ConfigureAwait(true);
            Assert.That(receiverFolders, Is.Not.Null);
            var messageCopy1 = message1.ShallowCopy();
            messageCopy1.IsMarkedAsRead = false; // SendAsync sets this flag
            var messageCopy2 = message2.ShallowCopy();
            messageCopy2.IsMarkedAsRead = false; // SendAsync sets this flag
            var inbox = await receiver.GetDefaultInboxFolderAsync(default).ConfigureAwait(true);
            Assert.That(inbox, Is.Not.Null);
            messageCopy1.Folder = inbox;
            messageCopy2.Folder = inbox;

            var messages = await receiver.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true);
            client.Verify(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            Assert.That(messages.Count, Is.EqualTo(2));
            var messages2 = await receiver.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true);
            client.Verify(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
            Assert.That(messages2.Count, Is.EqualTo(2));
            // Verify that message has decrypted correctly
            Assert.That(messages, Is.EqualTo(messages2).Using(new MessageComparer()));
            var comparer = new MessageEqualityComparer();
            Assert.That(messages2.Contains(messageCopy2, comparer), Is.True);
            Assert.That(messages2.Contains(messageCopy1, comparer), Is.True);
            Assert.That(messages2[0].Id, Is.EqualTo(2));
            Assert.That(messages2[1].Id, Is.EqualTo(1));
        }

        [Test]
        [TestCase(HybridAddressType)]
        [TestCase(DecentralizedAddressType)]
        public async Task SendReceiveTwoRecepients(string addressType)
        {
            (var index1, var senderAddress) = GetAddress1(addressType);
            var senderStorage = CreateKeyStorageMock1();
            var receiverStorage1 = CreateKeyStorageMock1(); // same seed but another key
            var receiverStorage2 = CreateKeyStorageMock1(); // same seed but another key
            (var index2, var receiverAddress1) = await GetAddress2Async(receiverStorage1.Object, addressType).ConfigureAwait(true);
            (var index3, var receiverAddress2) = await GetAddress3Async(receiverStorage2.Object, addressType).ConfigureAwait(true);

            Message message = CreateMessage(senderAddress, receiverAddress1);
            message.To.Add(receiverAddress2);
            var client = CreateDecClient(); // shared client to emulate transport layer
            Assert.That(senderAddress, Is.Not.EqualTo(receiverAddress1));
            Assert.That(senderAddress, Is.Not.EqualTo(receiverAddress2));
            using var sender = CreateDecMailBox(senderAddress, client.Object, senderStorage.Object, index1);

            var senderFolders = await sender.GetFoldersStructureAsync(default).ConfigureAwait(true);

            Assert.That(senderFolders, Is.Not.Null);

            var sent = senderFolders.Where(x => x.IsSent).FirstOrDefault();
            Assert.That(sent, Is.Not.Null);
            message.Folder = sent;

            Assert.DoesNotThrowAsync(async () => await sender.SendMessageAsync(message, default).ConfigureAwait(true));
            client.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            await CheckReceiverMessageAsync(receiverAddress1, message, client, receiverStorage1.Object, index2).ConfigureAwait(true);
            await CheckReceiverMessageAsync(receiverAddress2, message, client, receiverStorage2.Object, index3).ConfigureAwait(true);
        }

        private static async Task CheckReceiverMessageAsync(EmailAddress receiverAddress, Message message, Mock<IDecStorageClient> client, IDecStorage keyStorage, int accountIndex)
        {
            using var receiver = CreateDecMailBox(receiverAddress, client.Object, keyStorage, accountIndex);
            var receiverFolders = await receiver.GetFoldersStructureAsync(default).ConfigureAwait(true);
            Assert.That(receiverFolders, Is.Not.Null);
            var messageCopy = message.ShallowCopy();
            messageCopy.IsMarkedAsRead = false; // SendAsync sets this flag
            var inbox = await receiver.GetDefaultInboxFolderAsync(default).ConfigureAwait(true);
            Assert.That(inbox, Is.Not.Null);
            messageCopy.Folder = inbox;

            var messages = await receiver.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true);
            client.Verify(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            Assert.That(messages.Count, Is.EqualTo(1));
            var messages2 = await receiver.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true);
            client.Verify(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
            Assert.That(messages2.Count, Is.EqualTo(1));
            // Verify that message has decrypted correctly
            Assert.That(AreEqualMessages(messages[0], messages2[0]), Is.True);
            Assert.That(AreEqualMessages(messages2[0], messageCopy), Is.True);
        }

        private static DecMailBox CreateDecMailBox(EmailAddress address, IDecStorageClient client, IDecStorage storage, int accountIndex)
        {
            var addressType = address.IsHybrid ? HybridAddressType : DecentralizedAddressType;
            var senderAccount = CreateAccount(addressType, address, accountIndex);
            return new DecMailBox(senderAccount, storage, client, new PgpDecProtector(storage, _publicKeyService), _publicKeyService);
        }

        [Test]
        [TestCase(HybridAddressType)]
        [TestCase(DecentralizedAddressType)]
        public async Task DecEncryptMessage(string addressType)
        {
            DateTime KeyCreationTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            (_, var from) = GetAddress1(addressType);
            var message = CreateMessage(from, from);
            var storageMock = CreateKeyStorageMock1();
            var storage = storageMock.Object;
            using var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storage).ConfigureAwait(true);
            var masterKey = await storage.GetMasterKeyAsync().ConfigureAwait(true);
            pgpContext.GeneratePgpKeysByTag(masterKey, from.Address, from.StandardAddress);
            var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext, _publicKeyService);

            var messageToSign = message.ShallowCopy();
            Assert.DoesNotThrowAsync(async () => messageToSign = await messageProtector.SignAsync(messageToSign, default).ConfigureAwait(true));

            var messageToSignAndEncrypt = message.ShallowCopy();
            Assert.DoesNotThrowAsync(async () => messageToSignAndEncrypt = await messageProtector.SignAndEncryptAsync(messageToSignAndEncrypt, default).ConfigureAwait(true));

            var messageToEncrypt = message.ShallowCopy();
            Assert.DoesNotThrowAsync(async () => messageToEncrypt = await messageProtector.EncryptAsync(messageToEncrypt, default).ConfigureAwait(true));

            using var pgpContext2 = await TemporalKeyStorage.GetTemporalContextAsync(storage).ConfigureAwait(true);
            var messageProtector2 = MessageProtectorCreator.GetMessageProtector(pgpContext2, _publicKeyService);

            Assert.DoesNotThrowAsync(() => messageProtector2.TryVerifyAndDecryptAsync(messageToSign));
            Assert.DoesNotThrowAsync(() => messageProtector2.TryVerifyAndDecryptAsync(messageToSign)); // we do the same test two times intentionally
            Assert.ThrowsAsync<NoSecretKeyException>(() => messageProtector2.TryVerifyAndDecryptAsync(messageToSignAndEncrypt));
            Assert.DoesNotThrowAsync(() => messageProtector.TryVerifyAndDecryptAsync(messageToSignAndEncrypt));
            Assert.ThrowsAsync<NoSecretKeyException>(() => messageProtector2.TryVerifyAndDecryptAsync(messageToEncrypt));
            Assert.DoesNotThrowAsync(() => messageProtector.TryVerifyAndDecryptAsync(messageToEncrypt));
        }

        private static Account CreateAccount(string addressType, EmailAddress address, int accountIndex)
        {
            var account = new Account()
            {
                Email = address
            };

            if (addressType == DecentralizedAddressType)
            {
                account.DecentralizedAccountIndex = accountIndex;
            }

            return account;
        }

        [Test]
        [Category("PgpMessageProtector")]
        public async Task PgpMessageProtectorSignMessageSetsSignatureProtectionType()
        {
            // Arrange
            var storageMock = CreateKeyStorageMock1();
            var storage = storageMock.Object;
            (_, var address) = await GetAddress1Async(storage, DecentralizedAddressType).ConfigureAwait(true);
            using var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storage).ConfigureAwait(true);
            pgpContext.GeneratePgpKeysByBip44(await storage.GetMasterKeyAsync().ConfigureAwait(true), address.Address, CoinType, AccountIndex, Channel, KeyIndex);
            var protector = MessageProtectorCreator.GetMessageProtector(pgpContext, _publicKeyService);
            var message = CreateMessage(address, address);

            // Act
            var signedMessage = await protector.SignAsync(message, default).ConfigureAwait(true);

            // Assert
            Assert.That(signedMessage.Protection.Type, Is.EqualTo(MessageProtectionType.Signature));
            Assert.That(signedMessage.MimeBody, Is.Not.Null);
        }

        [Test]
        [Category("PgpMessageProtector")]
        public async Task PgpMessageProtectorEncryptAsyncMessageSetsEncryptionProtectionType()
        {
            // Arrange
            var storageMock = CreateKeyStorageMock1();
            var storage = storageMock.Object;
            (_, var address) = await GetAddress1Async(storage, DecentralizedAddressType).ConfigureAwait(true);
            using var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storage).ConfigureAwait(true);
            pgpContext.GeneratePgpKeysByBip44(await storage.GetMasterKeyAsync().ConfigureAwait(true), address.Address, CoinType, AccountIndex, Channel, KeyIndex);
            var protector = MessageProtectorCreator.GetMessageProtector(pgpContext, _publicKeyService);
            var message = CreateMessage(address, address);

            // Act
            var encryptedMessage = await protector.EncryptAsync(message, default).ConfigureAwait(true);

            // Assert
            Assert.That(encryptedMessage.Protection.Type, Is.EqualTo(MessageProtectionType.Encryption));
            Assert.That(encryptedMessage.MimeBody, Is.Not.Null);
        }

        [Test]
        [Category("PgpMessageProtector")]
        public async Task PgpMessageProtectorSignAndEncryptAsyncMessageSetsSignatureAndEncryptionProtectionType()
        {
            // Arrange
            var storageMock = CreateKeyStorageMock1();
            var storage = storageMock.Object;
            (_, var address) = await GetAddress1Async(storage, DecentralizedAddressType).ConfigureAwait(true);
            using var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storage).ConfigureAwait(true);
            pgpContext.GeneratePgpKeysByBip44(await storage.GetMasterKeyAsync().ConfigureAwait(true), address.Address, CoinType, AccountIndex, Channel, KeyIndex);
            var protector = MessageProtectorCreator.GetMessageProtector(pgpContext, _publicKeyService);
            var message = CreateMessage(address, address);

            // Act
            var encryptedMessage = await protector.SignAndEncryptAsync(message, default).ConfigureAwait(true);

            // Assert
            Assert.That(encryptedMessage.Protection.Type, Is.EqualTo(MessageProtectionType.SignatureAndEncryption));
            Assert.That(encryptedMessage.MimeBody, Is.Not.Null);
        }

        [Test]
        [Category("PgpMessageProtector")]
        public async Task PgpMessageProtectorTryVerifyAndDecryptAsyncSignedMessageVerifiesSignature()
        {
            // Arrange
            var storageMock = CreateKeyStorageMock1();
            var storage = storageMock.Object;
            (_, var address) = await GetAddress1Async(storage, DecentralizedAddressType).ConfigureAwait(true);
            using var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storage).ConfigureAwait(true);
            pgpContext.GeneratePgpKeysByBip44(await storage.GetMasterKeyAsync().ConfigureAwait(true), address.Address, CoinType, AccountIndex, Channel, KeyIndex);
            var protector = MessageProtectorCreator.GetMessageProtector(pgpContext, _publicKeyService);
            var message = CreateMessage(address, address);
            var signedMessage = await protector.SignAsync(message, default).ConfigureAwait(true);

            // Act
            var verifiedMessage = await protector.TryVerifyAndDecryptAsync(signedMessage, CancellationToken.None).ConfigureAwait(true);

            // Assert
            Assert.That(verifiedMessage.Protection.Type, Is.EqualTo(MessageProtectionType.Signature));
        }

        [Test]
        [Category("PgpMessageProtector")]
        public async Task PgpMessageProtectorTryVerifyAndDecryptAsyncEncryptedMessageDecryptsSuccessfully()
        {
            // Arrange
            var storageMock = CreateKeyStorageMock1();
            var storage = storageMock.Object;
            (_, var address) = await GetAddress1Async(storage, DecentralizedAddressType).ConfigureAwait(true);
            using var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storage).ConfigureAwait(true);
            pgpContext.GeneratePgpKeysByBip44(await storage.GetMasterKeyAsync().ConfigureAwait(true), address.Address, CoinType, AccountIndex, Channel, KeyIndex);
            var protector = MessageProtectorCreator.GetMessageProtector(pgpContext, _publicKeyService);
            var message = CreateMessage(address, address);
            var encryptedMessage = await protector.EncryptAsync(message, default).ConfigureAwait(true);

            // Act
            var decryptedMessage = await protector.TryVerifyAndDecryptAsync(encryptedMessage, CancellationToken.None).ConfigureAwait(true);

            // Assert
            Assert.That(decryptedMessage.Protection.Type, Is.EqualTo(MessageProtectionType.Encryption));
        }

        [Test]
        [Category("PgpMessageProtector")]
        public async Task TryToAddDecPublicKeysAsyncImportsKeyForDecentralizedAddress()
        {
            // Arrange
            var storageMock = CreateKeyStorageMock1();
            var storage = storageMock.Object;
            (_, var address) = await GetAddress1Async(storage, DecentralizedAddressType).ConfigureAwait(true);
            using var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storage).ConfigureAwait(true);
            Assert.That(pgpContext.GetPublicKeysInfo().Count, Is.Zero);

            // Act
            await pgpContext.TryToAddDecPublicKeysAsync(new[] { address }, _publicKeyService, default).ConfigureAwait(true);

            // Assert
            Assert.That(pgpContext.GetPublicKeysInfo().Count, Is.GreaterThan(0));
        }

        [Test]
        [Category("PgpMessageProtector")]
        public async Task TryToAddDecPublicKeysAsyncDoesNothingForRegularAddress()
        {
            // Arrange
            var storageMock = CreateKeyStorageMock1();
            var storage = storageMock.Object;
            var regularAddress = new EmailAddress("user@example.com");
            using var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storage).ConfigureAwait(true);
            Assert.That(pgpContext.GetPublicKeysInfo().Count, Is.Zero);

            // Act
            await pgpContext.TryToAddDecPublicKeysAsync(new[] { regularAddress }, _publicKeyService, default).ConfigureAwait(true);

            // Assert
            Assert.That(pgpContext.GetPublicKeysInfo().Count, Is.Zero);
        }

        [Test]
        [Category("PgpMessageProtector")]
        public async Task TryToAddDecPublicKeysAsyncOnlyDecentralizedImportedWhenMixedAddresses()
        {
            // Arrange
            var storageMock = CreateKeyStorageMock1();
            var storage = storageMock.Object;
            (_, var decAddress) = await GetAddress1Async(storage, DecentralizedAddressType).ConfigureAwait(true);
            var regularAddress = new EmailAddress("user@example.com");
            using var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storage).ConfigureAwait(true);
            Assert.That(pgpContext.GetPublicKeysInfo().Count, Is.Zero);

            // Act
            await pgpContext.TryToAddDecPublicKeysAsync(new[] { decAddress, regularAddress }, _publicKeyService, default).ConfigureAwait(true);

            // Assert
            Assert.That(pgpContext.GetPublicKeysInfo().Count, Is.GreaterThan(0));
        }

        [Test]
        [Category("PgpMessageProtector")]
        public async Task TryToAddDecPublicKeysAsyncEmptyCollectionDoesNothing()
        {
            // Arrange
            var storageMock = CreateKeyStorageMock1();
            var storage = storageMock.Object;
            using var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storage).ConfigureAwait(true);
            Assert.That(pgpContext.GetPublicKeysInfo().Count, Is.Zero);

            // Act
            await pgpContext.TryToAddDecPublicKeysAsync(Array.Empty<EmailAddress>(), _publicKeyService, default).ConfigureAwait(true);

            // Assert
            Assert.That(pgpContext.GetPublicKeysInfo().Count, Is.Zero);
        }
    }
}
