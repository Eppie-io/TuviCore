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
        private Mock<ISecurityManager> _securityManagerMock;
        private Mock<IMailBoxFactory> _mailBoxFactoryMock;
        private Mock<IMailServerTester> _mailServerTesterMock;
        private Mock<IDataStorage> _dataStorageMock;
        private Mock<IMailBox> _mailBoxMock;
        private Mock<IBackupManager> _backupManagerMock;
        private Mock<ICredentialsManager> _credentialsManagerMock;
        private Mock<IDecStorageClient> _decStorageClientMock;

        [SetUp]
        public void Setup()
        {
            _securityManagerMock = new Mock<ISecurityManager>();
            var backupProtector = new Mock<IBackupProtector>();
            _securityManagerMock.Setup(a => a.GetBackupProtector()).Returns(backupProtector.Object);

            _mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            _mailServerTesterMock = new Mock<IMailServerTester>();
            _dataStorageMock = new Mock<IDataStorage>();
            _mailBoxMock = new Mock<IMailBox>();
            _backupManagerMock = new Mock<IBackupManager>();
            _credentialsManagerMock = new Mock<ICredentialsManager>();
            _decStorageClientMock = new Mock<IDecStorageClient>();

            _mailBoxFactoryMock.Setup(f => f.CreateMailBox(It.IsAny<Account>()))
                .Returns(_mailBoxMock.Object);

            _mailBoxMock.Setup(m => m.GetFoldersStructureAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Folder>());
            _mailBoxMock.Setup(m => m.GetDefaultInboxFolderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Folder("INBOX", FolderAttributes.Inbox));
            _mailBoxMock.Setup(m => m.HasFolderCounters).Returns(true);
        }

        private ITuviMail CreateCore()
        {
            return TuviCoreCreator.CreateTuviMailCore(
                _mailBoxFactoryMock.Object,
                _mailServerTesterMock.Object,
                _dataStorageMock.Object,
                _securityManagerMock.Object,
                _backupManagerMock.Object,
                _credentialsManagerMock.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"),
                _decStorageClientMock.Object);
        }

        private void SetupAccount(Account account)
        {
            var accountsList = new List<Account> { account };
            _dataStorageMock.Setup(a => a.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accountsList);
            _dataStorageMock.Setup(a => a.GetAccountAsync(account.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);
        }

        [Test]
        public async Task RenameFolderAsyncValidInputShouldRenameFolder()
        {
            // Arrange
            var account = TestAccountInfo.GetAccount();
            SetupAccount(account);

            var testFolder = new Folder("OldFolderName", FolderAttributes.None)
            {
                Id = 10,
                AccountId = 1,
                AccountEmail = account.Email
            };

            var renamedFolder = new Folder("NewFolderName", FolderAttributes.None)
            {
                Id = 10,
                AccountId = 1,
                AccountEmail = account.Email
            };

            _mailBoxMock.Setup(m => m.RenameFolderAsync(It.IsAny<Folder>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(renamedFolder);

            using var core = CreateCore();

            FolderRenamedEventArgs eventArgs = null;
            core.FolderRenamed += (sender, args) => eventArgs = args;

            // Act
            var result = await core.RenameFolderAsync(account.Email, testFolder, "NewFolderName").ConfigureAwait(false);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.FullName, Is.EqualTo("NewFolderName"));
            Assert.That(result.Id, Is.EqualTo(10));
            Assert.That(eventArgs, Is.Not.Null);
            Assert.That(eventArgs.Folder, Is.SameAs(result));
            Assert.That(eventArgs.Folder.FullName, Is.EqualTo("NewFolderName"));
            Assert.That(eventArgs.AccountEmail, Is.EqualTo(account.Email));
            Assert.That(eventArgs.OldFullName, Is.EqualTo("OldFolderName"));

            _mailBoxMock.Verify(m => m.RenameFolderAsync(
                testFolder,
                "NewFolderName",
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify UpdateFolderPathAsync was called to migrate messages
            _dataStorageMock.Verify(d => d.UpdateFolderPathAsync(
                account.Email,
                "OldFolderName",
                "NewFolderName",
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Ensure folder structure update was executed
            _mailBoxMock.Verify(m => m.GetFoldersStructureAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Test]
        public void RenameFolderAsyncNullAccountEmailShouldThrow()
        {
            // Arrange
            var account = TestAccountInfo.GetAccount();
            SetupAccount(account);

            using var core = CreateCore();

            var testFolder = new Folder("TestFolder", FolderAttributes.None);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await core.RenameFolderAsync(null, testFolder, "NewName").ConfigureAwait(false));
        }

        [Test]
        public void RenameFolderAsyncNullFolderShouldThrow()
        {
            // Arrange
            var account = TestAccountInfo.GetAccount();
            SetupAccount(account);

            using var core = CreateCore();

            // Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await core.RenameFolderAsync(account.Email, null, "NewName").ConfigureAwait(false));
        }

        [Test]
        public void RenameFolderAsyncEmptyNameShouldThrow()
        {
            // Arrange
            var account = TestAccountInfo.GetAccount();
            SetupAccount(account);

            using var core = CreateCore();

            var testFolder = new Folder("TestFolder", FolderAttributes.None);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await core.RenameFolderAsync(account.Email, testFolder, "").ConfigureAwait(false));
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await core.RenameFolderAsync(account.Email, testFolder, "   ").ConfigureAwait(false));
        }

        [Test]
        public void RenameFolderAsyncSpecialFolderShouldThrow()
        {
            // Arrange
            var account = TestAccountInfo.GetAccount();
            SetupAccount(account);

            using var core = CreateCore();

            var inboxFolder = new Folder("INBOX", FolderAttributes.Inbox);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await core.RenameFolderAsync(account.Email, inboxFolder, "NewName").ConfigureAwait(false));
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
            SetupAccount(protonAccount);

            var testFolder = new Folder("TestFolder", FolderAttributes.None);

            _mailBoxMock.Setup(m => m.RenameFolderAsync(It.IsAny<Folder>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotSupportedException("Renaming folders in Proton Mail is not supported."));

            using var core = CreateCore();

            // Act & Assert
            Assert.ThrowsAsync<NotSupportedException>(async () =>
                await core.RenameFolderAsync(protonAccount.Email, testFolder, "NewName").ConfigureAwait(false));

            _dataStorageMock.Verify(d => d.UpdateFolderPathAsync(
                protonAccount.Email,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Never);

            _mailBoxMock.Verify(m => m.GetFoldersStructureAsync(It.IsAny<CancellationToken>()), Times.Never);
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
            SetupAccount(decAccount);

            var testFolder = new Folder("TestFolder", FolderAttributes.None);

            _mailBoxMock.Setup(m => m.RenameFolderAsync(It.IsAny<Folder>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotSupportedException("DEC protocol does not support folder renaming."));

            using var core = CreateCore();

            // Act & Assert
            Assert.ThrowsAsync<NotSupportedException>(async () =>
                await core.RenameFolderAsync(decAccount.Email, testFolder, "NewName").ConfigureAwait(false));

            _dataStorageMock.Verify(d => d.UpdateFolderPathAsync(
                decAccount.Email,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Never);

            _mailBoxMock.Verify(m => m.GetFoldersStructureAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void RenameFolderAsyncAccountNotFoundShouldThrow()
        {
            // Arrange
            var account = TestAccountInfo.GetAccount();
            _dataStorageMock.Setup(a => a.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Account> { account });
            _dataStorageMock.Setup(a => a.GetAccountAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Account)null);

            using var core = CreateCore();

            var testFolder = new Folder("TestFolder", FolderAttributes.None);

            // Act & Assert
            // Expecting an exception when account is not found.
            // Since specific exception type is not known in this context, catch generic Exception.
            Assert.CatchAsync<Exception>(async () =>
                await core.RenameFolderAsync(account.Email, testFolder, "NewName").ConfigureAwait(false));

            _mailBoxMock.Verify(m => m.RenameFolderAsync(It.IsAny<Folder>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void RenameFolderAsyncSameNameShouldThrow()
        {
            // Arrange
            var account = TestAccountInfo.GetAccount();
            SetupAccount(account);

            var testFolder = new Folder("SameName", FolderAttributes.None)
            {
                Id = 10,
                AccountId = 1,
                AccountEmail = account.Email
            };

            using var core = CreateCore();

            // Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                            await core.RenameFolderAsync(account.Email, testFolder, "SameName").ConfigureAwait(false));

            // Verify MailBox.RenameFolderAsync is never called
            _mailBoxMock.Verify(m => m.RenameFolderAsync(
                It.IsAny<Folder>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Never);

            // Verify UpdateFolderPathAsync is never called
            _dataStorageMock.Verify(d => d.UpdateFolderPathAsync(
                It.IsAny<EmailAddress>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public void RenameFolderAsyncStorageFailureShouldThrowAndNotUpdateLocal()
        {
            // Arrange
            var account = TestAccountInfo.GetAccount();
            SetupAccount(account);

            var testFolder = new Folder("OldFolderName", FolderAttributes.None)
            {
                Id = 10,
                AccountId = 1,
                AccountEmail = account.Email
            };

            _mailBoxMock.Setup(m => m.RenameFolderAsync(It.IsAny<Folder>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.IO.IOException("Connection failed"));

            using var core = CreateCore();

            // Act & Assert
            var ex = Assert.CatchAsync<System.IO.IOException>(async () =>
                await core.RenameFolderAsync(account.Email, testFolder, "NewFolderName").ConfigureAwait(false));

            // Verify exception message
            Assert.That(ex.Message, Is.EqualTo("Connection failed"));

            // Verify UpdateFolderPathAsync was NOT called
            _dataStorageMock.Verify(d => d.UpdateFolderPathAsync(
                It.IsAny<EmailAddress>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
