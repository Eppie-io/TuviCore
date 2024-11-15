using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;
using Tuvi.Core.Mail;

namespace Tuvi.Core.Tests
{
    public class BasicOperationsTests : CoreTestBase
    {
        [SetUp]
        public async Task SetupAsync()
        {
            DeleteStorage();

            await CreateDataStorageAsync().ConfigureAwait(true);
        }

        [TearDown]
        public void TearDown()
        {
            DeleteStorage();
        }

        [Test]
        public async Task DeleteMessagesShouldDeleteMessagesAndRaiseEvents()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var message1 = CreateMessage(1);
            var message101 = CreateMessage(101);
            var messages = new List<Message>() { message1, message101 };
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);
            var deletedMessages = new List<uint>();
            core.MessageDeleted += (s, e) =>
            {
                deletedMessages.Add(e.MessageID);
            };

            var storedMessages = await core.GetFolderEarlierMessagesAsync(account.DefaultInboxFolder, 100, null).ConfigureAwait(true);


            await core.DeleteMessagesAsync(storedMessages, default).ConfigureAwait(true);
            storedMessages = await core.GetFolderEarlierMessagesAsync(account.DefaultInboxFolder, 100, null).ConfigureAwait(true);

            Assert.That(storedMessages.Count, Is.EqualTo(0));
            Assert.That(deletedMessages.Count, Is.EqualTo(2));
            deletedMessages.Sort();
            Assert.That(deletedMessages[0], Is.EqualTo(1));
            Assert.That(deletedMessages[1], Is.EqualTo(101));
        }

        [Test]
        public async Task GetUnreadForAllAccountsOneAccount()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account1 = GetTestEmailAccount();
            var account2 = GetTestEmailAccount(new EmailAddress("account@mail.box"));
            await dataStorage.AddAccountAsync(account1).ConfigureAwait(true);
            await dataStorage.AddAccountAsync(account2).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account1.Email, default).ConfigureAwait(true);
            var message1 = CreateMessage(1, false);
            var message101 = CreateMessage(101, false);
            var messages = new List<Message>() { message1, message101 };
            await accountService.AddMessagesToDataStorageAsync(account1.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);

            var count = await core.GetUnreadCountForAllAccountsAsync(default).ConfigureAwait(true);
            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public async Task GetUnreadForAllAccountsTwoAccounts()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account1 = GetTestEmailAccount();
            var account2 = GetTestEmailAccount(new EmailAddress("account@mail.box"));
            await dataStorage.AddAccountAsync(account1).ConfigureAwait(true);
            await dataStorage.AddAccountAsync(account2).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account1.Email, default).ConfigureAwait(true);
            var message1 = CreateMessage(1, false);
            var message101 = CreateMessage(101, false);
            var messages = new List<Message>() { message1, message101 };
            await accountService.AddMessagesToDataStorageAsync(account1.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);

            var message21 = CreateMessage(21, true);
            var message2101 = CreateMessage(2101, false);
            var messages2 = new List<Message>() { message21, message2101 };
            await accountService.AddMessagesToDataStorageAsync(account2.DefaultInboxFolder,
                                                               messages2,
                                                               default).ConfigureAwait(true);

            var count = await core.GetUnreadCountForAllAccountsAsync(default).ConfigureAwait(true);
            Assert.That(count, Is.EqualTo(3));
        }

        [Test]
        public async Task GetUnreadForAllAccountsTwoAccountsNoUnreadShouldBeZero()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account1 = GetTestEmailAccount();
            var account2 = GetTestEmailAccount(new EmailAddress("account@mail.box"));
            await dataStorage.AddAccountAsync(account1).ConfigureAwait(true);
            await dataStorage.AddAccountAsync(account2).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account1.Email, default).ConfigureAwait(true);
            var message1 = CreateMessage(1, true);
            var message101 = CreateMessage(101, true);
            var messages = new List<Message>() { message1, message101 };
            await accountService.AddMessagesToDataStorageAsync(account1.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);

            var message21 = CreateMessage(21, true);
            var message2101 = CreateMessage(2101, true);
            var messages2 = new List<Message>() { message21, message2101 };
            await accountService.AddMessagesToDataStorageAsync(account2.DefaultInboxFolder,
                                                               messages2,
                                                               default).ConfigureAwait(true);

            var count = await core.GetUnreadCountForAllAccountsAsync(default).ConfigureAwait(true);
            Assert.That(count, Is.EqualTo(0));
        }


        [Test]
        public async Task MarkMessageAsReadExistingMessages()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var message1 = CreateMessage(1, false);
            var message101 = CreateMessage(101, false);
            var messages = new List<Message>() { message1, message101 };
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);
            var changedMessages = new List<Message>();
            core.MessagesIsReadChanged += (s, e) =>
            {
                changedMessages.AddRange(e.Messages);
            };

            await accountService.MarkMessageAsReadAsync(message1).ConfigureAwait(true);
            await accountService.MarkMessagesAsReadAsync(messages).ConfigureAwait(true);

            // TODO: we should not notify twice about the same element
            Assert.That(changedMessages.Count, Is.EqualTo(3));
            var unread = await accountService.GetUnreadMessagesCountInFolderAsync(account.DefaultInboxFolder).ConfigureAwait(true);
            Assert.That(unread, Is.EqualTo(0));
        }

        [Test]
        [Ignore("Currently we send changed message event event if nothing happened")]
        public async Task MarkMessageAsReadNonExistingMessages()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var message1 = CreateMessage(1, false);
            var message101 = CreateMessage(101, false);
            var messages = new List<Message>() { message1, message101 };
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);
            var changedMessages = new List<Message>();
            core.MessagesIsReadChanged += (s, e) =>
            {
                changedMessages.AddRange(e.Messages);
            };

            var message22 = CreateMessage(22, false);

            await accountService.MarkMessageAsReadAsync(message22).ConfigureAwait(true);

            // TODO: we should not notify twice about the same element
            Assert.That(changedMessages.Count, Is.EqualTo(0));
            var unread = await accountService.GetUnreadMessagesCountInFolderAsync(account.DefaultInboxFolder).ConfigureAwait(true);
            Assert.That(unread, Is.EqualTo(2));
        }

        [Test]
        public async Task MarkMessageAsUnReadExistingMessages()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var message1 = CreateMessage(1, true);
            var message101 = CreateMessage(101, true);
            var messages = new List<Message>() { message1, message101 };
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);
            var changedMessages = new List<Message>();
            core.MessagesIsReadChanged += (s, e) =>
            {
                changedMessages.AddRange(e.Messages);
            };

            await accountService.MarkMessageAsUnReadAsync(message1).ConfigureAwait(true);
            await accountService.MarkMessagesAsUnReadAsync(messages).ConfigureAwait(true);

            // TODO: we should not notify twice about the same element
            Assert.That(changedMessages.Count, Is.EqualTo(3));
            var unread = await accountService.GetUnreadMessagesCountInFolderAsync(account.DefaultInboxFolder).ConfigureAwait(true);
            Assert.That(unread, Is.EqualTo(2));
        }


        [Test]
        public async Task MarkMessageAsFlaggedExistingMessages()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var message1 = CreateMessage(1, true);
            var message101 = CreateMessage(101, true);
            var messages = new List<Message>() { message1, message101 };
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);
            var changedMessages = new List<Message>();
            core.MessagesIsFlaggedChanged += (s, e) =>
            {
                changedMessages.AddRange(e.Messages);
            };

            await accountService.FlagMessageAsync(message1).ConfigureAwait(true);

            // TODO: we should not notify twice about the same element
            Assert.That(changedMessages.Count, Is.EqualTo(1));
            var storedMessages = (await core.GetFolderEarlierMessagesAsync(account.DefaultInboxFolder, 100, null).ConfigureAwait(true)).OrderBy(x => x.Id).ToList();
            Assert.That(storedMessages.Count, Is.EqualTo(2));
            Assert.That(storedMessages[0].Id, Is.EqualTo(1));
            Assert.That(storedMessages[0].IsFlagged, Is.True);
            Assert.That(storedMessages[1].IsFlagged, Is.False);
        }

        [Test]
        public async Task MarkMessageAsUnFlaggedExistingMessages()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var message1 = CreateMessage(1, true);
            var message101 = CreateMessage(101, true);
            var messages = new List<Message>() { message1, message101 };
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);

            await accountService.FlagMessageAsync(message1).ConfigureAwait(true);

            var changedMessages = new List<Message>();
            core.MessagesIsFlaggedChanged += (s, e) =>
            {
                changedMessages.AddRange(e.Messages);
            };

            await accountService.UnflagMessageAsync(message1).ConfigureAwait(true);

            // TODO: we should not notify twice about the same element
            Assert.That(changedMessages.Count, Is.EqualTo(1));
            var storedMessages = (await core.GetFolderEarlierMessagesAsync(account.DefaultInboxFolder, 100, null).ConfigureAwait(true)).OrderBy(x => x.Id).ToList();
            Assert.That(storedMessages.Count, Is.EqualTo(2));
            Assert.That(storedMessages[0].Id, Is.EqualTo(1));
            Assert.That(storedMessages[0].IsFlagged, Is.False);
            Assert.That(storedMessages[1].IsFlagged, Is.False);
        }


        [Test]
        public async Task FlagExistingMessages()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var message1 = CreateMessage(1, true);
            var message101 = CreateMessage(101, true);
            var messages = new List<Message>() { message1, message101 };
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);
            var changedMessages = new List<Message>();
            core.MessagesIsFlaggedChanged += (s, e) =>
            {
                changedMessages.AddRange(e.Messages);
            };

            await accountService.FlagMessageAsync(message1).ConfigureAwait(true);
            await accountService.FlagMessagesAsync(messages).ConfigureAwait(true);

            // TODO: we should not notify twice about the same element
            Assert.That(changedMessages.Count, Is.EqualTo(3));
            var storedMessages = (await core.GetFolderEarlierMessagesAsync(account.DefaultInboxFolder, 100, null).ConfigureAwait(true)).OrderBy(x => x.Id).ToList();
            Assert.That(storedMessages.Count, Is.EqualTo(2));
            Assert.That(storedMessages[0].IsFlagged, Is.True);
            Assert.That(storedMessages[1].IsFlagged, Is.True);
        }

        [Test]
        public async Task UnflagExistingMessages()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var message1 = CreateFlaggedMessage(1);
            var message101 = CreateFlaggedMessage(101);
            var messages = new List<Message>() { message1, message101 };
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);
            var changedMessages = new List<Message>();
            core.MessagesIsFlaggedChanged += (s, e) =>
            {
                changedMessages.AddRange(e.Messages);
            };

            await accountService.UnflagMessageAsync(message1).ConfigureAwait(true);
            await accountService.UnflagMessagesAsync(messages).ConfigureAwait(true);

            // TODO: we should not notify twice about the same element
            Assert.That(changedMessages.Count, Is.EqualTo(3));
            var storedMessages = (await core.GetFolderEarlierMessagesAsync(account.DefaultInboxFolder, 100, null).ConfigureAwait(true)).OrderBy(x => x.Id).ToList();
            Assert.That(storedMessages.Count, Is.EqualTo(2));
            Assert.That(storedMessages[0].IsFlagged, Is.False);
            Assert.That(storedMessages[1].IsFlagged, Is.False);
        }

        [Test]
        public async Task UpdateDraftCanUpdateMessageId()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var message = CreateMessage(112);
            var messages = new List<Message>() { message };
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);

            // Test mailbox changes external message id
            await accountService.UpdateDraftMessageAsync(112, message).ConfigureAwait(true);

            var storedMessages = (await core.GetFolderEarlierMessagesAsync(account.DefaultInboxFolder, 100, null).ConfigureAwait(true)).OrderBy(x => x.Id).ToList();
            Assert.That(storedMessages.Count, Is.EqualTo(1));
            Assert.That(storedMessages[0].Id == 112, Is.False);
            Assert.That(storedMessages[0].Pk, Is.EqualTo(message.Pk));
        }

        [Test]
        public async Task SendMessageShouldThrow()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            var account = GetTestEmailAccount();
            var mailBox = CreateMockMailbox(account);

            using var core = CreateCore(dataStorage, mailBox.Object);
            await core.AddAccountAsync(account).ConfigureAwait(true);

            Assert.ThrowsAsync<ArgumentNullException>(() => { return core.SendMessageAsync(null, false, false, default); });
        }

        [Test]
        public async Task SendMessageShouldNotThrow()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            var account = GetTestEmailAccount();
            var mailBox = CreateMockMailbox(account);

            using var core = CreateCore(dataStorage, mailBox.Object);
            await core.AddAccountAsync(account).ConfigureAwait(true);

            var message = CreateMessage(0);
            message.From.Add(account.Email);

            Assert.DoesNotThrowAsync(() => { return core.SendMessageAsync(message, false, false, default); });
            mailBox.Verify(x => x.SendMessageAsync(It.IsNotNull<Message>(), default), Times.Once);

            // TODO: uncomment this and fix
            //Assert.That(message.Folder, Is.Not.Null);
            //Assert.That(message.Folder, Is.EqualTo(account.SentFolder));
        }

        [Test]
        public async Task SendSignedEncryptedMessageShouldThrow()
        {
            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            var account = GetTestEmailAccount();
            var mailBox = CreateMockMailbox(account);

            using var core = CreateCore(dataStorage, mailBox.Object);
            await core.AddAccountAsync(account).ConfigureAwait(true);

            Assert.ThrowsAsync<ArgumentNullException>(() => { return core.SendMessageAsync(null, encrypt: true, sign: true, default); });
        }

        [Test]
        public async Task SendSignedEncryptedMessageShouldNotThrow()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            var account = GetTestEmailAccount();
            var mailBox = CreateMockMailbox(account);

            var messageProtector = new Mock<IMessageProtector>();

            using var core = CreateCore(dataStorage, mailBox.Object, messageProtector.Object);
            await core.AddAccountAsync(account).ConfigureAwait(true);

            var message = CreateMessage(0);
            message.From.Add(account.Email);

            Assert.DoesNotThrowAsync(() => { return core.SendMessageAsync(message, encrypt: true, sign: true, It.IsAny<CancellationToken>()); });
            mailBox.Verify(x => x.SendMessageAsync(It.IsNotNull<Message>(), It.IsAny<CancellationToken>()), Times.Once);
            messageProtector.Verify(x => x.SignAndEncrypt(It.IsNotNull<Message>()), Times.Once);
        }

        [Test]
        public async Task SendSignedMessageShouldThrow()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);

            Assert.ThrowsAsync<ArgumentNullException>(() => { return accountService.SendMessageAsync(null, encrypt: false, sign: true, default); });
        }

        [Test]
        public async Task SendSignedMessageShouldNotThrow()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            var account = GetTestEmailAccount();
            var mailBox = CreateMockMailbox(account);
            var messageProtector = new Mock<IMessageProtector>();

            using var core = CreateCore(dataStorage, mailBox.Object, messageProtector.Object);
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);

            var message = CreateMessage(0);
            message.From.Add(account.Email);

            Assert.DoesNotThrowAsync(() => { return core.SendMessageAsync(message, encrypt: false, sign: true, default); });
            mailBox.Verify(x => x.SendMessageAsync(It.IsNotNull<Message>(), It.IsAny<CancellationToken>()), Times.Once);
            messageProtector.Verify(x => x.Sign(It.IsNotNull<Message>()), Times.Once);
        }

        [Test]
        public async Task SendEncryptedMessageShouldThrow()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);

            Assert.ThrowsAsync<ArgumentNullException>(() => { return accountService.SendMessageAsync(null, encrypt: true, sign: false, default); });
        }

        [Test]
        public async Task SendEncryptedMessageShouldNotThrow()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            Account account = GetTestEmailAccount();
            var mailBox = CreateMockMailbox(account);
            var messageProtector = new Mock<IMessageProtector>();

            using var core = CreateCore(dataStorage, mailBox.Object, messageProtector.Object);
            await core.AddAccountAsync(account).ConfigureAwait(true);

            var message = CreateMessage(0);
            message.From.Add(account.Email);

            Assert.DoesNotThrowAsync(() => { return core.SendMessageAsync(message, encrypt: true, sign: false, default); });
            mailBox.Verify(x => x.SendMessageAsync(It.IsNotNull<Message>(), It.IsAny<CancellationToken>()), Times.Once);
            messageProtector.Verify(x => x.Encrypt(It.IsNotNull<Message>()), Times.Once);
        }

        private static Mock<IMailBox> CreateMockMailbox(Account account)
        {
            var mailBox = new Mock<IMailBox>();
            mailBox.Setup(x => x.GetFoldersStructureAsync(It.IsAny<CancellationToken>())).ReturnsAsync(account.FoldersStructure);
            mailBox.Setup(x => x.SendMessageAsync(It.IsNotNull<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            mailBox.Setup(x => x.GetMessagesAsync(It.IsNotNull<Folder>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Message>());
            return mailBox;
        }

        [Test]
        public async Task SynchronizeFullNoRemoteTest()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);

            Assert.DoesNotThrowAsync(async () => await accountService.SynchronizeAsync(true, default).ConfigureAwait(true));
        }

        [Test]
        public async Task SynchronizeFullStorage()
        {
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            var message899 = CreateMessage(899);
            var message1000 = CreateMessage(1000);
            var remoteMessages = new List<Message>
            {
                message1000,
                message899
            };
            using var core = CreateCoreForSynchronization(dataStorage, remoteMessages);
            var account = GetTestEmailAccount();
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               new List<Message>() { message1000 },
                                                               default).ConfigureAwait(true);

            Assert.DoesNotThrowAsync(async () => await accountService.SynchronizeAsync(true, default).ConfigureAwait(true));
            var storedMessages = await core.GetAllEarlierMessagesAsync(100, null, default).ConfigureAwait(true);
            Assert.That(storedMessages.Count, Is.EqualTo(2));
            Assert.That(storedMessages[0].Id, Is.EqualTo(1000));
            Assert.That(storedMessages[1].Id, Is.EqualTo(899));
        }

        private static TuviMail CreateCoreForSynchronization(IDataStorage dataStorage, IReadOnlyList<Message> remoteMessages)
        {
            SyncDataProvider dataProvider = new SyncDataProvider();
            dataProvider.RemoteMessages.AddRange(remoteMessages);
            var mailBox = new Mock<IMailBox>();

            mailBox.Setup(x => x.GetEarlierMessagesForSynchronizationAsync(It.IsAny<Folder>(),
                                                                           It.IsAny<int>(),
                                                                           It.IsAny<Message>(),
                                                                           It.IsAny<CancellationToken>())).Returns((Folder f, int c, Message m, CancellationToken ct) =>
                                                                           {
                                                                               return dataProvider.LoadRemoteMessagesAsync(m, c, ct);
                                                                           });
            mailBox.Setup(x => x.GetEarlierMessagesAsync(It.IsAny<Folder>(),
                                                         It.IsAny<int>(),
                                                         It.IsAny<Message>(),
                                                         It.IsAny<CancellationToken>())).Returns((Folder f, int c, Message m, CancellationToken ct) =>
                                                         {
                                                             return dataProvider.LoadRemoteMessagesAsync(m, c, ct);
                                                         });
            var mailBoxFactory = new Mock<IMailBoxFactory>();
            mailBoxFactory.Setup(x => x.CreateMailBox(It.IsAny<Account>()))
                          .Returns(mailBox.Object);
            var mailServerTester = new Mock<IMailServerTester>();
            var securityManager = new Mock<ISecurityManager>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            credentialsManager.Setup(x => x.CreateCredentialsProvider(It.IsAny<Account>()))
                              .Returns(new Mock<ICredentialsProvider>().Object);
                        
            return new TuviMail(mailBoxFactory.Object,
                                mailServerTester.Object,
                                dataStorage,
                                securityManager.Object,
                                backupManager.Object,
                                credentialsManager.Object,
                                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"));
        }
    }
}
