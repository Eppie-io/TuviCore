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
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Tuvi.Core;
using Tuvi.Core.Backup;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Dec.Impl;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl.SecurityManagement;
using Tuvi.Core.Mail;
using Tuvi.Core.Utils;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Tuvi.Core.Mail.Tests")]
namespace SecurityManagementTests
{
    public class SecurityManagerTests : TestWithStorageBase
    {
        [SetUp]
        public void SetupTest()
        {
            DeleteStorage();
        }

        private static ISecurityManager GetSecurityManager(IDataStorage storage)
        {
            var pgpContent = TemporalKeyStorage.GetTemporalContextAsync(storage).Result;
            var messageProtectorMock = new Mock<IMessageProtector>();
            var backupProtectorMock = new Mock<IBackupProtector>();
            var publicKeyService = PublicKeyService.CreateDefault(PublicKeyService.NoOpNameResolver);

            var manager = SecurityManagerCreator.GetSecurityManager(
                    storage,
                    pgpContent,
                    messageProtectorMock.Object,
                    backupProtectorMock.Object,
                    publicKeyService);

            manager.SetKeyDerivationDetails(new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"));

            return manager;
        }

        private IDataStorage GetStorage()
        {
            return GetDataStorage();
        }

        [Test]
        public void KeyStorageNotExist()
        {
            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);

                Assert.That(manager.IsNeverStartedAsync().Result, Is.True);
                Assert.That(manager.GetSeedValidator(), Is.Not.Null);
                Assert.That(manager.GetSeedQuiz(), Is.Null);
            }
        }

        [Test]
        public void CreateMasterKey()
        {
            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);

                string[] seed = manager.CreateSeedPhraseAsync().Result;
                Assert.That(seed, Is.Not.Null);
                Assert.That(seed.Length, Is.GreaterThanOrEqualTo(manager.GetRequiredSeedPhraseLength()));
                foreach (var word in seed)
                {
                    Assert.That(word, Is.Not.Empty);
                }
                Assert.That(manager.IsNeverStartedAsync().Result, Is.True);
                Assert.That(manager.GetSeedValidator(), Is.Not.Null);
                Assert.That(manager.GetSeedQuiz(), Is.Not.Null);
            }
        }

        [Test]
        public void MasterKeyInitializedOnStart()
        {
            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);

                manager.CreateSeedPhraseAsync().Wait();
                manager.StartAsync(Password).Wait();
                Assert.That(manager.IsNeverStartedAsync().Result, Is.False);
                Assert.That(manager.IsSeedPhraseInitializedAsync().Result, Is.True);
            }
        }

        [Test]
        public void MasterKeyInitializedAfterStart()
        {
            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);

                Assert.DoesNotThrowAsync(() => manager.CreateSeedPhraseAsync());
                manager.StartAsync(Password).Wait();

                Assert.That(manager.IsSeedPhraseInitializedAsync().Result, Is.True);
            }
        }

        [Test]
        public void RestoreMasterKey()
        {
            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);

                var testSeed = TestData.GetTestSeed();
                manager.RestoreSeedPhraseAsync(testSeed).Wait();
                manager.StartAsync(Password).Wait();
                Assert.That(manager.IsNeverStartedAsync().Result, Is.False);
                Assert.That(manager.IsSeedPhraseInitializedAsync().Result, Is.True);
                Assert.That(manager.GetSeedValidator(), Is.Not.Null);
            }
        }

        [Test]
        public void ResetSecurityManager()
        {
            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);

                manager.CreateSeedPhraseAsync().Wait();
                manager.StartAsync(Password).Wait();
                Assert.That(manager.IsSeedPhraseInitializedAsync().Result, Is.True);

                manager.ResetAsync().Wait();
            }
        }

        [Test]
        public async Task ChangePassword()
        {
            using (var storage = GetStorage())
            {
                await storage.CreateAsync(Password).ConfigureAwait(true);
                ISecurityManager manager = GetSecurityManager(storage);
                await manager.CreateSeedPhraseAsync().ConfigureAwait(true);
                await manager.InitializeMasterKeyAsync().ConfigureAwait(true);
                await manager.ChangePasswordAsync(Password, NewPassword, default).ConfigureAwait(true);
            }

            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);
                Assert.ThrowsAsync<DataBasePasswordException>(() => manager.StartAsync(Password));
                Assert.DoesNotThrowAsync(() => manager.StartAsync(NewPassword));
            }
        }

        [Test]
        public async Task EmailPublicKeyStringGenerated()
        {
            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);

                var testSeed = TestData.GetTestSeed();
                await manager.RestoreSeedPhraseAsync(testSeed).ConfigureAwait(true);
                await manager.StartAsync(Password).ConfigureAwait(true);

                var email = new EmailAddress("user@example.com");
                var keyString = await manager.GetEmailPublicKeyStringAsync(email).ConfigureAwait(true);

                Assert.That(keyString, Is.Not.Null);
                Assert.That(keyString, Is.Not.Empty);
                Assert.That(keyString, Is.EqualTo("agd5r3j32csbqxy5j9tqs5xwqvh48rfht9ursj3vbamnjycbbseup"));
                Assert.DoesNotThrow(() => PublicKeyService.CreateDefault(PublicKeyService.NoOpNameResolver).Decode(keyString));
            }
        }

        [Test]
        public async Task NextDecentralizedAccountPublicKeyGenerated()
        {
            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);

                var testSeed = TestData.GetTestSeed();
                await manager.RestoreSeedPhraseAsync(testSeed).ConfigureAwait(true);
                await manager.StartAsync(Password).ConfigureAwait(true);

                var (keyString, accountIndex) = manager.GetNextDecAccountPublicKeyAsync(NetworkType.Eppie, default).Result;

                Assert.That(accountIndex, Is.EqualTo(0));
                Assert.That(keyString, Is.EqualTo("aewcimjjec6kjyk5nv8vy3tvsdwkpbzbyexhswmg3vyemmmk9mce4"));
                Assert.DoesNotThrow(() => PublicKeyService.CreateDefault(PublicKeyService.NoOpNameResolver).Decode(keyString));
            }
        }

        [Test]
        public async Task RemovePgpKeysForAccountRemovesExpectedIdentity()
        {
            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);

                await manager.CreateSeedPhraseAsync().ConfigureAwait(true);
                await manager.StartAsync(Password).ConfigureAwait(true);
                Assert.That(manager.IsSeedPhraseInitializedAsync().Result, Is.True);

                var account = new Account { Email = new EmailAddress("remove@example.com") };

                await manager.CreateDefaultPgpKeysAsync(account).ConfigureAwait(true);

                var pgpKeys = manager.GetPublicPgpKeysInfo();
                Assert.That(
                    pgpKeys.Any(k => k.UserIdentity.Contains("remove@example.com", StringComparison.Ordinal)),
                    Is.True,
                    "Pgp key was not created"
                );

                manager.RemovePgpKeys(account);

                pgpKeys = manager.GetPublicPgpKeysInfo();
                Assert.That(
                    pgpKeys.All(k => !k.UserIdentity.Contains("remove@example.com", StringComparison.Ordinal)),
                    Is.True,
                    "Pgp key was not removed"
                );
            }
        }
    }
}
