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

#nullable enable

namespace Tuvi.Core.Tests
{
    [TestFixture]
    public class ClaimDecentralizedNameAsyncTests
    {
        private static Mock<ISecurityManager> CreateMockSecurityManager()
        {
            var securityManagerMock = new Mock<ISecurityManager>();
            var backupProtector = new Mock<IBackupProtector>();
            securityManagerMock.Setup(a => a.GetBackupProtector()).Returns(backupProtector.Object);
            return securityManagerMock;
        }

        private static Account CreateEppieAccount(int accountIndex = 0)
        {
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, "aewcimjjec6kjyk5nv8vy3tvsdwkpbzbyexhswmg3vyemmmk9mce4");
            return new Account
            {
                Email = email,
                DecentralizedAccountIndex = accountIndex,
                Type = MailBoxType.Dec
            };
        }

        private static Account CreateBitcoinAccount(int accountIndex = 0)
        {
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Bitcoin, "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa");
            return new Account
            {
                Email = email,
                DecentralizedAccountIndex = accountIndex,
                Type = MailBoxType.Dec
            };
        }

        private static Account CreateEthereumAccount(int accountIndex = 0)
        {
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Ethereum, "0x742d35Cc6634C0532925a3b844Bc454e4438f44e");
            return new Account
            {
                Email = email,
                DecentralizedAccountIndex = accountIndex,
                Type = MailBoxType.Dec
            };
        }

        private static Account CreateTraditionalAccount()
        {
            return new Account
            {
                Email = new EmailAddress("test@example.com"),
                Type = MailBoxType.Email
            };
        }

        private static ITuviMail CreateTuviMailCore(
            Mock<ISecurityManager> securityManagerMock,
            Mock<IDecStorageClient> decStorageClientMock)
        {
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();

            return TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"),
                decStorageClientMock.Object);
        }

        [Test]
        public void ClaimDecentralizedNameAsyncThrowsOnNullName()
        {
            // Arrange
            var securityManagerMock = CreateMockSecurityManager();
            var decStorageClientMock = new Mock<IDecStorageClient>();
            var account = CreateEppieAccount();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            AsyncTestDelegate act = async () => await core.ClaimDecentralizedNameAsync(null!, account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<ArgumentException>(act);
        }

        [Test]
        public void ClaimDecentralizedNameAsyncThrowsOnEmptyName()
        {
            // Arrange
            var securityManagerMock = CreateMockSecurityManager();
            var decStorageClientMock = new Mock<IDecStorageClient>();
            var account = CreateEppieAccount();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            AsyncTestDelegate act = async () => await core.ClaimDecentralizedNameAsync(string.Empty, account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<ArgumentException>(act);
        }

        [Test]
        public void ClaimDecentralizedNameAsyncThrowsOnWhitespaceName()
        {
            // Arrange
            var securityManagerMock = CreateMockSecurityManager();
            var decStorageClientMock = new Mock<IDecStorageClient>();
            var account = CreateEppieAccount();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            AsyncTestDelegate act = async () => await core.ClaimDecentralizedNameAsync("   ", account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<ArgumentException>(act);
        }

        [Test]
        public void ClaimDecentralizedNameAsyncThrowsOnNullAccount()
        {
            // Arrange
            var securityManagerMock = CreateMockSecurityManager();
            var decStorageClientMock = new Mock<IDecStorageClient>();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            AsyncTestDelegate act = async () => await core.ClaimDecentralizedNameAsync("testname", null!).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<ArgumentNullException>(act);
        }

        [Test]
        public void ClaimDecentralizedNameAsyncThrowsOnBitcoinNetwork()
        {
            // Arrange
            var securityManagerMock = CreateMockSecurityManager();
            var decStorageClientMock = new Mock<IDecStorageClient>();
            var account = CreateBitcoinAccount();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            AsyncTestDelegate act = async () => await core.ClaimDecentralizedNameAsync("testname", account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<NotSupportedException>(act);
        }

        [Test]
        public void ClaimDecentralizedNameAsyncThrowsOnEthereumNetwork()
        {
            // Arrange
            var securityManagerMock = CreateMockSecurityManager();
            var decStorageClientMock = new Mock<IDecStorageClient>();
            var account = CreateEthereumAccount();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            AsyncTestDelegate act = async () => await core.ClaimDecentralizedNameAsync("testname", account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<NotSupportedException>(act);
        }

        [Test]
        public void ClaimDecentralizedNameAsyncThrowsOnTraditionalAccount()
        {
            // Arrange
            var securityManagerMock = CreateMockSecurityManager();
            var decStorageClientMock = new Mock<IDecStorageClient>();
            var account = CreateTraditionalAccount();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            AsyncTestDelegate act = async () => await core.ClaimDecentralizedNameAsync("testname", account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<NotSupportedException>(act);
        }

        [Test]
        public void ClaimDecentralizedNameAsyncThrowsWhenDisposed()
        {
            // Arrange
            var securityManagerMock = CreateMockSecurityManager();
            var decStorageClientMock = new Mock<IDecStorageClient>();
            var account = CreateEppieAccount();
            var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);
            core.Dispose();

            // Act
            AsyncTestDelegate act = async () => await core.ClaimDecentralizedNameAsync("testname", account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<ObjectDisposedException>(act);
        }

        [Test]
        public async Task ClaimDecentralizedNameAsyncReturnsCanonicalNameOnSuccess()
        {
            // Arrange
            const string name = "TestName";
            const string expectedCanonicalName = "testname.test";
            const string publicKey = "aewcimjjec6kjyk5nv8vy3tvsdwkpbzbyexhswmg3vyemmmk9mce4";
            const string signature = "testsignature";

            var securityManagerMock = CreateMockSecurityManager();
            securityManagerMock
                .Setup(sm => sm.GetEmailPublicKeyStringAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKey);
            securityManagerMock
                .Setup(sm => sm.SignNameClaimAsync(It.IsAny<string>(), It.IsAny<Account>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(signature);

            var decStorageClientMock = new Mock<IDecStorageClient>();
            decStorageClientMock
                .Setup(dc => dc.ClaimNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKey);

            var account = CreateEppieAccount();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            var result = await core.ClaimDecentralizedNameAsync(name, account).ConfigureAwait(false);

            // Assert
            Assert.That(result, Is.EqualTo(expectedCanonicalName));
        }

        [Test]
        public async Task ClaimDecentralizedNameAsyncReturnsEmptyOnMismatch()
        {
            // Arrange
            const string name = "TestName";
            const string publicKey = "aewcimjjec6kjyk5nv8vy3tvsdwkpbzbyexhswmg3vyemmmk9mce4";
            const string differentPublicKey = "differentpublickey";
            const string signature = "testsignature";

            var securityManagerMock = CreateMockSecurityManager();
            securityManagerMock
                .Setup(sm => sm.GetEmailPublicKeyStringAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKey);
            securityManagerMock
                .Setup(sm => sm.SignNameClaimAsync(It.IsAny<string>(), It.IsAny<Account>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(signature);

            var decStorageClientMock = new Mock<IDecStorageClient>();
            decStorageClientMock
                .Setup(dc => dc.ClaimNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(differentPublicKey);

            var account = CreateEppieAccount();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            var result = await core.ClaimDecentralizedNameAsync(name, account).ConfigureAwait(false);

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public async Task ClaimDecentralizedNameAsyncCallsSecurityManagerSignNameClaim()
        {
            // Arrange
            const string name = "TestName";
            const string expectedCanonicalName = "testname.test";
            const string publicKey = "aewcimjjec6kjyk5nv8vy3tvsdwkpbzbyexhswmg3vyemmmk9mce4";
            const string signature = "testsignature";

            var securityManagerMock = CreateMockSecurityManager();
            securityManagerMock
                .Setup(sm => sm.GetEmailPublicKeyStringAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKey);
            securityManagerMock
                .Setup(sm => sm.SignNameClaimAsync(It.IsAny<string>(), It.IsAny<Account>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(signature);

            var decStorageClientMock = new Mock<IDecStorageClient>();
            decStorageClientMock
                .Setup(dc => dc.ClaimNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKey);

            var account = CreateEppieAccount();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            await core.ClaimDecentralizedNameAsync(name, account).ConfigureAwait(false);

            // Assert
            securityManagerMock.Verify(
                sm => sm.SignNameClaimAsync(expectedCanonicalName, account, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task ClaimDecentralizedNameAsyncCallsDecStorageClientClaimName()
        {
            // Arrange
            const string name = "TestName";
            const string expectedCanonicalName = "testname.test";
            const string publicKey = "aewcimjjec6kjyk5nv8vy3tvsdwkpbzbyexhswmg3vyemmmk9mce4";
            const string signature = "testsignature";

            var securityManagerMock = CreateMockSecurityManager();
            securityManagerMock
                .Setup(sm => sm.GetEmailPublicKeyStringAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKey);
            securityManagerMock
                .Setup(sm => sm.SignNameClaimAsync(It.IsAny<string>(), It.IsAny<Account>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(signature);

            var decStorageClientMock = new Mock<IDecStorageClient>();
            decStorageClientMock
                .Setup(dc => dc.ClaimNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKey);

            var account = CreateEppieAccount();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            await core.ClaimDecentralizedNameAsync(name, account).ConfigureAwait(false);

            // Assert
            decStorageClientMock.Verify(
                dc => dc.ClaimNameAsync(expectedCanonicalName, publicKey, signature, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task ClaimDecentralizedNameAsyncCanonicalizesNameBeforeSigning()
        {
            // Arrange
            const string name = "  Te St  +  Na Me  ";
            const string expectedCanonicalName = "testname.test";
            const string publicKey = "aewcimjjec6kjyk5nv8vy3tvsdwkpbzbyexhswmg3vyemmmk9mce4";
            const string signature = "testsignature";

            var securityManagerMock = CreateMockSecurityManager();
            securityManagerMock
                .Setup(sm => sm.GetEmailPublicKeyStringAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKey);
            securityManagerMock
                .Setup(sm => sm.SignNameClaimAsync(It.IsAny<string>(), It.IsAny<Account>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(signature);

            var decStorageClientMock = new Mock<IDecStorageClient>();
            decStorageClientMock
                .Setup(dc => dc.ClaimNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKey);

            var account = CreateEppieAccount();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            var result = await core.ClaimDecentralizedNameAsync(name, account).ConfigureAwait(false);

            // Assert
            Assert.That(result, Is.EqualTo(expectedCanonicalName));
            securityManagerMock.Verify(
                sm => sm.SignNameClaimAsync(expectedCanonicalName, account, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task ClaimDecentralizedNameAsyncPassesCancellationToken()
        {
            // Arrange
            const string name = "TestName";
            const string publicKey = "aewcimjjec6kjyk5nv8vy3tvsdwkpbzbyexhswmg3vyemmmk9mce4";
            const string signature = "testsignature";

            var securityManagerMock = CreateMockSecurityManager();
            securityManagerMock
                .Setup(sm => sm.GetEmailPublicKeyStringAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKey);
            securityManagerMock
                .Setup(sm => sm.SignNameClaimAsync(It.IsAny<string>(), It.IsAny<Account>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(signature);

            var decStorageClientMock = new Mock<IDecStorageClient>();
            decStorageClientMock
                .Setup(dc => dc.ClaimNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKey);

            var account = CreateEppieAccount();
            using var cts = new CancellationTokenSource();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            await core.ClaimDecentralizedNameAsync(name, account, cts.Token).ConfigureAwait(false);

            // Assert
            securityManagerMock.Verify(
                sm => sm.GetEmailPublicKeyStringAsync(It.IsAny<EmailAddress>(), cts.Token),
                Times.Once);
            securityManagerMock.Verify(
                sm => sm.SignNameClaimAsync(It.IsAny<string>(), It.IsAny<Account>(), cts.Token),
                Times.Once);
            decStorageClientMock.Verify(
                dc => dc.ClaimNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), cts.Token),
                Times.Once);
        }

        [Test]
        public async Task ClaimDecentralizedNameAsyncReturnsCaseInsensitiveMatch()
        {
            // Arrange
            const string name = "TestName";
            const string expectedCanonicalName = "testname.test";
            const string publicKeyUpper = "AEWCIMJJEC6KJYK5NV8VY3TVSDWKPBZBYEXHSWMG3VYEMMMK9MCE4";
            const string publicKeyLower = "aewcimjjec6kjyk5nv8vy3tvsdwkpbzbyexhswmg3vyemmmk9mce4";
            const string signature = "testsignature";

            var securityManagerMock = CreateMockSecurityManager();
            securityManagerMock
                .Setup(sm => sm.GetEmailPublicKeyStringAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKeyLower);
            securityManagerMock
                .Setup(sm => sm.SignNameClaimAsync(It.IsAny<string>(), It.IsAny<Account>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(signature);

            var decStorageClientMock = new Mock<IDecStorageClient>();
            decStorageClientMock
                .Setup(dc => dc.ClaimNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(publicKeyUpper);

            var account = CreateEppieAccount();
            using var core = CreateTuviMailCore(securityManagerMock, decStorageClientMock);

            // Act
            var result = await core.ClaimDecentralizedNameAsync(name, account).ConfigureAwait(false);

            // Assert
            Assert.That(result, Is.EqualTo(expectedCanonicalName));
        }
    }
}
