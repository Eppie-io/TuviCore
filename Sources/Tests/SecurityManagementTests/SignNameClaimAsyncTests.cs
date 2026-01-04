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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Tuvi.Core;
using Tuvi.Core.Backup;
using Tuvi.Core.DataStorage;
using Tuvi.Core.DataStorage.Impl;
using Tuvi.Core.Dec.Impl;
using Tuvi.Core.Dec.Names;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl.SecurityManagement;
using Tuvi.Core.Mail;
using Tuvi.Core.Utils;
using TuviPgpLibImpl;

namespace SecurityManagementTests
{
    [TestFixture]
    [NonParallelizable]
    public class SignNameClaimAsyncTests
    {
        private const string Password = "123456";
        private static readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();
        private string _currentDbPath;

        [OneTimeTearDown]
        public void CleanupHttpClient()
        {
            _httpClient.Dispose();
        }

        [SetUp]
        public void SetupTest()
        {
            _currentDbPath = Path.Combine(Environment.CurrentDirectory, $"SignNameClaimTest_{Guid.NewGuid()}.db");
        }

        [TearDown]
        public void TearDownTest()
        {
            if (!string.IsNullOrEmpty(_currentDbPath) && File.Exists(_currentDbPath))
            {
                try
                {
                    File.Delete(_currentDbPath);
                }
                catch (IOException)
                {
                    // Ignore cleanup errors - file may still be in use
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore cleanup errors - file access denied
                }
            }
        }

        private IDataStorage GetStorage()
        {
            return DataStorageProvider.GetDataStorage(_currentDbPath);
        }

        private static ISecurityManager GetSecurityManager(IDataStorage storage)
        {
            var pgpContext = TemporalKeyStorage.GetTemporalContextAsync(storage).Result;
            return GetSecurityManager(storage, pgpContext);
        }

        private static ISecurityManager GetSecurityManager(IDataStorage storage, TuviPgpContext pgpContext)
        {
            var messageProtectorMock = new Mock<IMessageProtector>();
            var backupProtectorMock = new Mock<IBackupProtector>();
            var publicKeyService = PublicKeyService.CreateDefault(PublicKeyService.NoOpNameResolver, string.Empty, _httpClient);

            var manager = SecurityManagerCreator.GetSecurityManager(
                    storage,
                    pgpContext,
                    messageProtectorMock.Object,
                    backupProtectorMock.Object,
                    publicKeyService);

            manager.SetKeyDerivationDetails(new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"));

            return manager;
        }

        private async Task<(ISecurityManager Manager, IDataStorage Storage)> CreateInitializedSecurityManagerAsync()
        {
            var storage = GetStorage();
            ISecurityManager manager = GetSecurityManager(storage);

            var testSeed = TestData.GetTestSeed();
            await manager.RestoreSeedPhraseAsync(testSeed).ConfigureAwait(true);
            await manager.StartAsync(Password).ConfigureAwait(true);

            return (manager, storage);
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

        [Test]
        public void SignNameClaimAsyncThrowsOnNullName()
        {
            // Arrange
            using var storage = GetStorage();
            var manager = GetSecurityManager(storage);
            var account = CreateEppieAccount();

            // Act
            AsyncTestDelegate act = async () => await manager.SignNameClaimAsync(null, account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<ArgumentException>(act);
        }

        [Test]
        public void SignNameClaimAsyncThrowsOnEmptyName()
        {
            // Arrange
            using var storage = GetStorage();
            var manager = GetSecurityManager(storage);
            var account = CreateEppieAccount();

            // Act
            AsyncTestDelegate act = async () => await manager.SignNameClaimAsync(string.Empty, account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<ArgumentException>(act);
        }

        [Test]
        public void SignNameClaimAsyncThrowsOnWhitespaceName()
        {
            // Arrange
            using var storage = GetStorage();
            var manager = GetSecurityManager(storage);
            var account = CreateEppieAccount();

            // Act
            AsyncTestDelegate act = async () => await manager.SignNameClaimAsync("   ", account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<ArgumentException>(act);
        }

        [Test]
        public void SignNameClaimAsyncThrowsOnNullAccount()
        {
            // Arrange
            using var storage = GetStorage();
            var manager = GetSecurityManager(storage);

            // Act
            AsyncTestDelegate act = async () => await manager.SignNameClaimAsync("testname", null).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<ArgumentNullException>(act);
        }

        [Test]
        public void SignNameClaimAsyncThrowsOnBitcoinAccount()
        {
            // Arrange
            using var storage = GetStorage();
            var manager = GetSecurityManager(storage);
            var account = CreateBitcoinAccount();

            // Act
            AsyncTestDelegate act = async () => await manager.SignNameClaimAsync("testname", account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<NotSupportedException>(act);
        }

        [Test]
        public void SignNameClaimAsyncThrowsOnEthereumAccount()
        {
            // Arrange
            using var storage = GetStorage();
            var manager = GetSecurityManager(storage);
            var account = CreateEthereumAccount();

            // Act
            AsyncTestDelegate act = async () => await manager.SignNameClaimAsync("testname", account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<NotSupportedException>(act);
        }

        [Test]
        public void SignNameClaimAsyncThrowsOnTraditionalAccount()
        {
            // Arrange
            using var storage = GetStorage();
            var manager = GetSecurityManager(storage);
            var account = CreateTraditionalAccount();

            // Act
            AsyncTestDelegate act = async () => await manager.SignNameClaimAsync("testname", account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<NotSupportedException>(act);
        }

        [Test]
        public void SignNameClaimAsyncThrowsOnUninitializedDecentralizedAccountIndex()
        {
            // Arrange
            using var storage = GetStorage();
            var manager = GetSecurityManager(storage);
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, "aewcimjjec6kjyk5nv8vy3tvsdwkpbzbyexhswmg3vyemmmk9mce4");
            var account = new Account
            {
                Email = email,
                DecentralizedAccountIndex = -1,
                Type = MailBoxType.Dec
            };

            // Act
            AsyncTestDelegate act = async () => await manager.SignNameClaimAsync("testname", account).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<InvalidOperationException>(act);
        }

        [Test]
        public async Task SignNameClaimAsyncProducesValidSignature()
        {
            // Arrange
            var (manager, storage) = await CreateInitializedSecurityManagerAsync().ConfigureAwait(true);
            using (storage)
            {
                var account = CreateEppieAccount();
                const string name = "testname";

                // Act
                var signature = await manager.SignNameClaimAsync(name, account).ConfigureAwait(true);
                var publicKey = await manager.GetEmailPublicKeyStringAsync(account.Email).ConfigureAwait(true);

                // Assert
                Assert.That(signature, Is.Not.Null.And.Not.Empty);
                Assert.That(publicKey, Is.Not.Null.And.Not.Empty);
                var verifies = NameClaimVerifier.VerifyClaimV1Signature(name, publicKey, signature);
                Assert.That(verifies, Is.True);
            }
        }

        [Test]
        public async Task SignNameClaimAsyncIsDeterministic()
        {
            // Arrange
            var (manager, storage) = await CreateInitializedSecurityManagerAsync().ConfigureAwait(true);
            using (storage)
            {
                var account = CreateEppieAccount();
                const string name = "deterministictest";

                // Act
                var signature1 = await manager.SignNameClaimAsync(name, account).ConfigureAwait(true);
                var signature2 = await manager.SignNameClaimAsync(name, account).ConfigureAwait(true);

                // Assert
                Assert.That(signature1, Is.EqualTo(signature2));
            }
        }

        [Test]
        public async Task SignNameClaimAsyncWithCanonicalizedNameProducesSameSignature()
        {
            // Arrange
            var (manager, storage) = await CreateInitializedSecurityManagerAsync().ConfigureAwait(true);
            using (storage)
            {
                var account = CreateEppieAccount();

                // Act
                var signature1 = await manager.SignNameClaimAsync("Alice", account).ConfigureAwait(true);
                var signature2 = await manager.SignNameClaimAsync("  a l+i ce ", account).ConfigureAwait(true);

                // Assert
                Assert.That(signature1, Is.EqualTo(signature2));
            }
        }

        [Test]
        public async Task SignNameClaimAsyncDifferentNamesProduceDifferentSignatures()
        {
            // Arrange
            var (manager, storage) = await CreateInitializedSecurityManagerAsync().ConfigureAwait(true);
            using (storage)
            {
                var account = CreateEppieAccount();

                // Act
                var signature1 = await manager.SignNameClaimAsync("alice", account).ConfigureAwait(true);
                var signature2 = await manager.SignNameClaimAsync("bob", account).ConfigureAwait(true);

                // Assert
                Assert.That(signature1, Is.Not.EqualTo(signature2));
            }
        }

        [Test]
        public void SignNameClaimAsyncRespectsCancellationToken()
        {
            // Arrange
            using var storage = GetStorage();
            var manager = GetSecurityManager(storage);
            var account = CreateEppieAccount();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            AsyncTestDelegate act = async () => await manager.SignNameClaimAsync("testname", account, cts.Token).ConfigureAwait(false);

            // Assert
            Assert.ThrowsAsync<OperationCanceledException>(act);
        }

        [Test]
        public async Task SignNameClaimAsyncDerivedPublicKeyMatchesGetEmailPublicKeyStringAsync()
        {
            // Arrange
            var (manager, storage) = await CreateInitializedSecurityManagerAsync().ConfigureAwait(true);
            using (storage)
            {
                var account = CreateEppieAccount();
                const string name = "matchtest";

                // Act
                var signature = await manager.SignNameClaimAsync(name, account).ConfigureAwait(true);
                var publicKeyFromEmail = await manager.GetEmailPublicKeyStringAsync(account.Email).ConfigureAwait(true);

                // Assert
                var verifies = NameClaimVerifier.VerifyClaimV1Signature(name, publicKeyFromEmail, signature);
                Assert.That(verifies, Is.True);
            }
        }

        [Test]
        public async Task SignNameClaimAsyncWithLongNameProducesValidSignature()
        {
            // Arrange
            var (manager, storage) = await CreateInitializedSecurityManagerAsync().ConfigureAwait(true);
            using (storage)
            {
                var account = CreateEppieAccount();
                var longName = new string('a', 100);

                // Act
                var signature = await manager.SignNameClaimAsync(longName, account).ConfigureAwait(true);
                var publicKey = await manager.GetEmailPublicKeyStringAsync(account.Email).ConfigureAwait(true);

                // Assert
                Assert.That(signature, Is.Not.Null.And.Not.Empty);
                var verifies = NameClaimVerifier.VerifyClaimV1Signature(longName, publicKey, signature);
                Assert.That(verifies, Is.True);
            }
        }

        [Test]
        public async Task SignNameClaimAsyncWithMinimalNameProducesValidSignature()
        {
            // Arrange
            var (manager, storage) = await CreateInitializedSecurityManagerAsync().ConfigureAwait(true);
            using (storage)
            {
                var account = CreateEppieAccount();
                const string minimalName = "a";

                // Act
                var signature = await manager.SignNameClaimAsync(minimalName, account).ConfigureAwait(true);
                var publicKey = await manager.GetEmailPublicKeyStringAsync(account.Email).ConfigureAwait(true);

                // Assert
                Assert.That(signature, Is.Not.Null.And.Not.Empty);
                var verifies = NameClaimVerifier.VerifyClaimV1Signature(minimalName, publicKey, signature);
                Assert.That(verifies, Is.True);
            }
        }
    }
}
