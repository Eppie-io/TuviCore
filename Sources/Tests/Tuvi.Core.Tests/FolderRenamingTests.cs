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
    public class FolderRenamingTests
    {
        private static Mock<ISecurityManager> InitMockSecurityManager()
        {
            var securityManagerMock = new Mock<ISecurityManager>();
            var backupProtector = new Mock<IBackupProtector>();
            securityManagerMock.Setup(a => a.GetBackupProtector()).Returns(backupProtector.Object);
            return securityManagerMock;
        }

        [Test]
        public async Task RenameFolderAsyncValidInputShouldRenameFolder()
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

            var testFolder = new Folder("OldFolderName", FolderAttributes.None)
            {
                Id = 10,
                AccountId = 1,
                AccountEmail = accountsList[0].Email
            };

            var renamedFolder = new Folder("NewFolderName", FolderAttributes.None)
            {
                Id = 10,
                AccountId = 1,
                AccountEmail = accountsList[0].Email
            };

            mailBoxMock.Setup(m => m.RenameFolderAsync(It.IsAny<Folder>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(renamedFolder);
            mailBoxMock.Setup(m => m.GetFoldersStructureAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Folder>());
            mailBoxMock.Setup(m => m.GetDefaultInboxFolderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Folder("INBOX", FolderAttributes.Inbox));
            mailBoxMock.Setup(m => m.HasFolderCounters).Returns(true);

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

            FolderRenamedEventArgs eventArgs = null;
            core.FolderRenamed += (sender, args) => eventArgs = args;

            // Act
            var result = await core.RenameFolderAsync(accountsList[0].Email, testFolder, "NewFolderName").ConfigureAwait(false);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.FullName, Is.EqualTo("NewFolderName"));
            Assert.That(eventArgs, Is.Not.Null);
            Assert.That(eventArgs.Folder.FullName, Is.EqualTo("NewFolderName"));
            Assert.That(eventArgs.AccountEmail, Is.EqualTo(accountsList[0].Email));
            Assert.That(eventArgs.OldName, Is.EqualTo("OldFolderName"));

            // Verify UpdateFolderPathAsync was called to migrate messages
            dataStorageMock.Verify(d => d.UpdateFolderPathAsync(
                accountsList[0].Email,
                "OldFolderName",
                "NewFolderName",
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public void RenameFolderAsyncNullAccountEmailShouldThrow()
        {
            // Arrange
            var accountsList = new List<Account>() { TestAccountInfo.GetAccount() };
            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            var decStorageClient = new Mock<IDecStorageClient>();

            dataStorageMock.Setup(a => a.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accountsList);

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
                await core.RenameFolderAsync(null, testFolder, "NewName").ConfigureAwait(false));
        }

        [Test]
        public void RenameFolderAsyncNullFolderShouldThrow()
        {
            // Arrange
            var accountsList = new List<Account>() { TestAccountInfo.GetAccount() };
            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            var decStorageClient = new Mock<IDecStorageClient>();

            dataStorageMock.Setup(a => a.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accountsList);

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
                await core.RenameFolderAsync(accountsList[0].Email, null, "NewName").ConfigureAwait(false));
        }

        [Test]
        public void RenameFolderAsyncEmptyNameShouldThrow()
        {
            // Arrange
            var accountsList = new List<Account>() { TestAccountInfo.GetAccount() };
            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            var decStorageClient = new Mock<IDecStorageClient>();

            dataStorageMock.Setup(a => a.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accountsList);

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
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await core.RenameFolderAsync(accountsList[0].Email, testFolder, "").ConfigureAwait(false));
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await core.RenameFolderAsync(accountsList[0].Email, testFolder, "   ").ConfigureAwait(false));
        }

        [Test]
        public void RenameFolderAsyncSpecialFolderShouldThrow()
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
            mailBoxMock.Setup(m => m.HasFolderCounters).Returns(true);

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

            var inboxFolder = new Folder("INBOX", FolderAttributes.Inbox);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await core.RenameFolderAsync(accountsList[0].Email, inboxFolder, "NewName").ConfigureAwait(false));
        }

        [Test]
        public void RenameFolderAsyncProtonMailAccountShouldThrowNotSupportedException()
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

            mailBoxMock.Setup(m => m.RenameFolderAsync(It.IsAny<Folder>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotSupportedException("Renaming folders in Proton Mail is not supported."));
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
                await core.RenameFolderAsync(protonAccount.Email, testFolder, "NewName").ConfigureAwait(false));
        }

        [Test]
        public void RenameFolderAsyncDecAccountShouldThrowNotSupportedException()
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

            mailBoxMock.Setup(m => m.RenameFolderAsync(It.IsAny<Folder>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotSupportedException("DEC protocol does not support folder renaming."));
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
                await core.RenameFolderAsync(decAccount.Email, testFolder, "NewName").ConfigureAwait(false));
        }
    }
}
