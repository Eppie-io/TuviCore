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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Tuvi.Core;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using Tuvi.Core.Entities.Exceptions;
using Tuvi.Core.Impl.BackupManagement;
using TuviPgpLib.Entities;

namespace BackupTests
{
    [TestFixture]
    public class BackupManagerTests : BaseBackupTest
    {
        private Mock<IDataStorage> DataStorageMock;
        private Mock<ISecurityManager> SecurityManagerMock;
        private IBackupManager BackupManager;
        private Mock<IBackupDetailsProvider> BackupDetailsProviderMock;

        [OneTimeSetUp]
        protected void InitializeContext()
        {
            Initialize();
        }

        [SetUp]
        public void SetupTest()
        {
            DataStorageMock = new Mock<IDataStorage>();
            SecurityManagerMock = new Mock<ISecurityManager>();
            BackupDetailsProviderMock = new Mock<IBackupDetailsProvider>();

            BackupDetailsProviderMock.Setup(p => p.GetPackageIdentifier())
                .Returns(TestData.BackupPackageIdentifier);

            BackupManager = BackupManagerCreator.GetBackupManager(
                DataStorageMock.Object,
                BackupDataProtector,
                BackupSerializationFactory,
                SecurityManagerMock.Object);
        }

        [Test]
        [Category("Integration")]
        [Category("BackupManager")]
        public void SetBackupDetailsWithNullThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => BackupManager.SetBackupDetails(null));
        }

        [Test]
        [Category("Integration")]
        [Category("BackupManager")]
        public void CreateBackupWithoutSetBackupDetailsThrowsException()
        {
            // Arrange
            using var outputStream = new MemoryStream();

            // Act & Assert
            // BackupFactory is not set because SetBackupDetails was not called
            // This causes ArgumentNullException in BackupManager.CreateBackupAsync when it tries to use accounts
            Assert.ThrowsAsync<ArgumentNullException>(
                () => BackupManager.CreateBackupAsync(outputStream));
        }

        [Test]
        [Category("Integration")]
        [Category("BackupManager")]
        public void RestoreBackupWithoutSetBackupDetailsThrowsException()
        {
            // Arrange
            using var inputStream = new MemoryStream();

            // Act & Assert
            // The InvalidOperationException is wrapped in BackupParsingException during parsing
            Assert.ThrowsAsync<Tuvi.Core.Entities.Exceptions.BackupParsingException>(
                () => BackupManager.RestoreBackupAsync(inputStream));
        }

        [Test]
        [Category("Integration")]
        [Category("BackupManager")]
        public async Task CreateBackupFiltersAccountsByIsBackupAccountSettingsEnabled()
        {
            // Arrange
            var account1 = CreateTestAccount(TestData.User1Email, true, false);
            var account2 = CreateTestAccount(TestData.User2Email, false, false);
            var account3 = CreateTestAccount(TestData.User3Email, true, false);

            var accounts = new List<Account> { account1, account2, account3 };

            DataStorageMock.Setup(d => d.GetAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(accounts);

            DataStorageMock.Setup(d => d.GetSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Settings());

            SecurityManagerMock.Setup(s => s.GetPublicPgpKeysInfo())
                .Returns(new List<PgpKeyInfo>());

            BackupManager.SetBackupDetails(BackupDetailsProviderMock.Object);

            using var outputStream = new MemoryStream();

            // Act
            await BackupManager.CreateBackupAsync(outputStream, CancellationToken.None).ConfigureAwait(true);

            // Assert
            outputStream.Position = 0;
            var parser = BackupSerializationFactory.CreateBackupParser();
            await parser.ParseBackupAsync(outputStream).ConfigureAwait(true);

            var restoredAccounts = await parser.GetAccountsAsync().ConfigureAwait(true);

            Assert.That(restoredAccounts.Count, Is.EqualTo(2));
            Assert.That(restoredAccounts.Any(a => a.Email.Address == TestData.User1Email), Is.True);
            Assert.That(restoredAccounts.Any(a => a.Email.Address == TestData.User2Email), Is.False);
            Assert.That(restoredAccounts.Any(a => a.Email.Address == TestData.User3Email), Is.True);
        }

        [Test]
        [Category("Integration")]
        [Category("BackupManager")]
        public async Task CreateBackupFiltersMessagesByIsBackupAccountMessagesEnabled()
        {
            // Arrange
            var account1 = CreateTestAccount(TestData.User1Email, true, true);
            var account2 = CreateTestAccount(TestData.User2Email, true, false);

            var accounts = new List<Account> { account1, account2 };

            var messagesAccount1 = new List<Message>
            {
                CreateTestMessage(1, "Subject1")
            };

            DataStorageMock.Setup(d => d.GetAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(accounts);

            DataStorageMock.Setup(d => d.GetMessageListAsync(
                    account1.Email,
                    It.IsAny<string>(),
                    It.IsAny<uint>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(messagesAccount1);

            DataStorageMock.Setup(d => d.GetSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Settings());

            SecurityManagerMock.Setup(s => s.GetPublicPgpKeysInfo())
                .Returns(new List<PgpKeyInfo>());

            BackupManager.SetBackupDetails(BackupDetailsProviderMock.Object);

            using var outputStream = new MemoryStream();

            // Act
            await BackupManager.CreateBackupAsync(outputStream, CancellationToken.None).ConfigureAwait(true);

            // Assert
            outputStream.Position = 0;
            var parser = BackupSerializationFactory.CreateBackupParser();
            await parser.ParseBackupAsync(outputStream).ConfigureAwait(true);

            var restoredMessages = await parser.GetMessagesAsync().ConfigureAwait(true);

            Assert.That(restoredMessages.Count, Is.EqualTo(1));
            Assert.That(restoredMessages[0].EmailAccount, Is.EqualTo(TestData.User1Email));
        }

        [Test]
        [Category("Integration")]
        [Category("BackupManager")]
        public async Task RestoreBackupWithVersionMismatchDoesNotRestore()
        {
            // Arrange
            var account = CreateTestAccount(TestData.MismatchTestEmail, true, false);

            var backupData = await CreateTestBackupAsync(new List<Account> { account }).ConfigureAwait(true);

            BackupManager.SetBackupDetails(BackupDetailsProviderMock.Object);

            bool accountRestoredCalled = false;
            BackupManager.AccountRestoredAsync += (acc) =>
            {
                accountRestoredCalled = true;
                return Task.CompletedTask;
            };

            // Act
            using var backup = new MemoryStream(backupData);
            Assert.ThrowsAsync<BackupVersionMismatchException>(() => BackupManager.RestoreBackupAsync(backup, CancellationToken.None));
            Assert.That(accountRestoredCalled, Is.False);
        }

        [Test]
        [Category("Integration")]
        [Category("BackupManager")]
        public async Task CreateDetachedSignatureDataCreatesValidSignature()
        {
            // Arrange
            BackupManager.SetBackupDetails(BackupDetailsProviderMock.Object);

            var testData = System.Text.Encoding.UTF8.GetBytes("Test data to sign");
            using var dataToSign = new MemoryStream(testData);
            using var detachedSignatureData = new MemoryStream();
            using var publicKeyData = new MemoryStream();

            // Act
            await BackupManager.CreateDetachedSignatureDataAsync(
                dataToSign,
                detachedSignatureData,
                publicKeyData,
                CancellationToken.None).ConfigureAwait(true);

            // Assert
            Assert.That(detachedSignatureData.Length, Is.GreaterThan(0));
            Assert.That(publicKeyData.Length, Is.GreaterThan(0));

            dataToSign.Position = 0;
            detachedSignatureData.Position = 0;

            var isVerified = await BackupDataSignatureVerifier.VerifySignatureAsync(
                dataToSign,
                detachedSignatureData).ConfigureAwait(true);

            Assert.That(isVerified, Is.True);
        }

        [Test]
        [Category("Integration")]
        [Category("BackupManager")]
        public async Task FullBackupRestoreCyclePreservesAllData()
        {
            // Arrange
            var account = CreateTestAccount(TestData.FullCycleTestEmail, true, true);
            account.SynchronizationInterval = 15;
            account.ExternalContentPolicy = ExternalContentPolicy.Block;

            var message = CreateTestMessage(TestMessageId, TestData.ImportantMessageSubject);
            message.From.Add(new EmailAddress(TestData.SenderEmailAddress));
            message.To.Add(new EmailAddress(TestData.RecipientEmailAddress));

            var messages = new List<Message> { message };

            var settings = new Settings
            {
                EppieAccountCounter = 5,
                BitcoinAccountCounter = 3
            };

            DataStorageMock.Setup(d => d.GetAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Account> { account });

            DataStorageMock.Setup(d => d.GetMessageListAsync(
                    account.Email,
                    It.IsAny<string>(),
                    It.IsAny<uint>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            DataStorageMock.Setup(d => d.GetSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            SecurityManagerMock.Setup(s => s.GetPublicPgpKeysInfo())
                .Returns(new List<PgpKeyInfo>());

            BackupManager.SetBackupDetails(BackupDetailsProviderMock.Object);

            using var backupStream = new MemoryStream();

            // Act - Create backup
            await BackupManager.CreateBackupAsync(backupStream, CancellationToken.None).ConfigureAwait(true);

            backupStream.Position = 0;

            // Act - Restore backup
            Account restoredAccount = null;
            IReadOnlyList<FolderMessagesBackupContainer> restoredMessages = null;

            BackupManager.AccountRestoredAsync += (acc) =>
            {
                restoredAccount = acc;
                return Task.CompletedTask;
            };

            BackupManager.MessagesRestoredAsync += (email, msgs) =>
            {
                restoredMessages = msgs;
                return Task.CompletedTask;
            };

            await BackupManager.RestoreBackupAsync(backupStream, CancellationToken.None).ConfigureAwait(true);

            // Assert - Verify account
            Assert.That(restoredAccount, Is.Not.Null);
            Assert.That(restoredAccount.Email.Address, Is.EqualTo(TestData.FullCycleTestEmail));
            Assert.That(restoredAccount.SynchronizationInterval, Is.EqualTo(15));
            Assert.That(restoredAccount.ExternalContentPolicy, Is.EqualTo(ExternalContentPolicy.Block));

            // Assert - Verify messages
            Assert.That(restoredMessages, Is.Not.Null);
            var restoredMessage = restoredMessages[0].Messages[0];
            Assert.That(restoredMessage.Id, Is.EqualTo(TestMessageId));
            Assert.That(restoredMessage.Subject, Is.EqualTo(TestData.ImportantMessageSubject));
            Assert.That(restoredMessage.Date, Is.EqualTo(TestData.BackupManagerTestMessageDate));
            Assert.That(restoredMessage.From.Count, Is.EqualTo(1));
            Assert.That(restoredMessage.To.Count, Is.EqualTo(1));

            // Assert - Verify settings were set
            DataStorageMock.Verify(d => d.SetSettingsAsync(
                It.Is<Settings>(s => s.EppieAccountCounter == 5 && s.BitcoinAccountCounter == 3),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #region Helper Methods

        private const uint TestMessageId = 42;

        private static Account CreateTestAccount(string email, bool backupSettings, bool backupMessages)
        {
            var account = new Account
            {
                Email = new EmailAddress(email),
                AuthData = new BasicAuthData { Password = TestData.TestPassword },
                IsBackupAccountSettingsEnabled = backupSettings,
                IsBackupAccountMessagesEnabled = backupMessages,
                FoldersStructure = new List<Folder>
                {
                    new Folder(TestData.InboxFolderName, FolderAttributes.Inbox)
                }
            };

            return account;
        }

        private static Message CreateTestMessage(uint id, string subject)
        {
            return new Message
            {
                Id = id,
                Subject = subject,
                Date = TestData.BackupManagerTestMessageDate,
                TextBody = "Test body",
                IsMarkedAsRead = false,
                IsFlagged = false
            };
        }

        private static async Task<byte[]> CreateTestBackupAsync(List<Account> accounts)
        {
            using var backup = new MemoryStream();
            var builder = BackupSerializationFactory.CreateBackupBuilder();

            await builder.SetVersionAsync(TestData.ProtocolVersion).ConfigureAwait(true);
            await builder.SetAccountsAsync(accounts).ConfigureAwait(true);

            await builder.BuildBackupAsync(backup).ConfigureAwait(true);

            return backup.ToArray();
        }

        #endregion
    }
}
