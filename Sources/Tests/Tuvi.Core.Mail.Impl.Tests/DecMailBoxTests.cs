using KeyDerivationLib;
using MimeKit;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.DataStorage;
using Tuvi.Core.DataStorage.Impl;
using Tuvi.Core.Dec;
using Tuvi.Core.Dec.Impl;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;
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

    internal class MessageComparer : IComparer, IComparer<Message>
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

    internal class MyKeyMatcher : IKeyMatcher
    {
        public bool IsMatch(PgpPublicKey key, MailboxAddress mailbox)
        {
            var encodedPublicKey = PublicKeyConverter.ConvertPublicKeyToEmailName(key.GetKey() as ECPublicKeyParameters);
            return string.Equals(mailbox.ToEmailAddress().DecentralizedAddress, encodedPublicKey, StringComparison.OrdinalIgnoreCase);
        }
    }
    public class DecMailBoxTests
    {
        private static string GetSHA256(byte[] data)
        {
            using var stream = new MemoryStream(data);
            using (var func = System.Security.Cryptography.SHA256.Create())
            {
                return StringHelper.BytesToHex(func.ComputeHash(stream));
            }
        }

        private static async Task<EmailAddress> GetDecAddressAsync(IKeyStorage storage, string keyTag)
        {
            var pubKey = await TuviMail.GetDecAccountPublicKeyStringAsync(storage, keyTag, default).ConfigureAwait(true);
            return new EmailAddress(pubKey + "@eppie", keyTag);
        }

        [Test]
        public async Task DecTransportEncryptionDecryptionTest()
        {
            var storage = CreateKeyStorageMock1();
            var protector = new PgpDecProtector(storage.Object);
            var data = "data to encrypt";
            // encryption
            var address = "ahgp4kj6bd68xntwsvv4tukxe2pxmrcve7p9u5hmzi6mwmrrz476f";
            var keyTag = "Decentralized Account Demo #1";
            var address2 = await GetDecAddressAsync(storage.Object, keyTag).ConfigureAwait(true);
            var ecnryptedData = await protector.EncryptAsync(address, data, default).ConfigureAwait(true);

            var parts = address2.Address.Split('@');
            Assert.That(address, Is.EqualTo(parts[0]));

            // decryption
            var decryptedData = await protector.DecryptAsync(address, keyTag, ecnryptedData, default).ConfigureAwait(true);
            Assert.That(decryptedData, Is.EqualTo(data));
        }

        [Test]
        public async Task DecMessageEncryptionDecryptionTest()
        {
            var senderStorage = CreateKeyStorageMock1();
            var receiverStorage = CreateKeyStorageMock1();
            var senderAddress = await GetAddress1Async(senderStorage.Object).ConfigureAwait(true);
            var receiverAddress = await GetAddress2Async(receiverStorage.Object).ConfigureAwait(true);
            using var senderContext = await CreateDecPgpContextAsync(senderStorage.Object, senderAddress).ConfigureAwait(true); ;

            var pubKeys = senderContext.EnumeratePublicKeys().ToList();

            Message message = CreateMessage(senderAddress, receiverAddress);
            var senderMessageProtector = MessageProtectorCreator.GetMessageProtector(senderContext);
            var encryptedMessage = senderMessageProtector.Encrypt(message);

            using var receiverContext = await CreateDecPgpContextAsync(receiverStorage.Object, receiverAddress).ConfigureAwait(true);

            var receiverMessageProtector = MessageProtectorCreator.GetMessageProtector(receiverContext);
            var decryptedMessage = await receiverMessageProtector.TryVerifyAndDecryptAsync(encryptedMessage).ConfigureAwait(true);
            Assert.That(decryptedMessage, Is.EqualTo(message));
            Assert.That(decryptedMessage.Protection.SignaturesInfo.Count, Is.EqualTo(0));
        }

        [Test]
        [Category("Dec")]
        public async Task DecMessageSignatureTest()
        {
            var senderStorage = CreateKeyStorageMock1();
            var receiverStorage = CreateKeyStorageMock1();
            var senderAddress = await GetAddress1Async(senderStorage.Object).ConfigureAwait(true);
            var receiverAddress = await GetAddress2Async(receiverStorage.Object).ConfigureAwait(true);
            using var senderContext = await CreateDecPgpContextAsync(senderStorage.Object, senderAddress).ConfigureAwait(true);

            var pubKeys = senderContext.EnumeratePublicKeys().ToList();

            Message message = CreateMessage(senderAddress, receiverAddress);
            var senderMessageProtector = MessageProtectorCreator.GetMessageProtector(senderContext);
            var encryptedMessage = senderMessageProtector.Sign(message);

            using var receiverContext = await CreateDecPgpContextAsync(receiverStorage.Object, receiverAddress).ConfigureAwait(true);
            receiverContext.TryToAddDecSignerPublicKey(senderAddress);
            var receiverMessageProtector = MessageProtectorCreator.GetMessageProtector(receiverContext);
            var decryptedMessage = await receiverMessageProtector.TryVerifyAndDecryptAsync(encryptedMessage).ConfigureAwait(true);
            Assert.That(decryptedMessage, Is.EqualTo(message));
            Assert.That(decryptedMessage.Protection.SignaturesInfo.Count, Is.EqualTo(1));
            Assert.IsTrue(decryptedMessage.Protection.SignaturesInfo[0].IsVerified);
        }

        [Test]
        [Category("Dec")]
        public async Task DecMessageSignAndEncryptTest()
        {
            using var senderStorage = await CreateKeyStorage1Async().ConfigureAwait(true);
            using var receiverStorage = await CreateKeyStorage1Async().ConfigureAwait(true); // same seed but another key
            var senderAddress = await GetAddress1Async(senderStorage).ConfigureAwait(true);
            var receiverAddress = await GetAddress2Async(receiverStorage).ConfigureAwait(true);
            using var senderContext = await CreateDecPgpContextAsync(senderStorage, senderAddress).ConfigureAwait(true); ;

            var pubKeys = senderContext.EnumeratePublicKeys().ToList();

            Message message = CreateMessage(senderAddress, receiverAddress);
            var senderMessageProtector = MessageProtectorCreator.GetMessageProtector(senderContext);
            var encryptedMessage = senderMessageProtector.SignAndEncrypt(message);
            Assert.IsNull(message.TextBody);
            Assert.IsNull(message.HtmlBody);

            // emulate dec transport layer
            var receiverEncryptedMessage = EmulateDecTransport(encryptedMessage);

            Assert.IsTrue(AreEqualMessages(encryptedMessage, receiverEncryptedMessage));

            using var receiverContext = await CreateDecPgpContextAsync(receiverStorage, receiverAddress).ConfigureAwait(true);
            receiverContext.TryToAddDecSignerPublicKey(senderAddress);
            var receiverMessageProtector = MessageProtectorCreator.GetMessageProtector(receiverContext);
            var decryptedMessage = await receiverMessageProtector.TryVerifyAndDecryptAsync(receiverEncryptedMessage).ConfigureAwait(true);
            Assert.IsFalse(AreEqualMessages(decryptedMessage, message));
            decryptedMessage.ClearUnprotectedBody();
            Assert.IsTrue(AreEqualMessages(decryptedMessage, message));
            Assert.IsTrue(decryptedMessage.Protection.SignaturesInfo.Count == 1);
            Assert.IsTrue(decryptedMessage.Protection.SignaturesInfo[0].IsVerified);
        }

        private static Message EmulateDecTransport(Message messageToSend)
        {
            var rawMessage = new DecMessageRaw(messageToSend);
            var data = JsonConvert.SerializeObject(rawMessage);
            var receivedRawMessage = JsonConvert.DeserializeObject<DecMessageRaw>(data);
            return receivedRawMessage.ToMessage();
        }

        [Test]
        public async Task DecAddressTranslationAsync()
        {
            var storage = CreateKeyStorageMock1();
            var address = await GetAddress1Async(storage.Object).ConfigureAwait(true);
            using var context = await CreateDecPgpContextAsync(storage.Object, address).ConfigureAwait(true);
            var publicKey = context.EnumeratePublicKeys().Where(x => !x.IsMasterKey && x.IsEncryptionKey).First();
            var s1 = PublicKeyConverter.ConvertPublicKeyToEmailName(publicKey.GetKey() as ECPublicKeyParameters);
            Assert.That(s1, Is.EqualTo(address.DecentralizedAddress));
        }

        private static async Task<TuviPgpContext> CreateDecPgpContextAsync(IDecStorage storage, EmailAddress emailAddress)
        {
            var context = new TuviPgpContext(storage, new MyKeyMatcher());
            await context.LoadContextAsync().ConfigureAwait(true);
            var masterKey = await storage.GetMasterKeyAsync(default).ConfigureAwait(true);
            context.DeriveKeyForDec(masterKey, emailAddress.Address, emailAddress.Name);
            return context;
        }

        [Test]
        public async Task SendReceiveDecSelfTest()
        {
            var address = GetAddress1();
            Message message = CreateMessage(address, address);
            var storage = CreateKeyStorageMock1();
            var client = CreateDecClient();
            var account = new Account()
            {
                Email = address,
                KeyTag = address.Name
            };
            using var mailBox = new DecMailBox(account, storage.Object, client.Object, new PgpDecProtector(storage.Object));
            var folders = await mailBox.GetFoldersStructureAsync(default).ConfigureAwait(true);
            Assert.IsNotNull(folders);
            var sent = folders.Where(x => x.IsSent).FirstOrDefault();
            Assert.IsNotNull(sent);
            message.Folder = sent;
            var inbox = await mailBox.GetDefaultInboxFolderAsync(default).ConfigureAwait(true);
            Assert.IsNotNull(inbox);
            Assert.DoesNotThrowAsync(async () => await mailBox.SendMessageAsync(message, default).ConfigureAwait(true));
            client.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.AtLeastOnce);

            var messages = await mailBox.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true);
            client.Verify(x => x.ListAsync(It.IsAny<string>()), Times.AtLeastOnce);
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
            Assert.IsTrue(AreEqualMessages(coreMessage, restoredMessage));
        }

        [Test]
        public async Task SendReceiveDifferentAccountsSameSeedTest()
        {
            var senderAddress = GetAddress1();
            using var senderStorage = await CreateKeyStorage1Async().ConfigureAwait(true);
            using var receiverStorage = await CreateKeyStorage1Async().ConfigureAwait(true); // same seed but another key
            var receiverAddress = await GetAddress2Async(receiverStorage).ConfigureAwait(true);

            Message message = CreateMessage(senderAddress, receiverAddress);
            var client = CreateDecClient(); // shared client to emulate transport layer
            Assert.That(senderAddress, Is.Not.EqualTo(receiverAddress));

            using var sender = CreateDecMailBox(senderAddress, client.Object, senderStorage);

            var senderFolders = await sender.GetFoldersStructureAsync(default).ConfigureAwait(true);
            Assert.IsNotNull(senderFolders);

            var sent = senderFolders.Where(x => x.IsSent).FirstOrDefault();
            Assert.IsNotNull(sent);
            message.Folder = sent;
            Assert.DoesNotThrowAsync(async () => await sender.SendMessageAsync(message, default).ConfigureAwait(true));
            client.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.AtLeastOnce);

            await CheckReceiverMessageAsync(receiverAddress, message, client, receiverStorage).ConfigureAwait(true);
        }

        [Test]
        public async Task SendReceiveTwoMessagesTest()
        {
            using var senderStorage = await CreateKeyStorage1Async().ConfigureAwait(true);
            using var receiverStorage = await CreateKeyStorage1Async().ConfigureAwait(true); // same seed but another key
            var senderAddress = await GetAddress1Async(senderStorage).ConfigureAwait(true);
            var receiverAddress = await GetAddress2Async(receiverStorage).ConfigureAwait(true);

            Message message1 = CreateMessage(senderAddress, receiverAddress, 12, TimeSpan.Zero);
            Message message2 = CreateMessage(senderAddress, receiverAddress, 145, TimeSpan.FromSeconds(1));
            var client = CreateDecClient(); // shared client to emulate transport layer;

            using var sender = CreateDecMailBox(senderAddress, client.Object, senderStorage);

            var senderFolders = await sender.GetFoldersStructureAsync(default).ConfigureAwait(true);
            Assert.IsNotNull(senderFolders);

            var sent = senderFolders.Where(x => x.IsSent).FirstOrDefault();
            Assert.IsNotNull(sent);
            message1.Folder = sent;
            message2.Folder = sent;
            Assert.DoesNotThrowAsync(async () => await sender.SendMessageAsync(message1, default).ConfigureAwait(true));
            Assert.DoesNotThrowAsync(async () => await sender.SendMessageAsync(message2, default).ConfigureAwait(true));
            client.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.AtLeast(2));

            using var receiver = CreateDecMailBox(receiverAddress, client.Object, receiverStorage);
            var receiverFolders = await receiver.GetFoldersStructureAsync(default).ConfigureAwait(true);
            Assert.IsNotNull(receiverFolders);
            var messageCopy1 = message1.ShallowCopy();
            messageCopy1.IsMarkedAsRead = false; // SendAsync sets this flag
            var messageCopy2 = message2.ShallowCopy();
            messageCopy2.IsMarkedAsRead = false; // SendAsync sets this flag
            var inbox = await receiver.GetDefaultInboxFolderAsync(default).ConfigureAwait(true);
            Assert.IsNotNull(inbox);
            messageCopy1.Folder = inbox;
            messageCopy2.Folder = inbox;

            var messages = await receiver.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true);
            client.Verify(x => x.ListAsync(It.IsAny<string>()), Times.AtLeastOnce);
            Assert.That(messages.Count, Is.EqualTo(2));
            var messages2 = await receiver.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true);
            client.Verify(x => x.ListAsync(It.IsAny<string>()), Times.AtLeast(2));
            Assert.That(messages2.Count, Is.EqualTo(2));
            // Verify that message has decrypted correctly
            CollectionAssert.AreEqual(messages, messages2, new MessageComparer());
            var comparer = new MessageEqualityComparer();
            Assert.IsTrue(messages2.Contains(messageCopy2, comparer));
            Assert.IsTrue(messages2.Contains(messageCopy1, comparer));
            Assert.AreEqual(messages2[0].Id, 2);
            Assert.AreEqual(messages2[1].Id, 1);
        }

        [Test]
        public async Task SendReceiveTwoRecepients()
        {
            var senderAddress = GetAddress1();
            var senderStorage = CreateKeyStorageMock1();
            var receiverStorage1 = CreateKeyStorageMock1(); // same seed but another key
            var receiverStorage2 = CreateKeyStorageMock1(); // same seed but another key
            var receiverAddress1 = await GetAddress2Async(receiverStorage1.Object).ConfigureAwait(true);
            var receiverAddress2 = await GetAddress3Async(receiverStorage2.Object).ConfigureAwait(true);

            Message message = CreateMessage(senderAddress, receiverAddress1);
            message.To.Add(receiverAddress2);
            var client = CreateDecClient(); // shared client to emulate transport layer
            Assert.That(senderAddress, Is.Not.EqualTo(receiverAddress1));
            Assert.That(senderAddress, Is.Not.EqualTo(receiverAddress2));
            using var sender = CreateDecMailBox(senderAddress, client.Object, senderStorage.Object);

            var senderFolders = await sender.GetFoldersStructureAsync(default).ConfigureAwait(true);

            Assert.IsNotNull(senderFolders);

            var sent = senderFolders.Where(x => x.IsSent).FirstOrDefault();
            Assert.IsNotNull(sent);
            message.Folder = sent;

            Assert.DoesNotThrowAsync(async () => await sender.SendMessageAsync(message, default).ConfigureAwait(true));
            client.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.AtLeastOnce);

            await CheckReceiverMessageAsync(receiverAddress1, message, client, receiverStorage1.Object).ConfigureAwait(true);
            await CheckReceiverMessageAsync(receiverAddress2, message, client, receiverStorage2.Object).ConfigureAwait(true);
        }

        private static async Task CheckReceiverMessageAsync(EmailAddress receiverAddress, Message message, Mock<IDecStorageClient> client, IDecStorage keyStorage)
        {
            using var receiver = CreateDecMailBox(receiverAddress, client.Object, keyStorage);
            var receiverFolders = await receiver.GetFoldersStructureAsync(default).ConfigureAwait(true);
            Assert.IsNotNull(receiverFolders);
            var messageCopy = message.ShallowCopy();
            messageCopy.IsMarkedAsRead = false; // SendAsync sets this flag
            var inbox = await receiver.GetDefaultInboxFolderAsync(default).ConfigureAwait(true);
            Assert.IsNotNull(inbox);
            messageCopy.Folder = inbox;

            var messages = await receiver.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true);
            client.Verify(x => x.ListAsync(It.IsAny<string>()), Times.AtLeastOnce);
            Assert.That(messages.Count, Is.EqualTo(1));
            var messages2 = await receiver.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true);
            client.Verify(x => x.ListAsync(It.IsAny<string>()), Times.AtLeast(2));
            Assert.That(messages2.Count, Is.EqualTo(1));
            // Verify that message has decrypted correctly
            Assert.IsTrue(AreEqualMessages(messages[0], messages2[0]));
            Assert.IsTrue(AreEqualMessages(messages2[0], messageCopy));
        }

        private static DecMailBox CreateDecMailBox(EmailAddress address, IDecStorageClient client, IDecStorage storage)
        {
            var senderAccount = new Account()
            {
                Email = address,
                KeyTag = address.Name
            };
            return new DecMailBox(senderAccount, storage, client, new PgpDecProtector(storage));
        }

        [Test]
        public async Task DecEncryptMessage()
        {
            DateTime KeyCreationTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            var from = GetAddress1();
            var message = CreateMessage(from, from);
            var storageMock = CreateKeyStorageMock1();
            var storage = storageMock.Object;
            using var pgpContext = await EccPgpExtension.GetTemporalContextAsync(storage).ConfigureAwait(true);
            var masterKey = await storage.GetMasterKeyAsync().ConfigureAwait(true);
            pgpContext.DeriveKeyForDec(masterKey, from.StandardAddress, from.Name);
            var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext);

            var messageToSign = message.ShallowCopy();
            Assert.DoesNotThrow(() => messageToSign = messageProtector.Sign(messageToSign));

            var messageToSignAndEncrypt = message.ShallowCopy();
            Assert.DoesNotThrow(() => messageToSignAndEncrypt = messageProtector.SignAndEncrypt(messageToSignAndEncrypt));

            var messageToEncrypt = message.ShallowCopy();
            Assert.DoesNotThrow(() => messageToEncrypt = messageProtector.Encrypt(messageToEncrypt));

            using var pgpContext2 = await EccPgpExtension.GetTemporalContextAsync(storage).ConfigureAwait(true);
            var messageProtector2 = MessageProtectorCreator.GetMessageProtector(pgpContext2);

            Assert.DoesNotThrowAsync(() => messageProtector2.TryVerifyAndDecryptAsync(messageToSign));
            Assert.DoesNotThrowAsync(() => messageProtector2.TryVerifyAndDecryptAsync(messageToSign)); // we do the same test two times intentionally
            Assert.ThrowsAsync<NoSecretKeyException>(() => messageProtector2.TryVerifyAndDecryptAsync(messageToSignAndEncrypt));
            Assert.DoesNotThrowAsync(() => messageProtector.TryVerifyAndDecryptAsync(messageToSignAndEncrypt));
            Assert.ThrowsAsync<NoSecretKeyException>(() => messageProtector2.TryVerifyAndDecryptAsync(messageToEncrypt));
            Assert.DoesNotThrowAsync(() => messageProtector.TryVerifyAndDecryptAsync(messageToEncrypt));
        }

        [Test]
        public async Task DecSeveralTransportsNoFaults()
        {
            var address = GetAddress1();
            Message message = CreateMessage(address, address);
            var storage = CreateKeyStorageMock1();
            var client1 = CreateDecClient();
            var client2 = CreateDecClient();
            var clients = new List<IDecStorageClient>() { client1.Object, client2.Object };
            var account = new Account()
            {
                Email = address,
                KeyTag = address.Name
            };
            using var mailBox = new DecMailBox(account, storage.Object, clients, new PgpDecProtector(storage.Object));

            var folders = await mailBox.GetFoldersStructureAsync(default).ConfigureAwait(true);
            var inbox = folders.Where(x => x.IsInbox).First();
            var sent = folders.Where(x => x.IsSent).FirstOrDefault();
            message.Folder = sent;
            await mailBox.SendMessageAsync(message, default).ConfigureAwait(true);
            var messages = await mailBox.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(1));

            // Both clients should be called
            client1.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.AtLeastOnce);
            client1.Verify(x => x.ListAsync(It.IsAny<string>()), Times.AtLeastOnce);
            client1.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);

            client2.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.AtLeastOnce);
            client2.Verify(x => x.ListAsync(It.IsAny<string>()), Times.AtLeastOnce);
            client2.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Test]
        [Ignore("The concept of unreliable transportation has yet to be implemented")]
        public async Task DecSeveralTransportsWithFaults()
        {
            var address = GetAddress1();
            Message message = CreateMessage(address, address);
            var storage = CreateKeyStorageMock1();
            var client1 = CreateFaultyDecClient();
            var client2 = CreateDecClient();
            var client3 = CreateFaultyDecClient();
            var clients = new List<IDecStorageClient>() { client1.Object, client2.Object, client3.Object };
            var account = new Account()
            {
                Email = address,
                KeyTag = address.Name
            };
            using var mailBox = new DecMailBox(account, storage.Object, clients, new PgpDecProtector(storage.Object));

            var folders = await mailBox.GetFoldersStructureAsync(default).ConfigureAwait(true);
            var inbox = folders.Where(x => x.IsInbox).First();
            var sent = folders.Where(x => x.IsSent).FirstOrDefault();
            message.Folder = sent;
            await mailBox.SendMessageAsync(message, default).ConfigureAwait(true);
            var messages = await mailBox.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(1));

            // All clients should be called
            client1.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.AtLeastOnce);
            client1.Verify(x => x.ListAsync(It.IsAny<string>()), Times.AtLeastOnce);
            client1.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);

            client2.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.AtLeastOnce);
            client2.Verify(x => x.ListAsync(It.IsAny<string>()), Times.AtLeastOnce);
            client2.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);

            client3.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.AtLeastOnce);
            client3.Verify(x => x.ListAsync(It.IsAny<string>()), Times.AtLeastOnce);
            client3.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task DecAllTransportsWithFaults()
        {
            var address = GetAddress1();
            Message message = CreateMessage(address, address);
            var storage = CreateKeyStorageMock1();
            var client1 = CreateFaultyDecClient();
            var client2 = CreateFaultyDecClient();
            var clients = new List<IDecStorageClient>() { client1.Object, client2.Object };
            var account = new Account()
            {
                Email = address
            };
            using var mailBox = new DecMailBox(account, storage.Object, clients, new PgpDecProtector(storage.Object));

            var folders = await mailBox.GetFoldersStructureAsync(default).ConfigureAwait(true);
            var inbox = folders.Where(x => x.IsInbox).First();
            var sent = folders.Where(x => x.IsSent).FirstOrDefault();
            message.Folder = sent;

            Assert.ThrowsAsync<DecException>( () => mailBox.SendMessageAsync(message, default));
            
            IReadOnlyList<Message> messages = null;
            Assert.ThrowsAsync<DecException>(async () => { messages = await mailBox.GetMessagesAsync(inbox, 100, default).ConfigureAwait(true); });
            Assert.IsNull(messages);

            // Both clients should be called
            client1.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.AtLeastOnce);
            client1.Verify(x => x.ListAsync(It.IsAny<string>()), Times.AtLeastOnce);
            client1.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            client2.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.AtLeastOnce);
            client2.Verify(x => x.ListAsync(It.IsAny<string>()), Times.AtLeastOnce);
            client2.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        private static Mock<IDecStorageClient> CreateDecClient()
        {
            var decMessages = new Dictionary<string, Dictionary<string, byte[]>>();
            var client = new Mock<IDecStorageClient>();
            client.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
                  .ReturnsAsync((string address, byte[] data) =>
                  {
                      var hash = GetSHA256(data);
                      Dictionary<string, byte[]> messages;
                      if (decMessages.TryGetValue(address, out messages) == false)
                      {
                          messages = new Dictionary<string, byte[]>(); ;
                          decMessages.Add(address, messages);
                      }
                      messages[hash] = data;
                      return hash;
                  });
            client.Setup(x => x.ListAsync(It.IsAny<string>()))
                  .ReturnsAsync(
                (string address) =>
                {
                    return decMessages[address].Keys.ToList();
                });
            client.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                  .ReturnsAsync(
                (string address, string hash) =>
                {
                    return decMessages[address][hash];
                });
            return client;
        }

        private static Mock<IDecStorageClient> CreateFaultyDecClient()
        {
            var decMessages = new Dictionary<string, Dictionary<string, byte[]>>();
            var client = new Mock<IDecStorageClient>();
            client.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
                  .ReturnsAsync((string address, byte[] data) =>
                  {
                      throw new DecException();
                  });
            client.Setup(x => x.ListAsync(It.IsAny<string>()))
                  .ReturnsAsync(
                (string address) =>
                {
                    throw new DecException();
                });
            client.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                  .ReturnsAsync(
                (string address, string hash) =>
                {
                    throw new DecException();
                });
            return client;
        }

        private static Mock<IDecStorage> CreateKeyStorageMock1()
        {
            return CreateKeyStorageMock(TestSeedPhrase1);
        }

        private Task<IDataStorage> CreateKeyStorage1Async()
        {
            return CreateKeyStorageAsync(TestSeedPhrase1);
        }

        private int _storageId;

        private async Task<IDataStorage> CreateKeyStorageAsync(string[] seedPhrase)
        {
            EncryptionTestsData.CreateTestMasterKeyForSeed(seedPhrase);
            var keyFactory = new MasterKeyFactory(new ImplementationDetailsProvider("Tuvi seed", "Tuvi.Package", "backup@test"));
            keyFactory.RestoreSeedPhrase(seedPhrase);
            var masterKey = keyFactory.GetMasterKey();
            string path = $"test{_storageId++}.db";
            File.Delete(path);
            var storage = DataStorageProvider.GetDataStorage(path);
            await storage.CreateAsync("123").ConfigureAwait(false);
            await storage.InitializeMasterKeyAsync(masterKey, default).ConfigureAwait(false);
            return storage;
        }

        private static Mock<IDecStorage> CreateKeyStorageMock(string[] seedPhrase)
        {
            var messages = new Dictionary<EmailAddress, List<DecMessage>>();
            var storage = new Mock<IDecStorage>();
            storage.Setup(x => x.GetMasterKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EncryptionTestsData.CreateTestMasterKeyForSeed(seedPhrase));
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
                   // TODO: uncomment 
                   //Folder.Equals(other.Folder) &&
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

        private static EmailAddress GetAddress1()
        {
            return new EmailAddress("ahgp4kj6bd68xntwsvv4tukxe2pxmrcve7p9u5hmzi6mwmrrz476f@eppie", "Decentralized Account Demo #1");
        }

        private static Task<EmailAddress> GetAddress1Async(IKeyStorage keyStorage)
        {
            return GetDecAddressAsync(keyStorage, "Decentralized Account Demo #1");
        }

        private static Task<EmailAddress> GetAddress2Async(IKeyStorage keyStorage)
        {
            return GetDecAddressAsync(keyStorage, "Decentralized Account Demo #2");
        }

        private static Task<EmailAddress> GetAddress3Async(IKeyStorage keyStorage)
        {
            return GetDecAddressAsync(keyStorage, "Decentralized Account Demo #3");
        }

        private static readonly string[] TestSeedPhrase1 = {
            "apple", "apple", "apple", "apple", "apple", "apple",
            "apple", "apple", "apple", "apple", "apple", "apple"
        };
    }
}
