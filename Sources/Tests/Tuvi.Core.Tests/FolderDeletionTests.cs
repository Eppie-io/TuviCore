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
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Tuvi.Core.Backup;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Dec;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;
using Tuvi.Core.Mail;

namespace Tuvi.Core.Tests
{
    [TestFixture]
    public class FolderDeletionTests
    {
        private static Mock<ISecurityManager> InitMockSecurityManager()
        {
            var securityManagerMock = new Mock<ISecurityManager>();
            var backupProtector = new Mock<IBackupProtector>();
            securityManagerMock.Setup(a => a.GetBackupProtector()).Returns(backupProtector.Object);
            return securityManagerMock;
        }

        [Test]
        public async Task DeleteFolderAsyncValidInputShouldDeleteFolder()
        {
            // Arrange
            var accountsList = new List<Account>() { TestAccountInfo.GetAccount() };
            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var mailBoxMock = new Mock<IMailBox>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            var decStorageClient = new Mock<IDecStorageClient>();

            dataStorageMock.Setup(a => a.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accountsList);
            dataStorageMock.Setup(a => a.GetAccountAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(accountsList[0]);

            var testFolder = new Folder("TestFolder", FolderAttributes.None)
            {
                Id = 10,
                AccountId = 1,
                AccountEmail = accountsList[0].Email
            };

            mailBoxMock.Setup(m => m.DeleteFolderAsync(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mailBoxMock.Setup(m => m.GetFoldersStructureAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Folder>());
            mailBoxMock.Setup(m => m.GetDefaultInboxFolderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Folder("INBOX", FolderAttributes.Inbox));
            mailBoxMock.Setup(m => m.HasFolderCounters).Returns(true);

            dataStorageMock.Setup(d => d.DeleteFolderAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            mailBoxFactoryMock.Setup(f => f.CreateMailBox(It.IsAny<Account>()))
                .Returns(mailBoxMock.Object);

            using var core = TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"),
                decStorageClient.Object);

            FolderDeletedEventArgs eventArgs = null;
            core.FolderDeleted += (sender, args) => eventArgs = args;

            // Act
            await core.DeleteFolderAsync(accountsList[0].Email, testFolder).ConfigureAwait(false);

            // Assert
            Assert.That(eventArgs, Is.Not.Null);
            Assert.That(eventArgs.Folder.FullName, Is.EqualTo("TestFolder"));
            Assert.That(eventArgs.AccountEmail, Is.EqualTo(accountsList[0].Email));
            mailBoxMock.Verify(m => m.DeleteFolderAsync(testFolder, It.IsAny<CancellationToken>()), Times.Once);
            dataStorageMock.Verify(d => d.DeleteFolderAsync(accountsList[0].Email, testFolder.FullName, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void DeleteFolderAsyncNullAccountEmailShouldThrowArgumentNullException()
        {
            // Arrange
            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            var decStorageClient = new Mock<IDecStorageClient>();

            using var core = TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"),
                decStorageClient.Object);

            var testFolder = new Folder("TestFolder", FolderAttributes.None);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await core.DeleteFolderAsync(null, testFolder).ConfigureAwait(false));
        }

        [Test]
        public void DeleteFolderAsyncNullFolderShouldThrowArgumentNullException()
        {
            // Arrange
            var accountsList = new List<Account>() { TestAccountInfo.GetAccount() };
            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var mailBoxMock = new Mock<IMailBox>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            var decStorageClient = new Mock<IDecStorageClient>();

            dataStorageMock.Setup(a => a.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accountsList);
            dataStorageMock.Setup(a => a.GetAccountAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(accountsList[0]);

            mailBoxMock.Setup(m => m.GetFoldersStructureAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Folder>());
            mailBoxMock.Setup(m => m.GetDefaultInboxFolderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Folder("INBOX", FolderAttributes.Inbox));

            mailBoxFactoryMock.Setup(f => f.CreateMailBox(It.IsAny<Account>()))
                .Returns(mailBoxMock.Object);

            using var core = TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"),
                decStorageClient.Object);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await core.DeleteFolderAsync(accountsList[0].Email, null).ConfigureAwait(false));
        }

        [Test]
        public async Task DeleteFolderAsyncUpdatesFolderStructure()
        {
            // Arrange
            var accountsList = new List<Account>() { TestAccountInfo.GetAccount() };
            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var mailBoxMock = new Mock<IMailBox>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            var decStorageClient = new Mock<IDecStorageClient>();

            dataStorageMock.Setup(a => a.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accountsList);
            dataStorageMock.Setup(a => a.GetAccountAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(accountsList[0]);

            var testFolder = new Folder("OldFolder", FolderAttributes.None)
            {
                Id = 10,
                AccountId = 1,
                AccountEmail = accountsList[0].Email
            };

            var updatedFolders = new List<Folder>
            {
                new Folder("INBOX", FolderAttributes.Inbox)
            };

            mailBoxMock.Setup(m => m.DeleteFolderAsync(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mailBoxMock.Setup(m => m.GetFoldersStructureAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(updatedFolders);
            mailBoxMock.Setup(m => m.GetDefaultInboxFolderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Folder("INBOX", FolderAttributes.Inbox));
            mailBoxMock.Setup(m => m.HasFolderCounters).Returns(true);

            dataStorageMock.Setup(d => d.DeleteFolderAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            mailBoxFactoryMock.Setup(f => f.CreateMailBox(It.IsAny<Account>()))
                .Returns(mailBoxMock.Object);

            using var core = TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"),
                decStorageClient.Object);

            // Act
            await core.DeleteFolderAsync(accountsList[0].Email, testFolder).ConfigureAwait(false);

            // Assert - Verify that GetFoldersStructureAsync is called during folder structure update
            mailBoxMock.Verify(m => m.GetFoldersStructureAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Test]
        public void DeleteFolderAsyncProtonMailAccountShouldThrowNotSupportedException()
        {
            // Arrange
            var protonAccount = new Account
            {
                Id = 1,
                Email = new EmailAddress("user@protonmail.com"),
                Type = MailBoxType.Proton,
                IncomingServerAddress = "127.0.0.1",
                OutgoingServerAddress = "127.0.0.1",
                IncomingServerPort = 1143,
                OutgoingServerPort = 1025,
                AuthData = new BasicAuthData() { Password = "test" }
            };
            var accountsList = new List<Account>() { protonAccount };

            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var mailBoxMock = new Mock<IMailBox>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            var decStorageClient = new Mock<IDecStorageClient>();

            dataStorageMock.Setup(a => a.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accountsList);
            dataStorageMock.Setup(a => a.GetAccountAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(protonAccount);

            var testFolder = new Folder("TestFolder", FolderAttributes.None);

            mailBoxMock.Setup(m => m.DeleteFolderAsync(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotSupportedException("Proton Mail does not support folder deletion."));
            mailBoxMock.Setup(m => m.GetFoldersStructureAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Folder>());
            mailBoxMock.Setup(m => m.GetDefaultInboxFolderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Folder("INBOX", FolderAttributes.Inbox));

            mailBoxFactoryMock.Setup(f => f.CreateMailBox(It.IsAny<Account>()))
                .Returns(mailBoxMock.Object);

            using var core = TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"),
                decStorageClient.Object);

            // Act & Assert
            Assert.ThrowsAsync<NotSupportedException>(async () =>
                await core.DeleteFolderAsync(protonAccount.Email, testFolder).ConfigureAwait(false));
        }

        [Test]
        public void DeleteFolderAsyncDecAccountShouldThrowNotSupportedException()
        {
            // Arrange
            var decAccount = new Account
            {
                Id = 1,
                Email = new EmailAddress("user@eppie.io"),
                Type = MailBoxType.Dec,
                IncomingServerAddress = "127.0.0.1",
                OutgoingServerAddress = "127.0.0.1",
                IncomingServerPort = 1143,
                OutgoingServerPort = 1025,
                AuthData = new BasicAuthData() { Password = "test" }
            };
            var accountsList = new List<Account>() { decAccount };

            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var mailBoxMock = new Mock<IMailBox>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            var decStorageClient = new Mock<IDecStorageClient>();

            dataStorageMock.Setup(a => a.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accountsList);
            dataStorageMock.Setup(a => a.GetAccountAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(decAccount);

            var testFolder = new Folder("TestFolder", FolderAttributes.None);

            mailBoxMock.Setup(m => m.DeleteFolderAsync(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotSupportedException("DEC protocol does not support folder deletion."));
            mailBoxMock.Setup(m => m.GetFoldersStructureAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Folder>());
            mailBoxMock.Setup(m => m.GetDefaultInboxFolderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Folder("INBOX", FolderAttributes.Inbox));

            mailBoxFactoryMock.Setup(f => f.CreateMailBox(It.IsAny<Account>()))
                .Returns(mailBoxMock.Object);

            using var core = TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"),
                decStorageClient.Object);

            // Act & Assert
            Assert.ThrowsAsync<NotSupportedException>(async () =>
                await core.DeleteFolderAsync(decAccount.Email, testFolder).ConfigureAwait(false));
        }

        [Test]
        public void DeleteFolderAsyncSpecialFolderShouldThrowInvalidOperationException()
        {
            // Arrange
            var accountsList = new List<Account>() { TestAccountInfo.GetAccount() };
            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var mailBoxMock = new Mock<IMailBox>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            var decStorageClient = new Mock<IDecStorageClient>();

            dataStorageMock.Setup(a => a.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accountsList);
            dataStorageMock.Setup(a => a.GetAccountAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(accountsList[0]);

            mailBoxMock.Setup(m => m.GetFoldersStructureAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Folder>());
            mailBoxMock.Setup(m => m.GetDefaultInboxFolderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Folder("INBOX", FolderAttributes.Inbox));

            mailBoxFactoryMock.Setup(f => f.CreateMailBox(It.IsAny<Account>()))
                .Returns(mailBoxMock.Object);

            using var core = TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"),
                decStorageClient.Object);

            // Test each special folder type
            var inboxFolder = new Folder("INBOX", FolderAttributes.Inbox);
            var sentFolder = new Folder("Sent", FolderAttributes.Sent);
            var trashFolder = new Folder("Trash", FolderAttributes.Trash);
            var draftFolder = new Folder("Drafts", FolderAttributes.Draft);
            var junkFolder = new Folder("Spam", FolderAttributes.Junk);
            var importantFolder = new Folder("Important", FolderAttributes.Important);
            var allFolder = new Folder("All", FolderAttributes.All);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await core.DeleteFolderAsync(accountsList[0].Email, inboxFolder).ConfigureAwait(false));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await core.DeleteFolderAsync(accountsList[0].Email, sentFolder).ConfigureAwait(false));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await core.DeleteFolderAsync(accountsList[0].Email, trashFolder).ConfigureAwait(false));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await core.DeleteFolderAsync(accountsList[0].Email, draftFolder).ConfigureAwait(false));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await core.DeleteFolderAsync(accountsList[0].Email, junkFolder).ConfigureAwait(false));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await core.DeleteFolderAsync(accountsList[0].Email, importantFolder).ConfigureAwait(false));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await core.DeleteFolderAsync(accountsList[0].Email, allFolder).ConfigureAwait(false));
        }
    }
}
