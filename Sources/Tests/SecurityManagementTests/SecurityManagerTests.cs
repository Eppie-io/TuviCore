using Moq;
using NUnit.Framework;
using System.Threading.Tasks;
using Tuvi.Core;
using Tuvi.Core.Backup;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl.SecurityManagement;
using Tuvi.Core.Mail;
using TuviPgpLib;

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
            var pgpContentMock = new Mock<ITuviPgpContext>();
            var messageProtectorMock = new Mock<IMessageProtector>();
            var backupProtectorMock = new Mock<IBackupProtector>();

            var manager = SecurityManagerCreator.GetSecurityManager(
                storage,
                pgpContentMock.Object,
                messageProtectorMock.Object,
                backupProtectorMock.Object);
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
        public void KeyNotInitialized()
        {
            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);

                Assert.DoesNotThrowAsync(() => manager.StartAsync(Password));
                Assert.That(manager.IsNeverStartedAsync().Result, Is.False);
                Assert.That(manager.IsSeedPhraseInitializedAsync().Result, Is.False);
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

                manager.StartAsync(Password).Wait();

                Assert.DoesNotThrowAsync(() => manager.CreateSeedPhraseAsync());
                Assert.DoesNotThrowAsync(() => manager.InitializeSeedPhraseAsync());
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

            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);

                manager.StartAsync(Password).Wait();
                Assert.That(manager.IsSeedPhraseInitializedAsync().Result, Is.False);
            }
        }

        [Test]
        public async Task ChangePassword()
        {
            using (var storage = GetStorage())
            {
                await storage.CreateAsync(Password).ConfigureAwait(true);
                ISecurityManager manager = GetSecurityManager(storage);
                await manager.ChangePasswordAsync(Password, NewPassword, default).ConfigureAwait(true);
            }

            using (var storage = GetStorage())
            {
                ISecurityManager manager = GetSecurityManager(storage);
                Assert.ThrowsAsync<DataBasePasswordException>(() => manager.StartAsync(Password));
                Assert.DoesNotThrowAsync(() => manager.StartAsync(NewPassword));
            }
        }
    }
}
