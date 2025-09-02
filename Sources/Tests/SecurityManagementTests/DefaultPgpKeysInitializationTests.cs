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

using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Tuvi.Core;
using Tuvi.Core.Backup;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl.SecurityManagement;
using Tuvi.Core.Mail;
using Tuvi.Core.Utils;
using TuviPgpLib;
using TuviPgpLibImpl;

namespace SecurityManagementTests
{
    public class DefaultPgpKeysInitializationTests : TestWithStorageBase
    {
        private ITuviPgpContext PgpContext;

        [SetUp]
        public void SetupTest()
        {
            DeleteStorage();
        }

        private ISecurityManager GetSecurityManager(IDataStorage storage)
        {
            PgpContext = new TuviPgpContext(storage);
            var messageProtectorMock = new Mock<IMessageProtector>();
            var backupProtectorMock = new Mock<IBackupProtector>();
            var publicKeyServiceMock = new Mock<IPublicKeyService>();

            var manager = SecurityManagerCreator.GetSecurityManager(
                storage,
                PgpContext,
                messageProtectorMock.Object,
                backupProtectorMock.Object,
                publicKeyServiceMock.Object);

            manager.SetKeyDerivationDetails(new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"));

            return manager;
        }

        private IDataStorage GetStorage()
        {
            return base.GetDataStorage();
        }

        [Test]
        public async Task Explicit()
        {
            using (var storage = GetStorage())
            {
                var account = Account.Default;
                account.Email = TestData.GetAccount().GetEmailAddress();

                ISecurityManager manager = GetSecurityManager(storage);
                await manager.CreateSeedPhraseAsync().ConfigureAwait(true);
                await manager.StartAsync(Password).ConfigureAwait(true);

                Assert.That(PgpContext.IsSecretKeyExist(account.Email.ToUserIdentity()), Is.False);

                await manager.CreateDefaultPgpKeysAsync(account).ConfigureAwait(true);

                Assert.That(
                    PgpContext.IsSecretKeyExist(account.Email.ToUserIdentity()),
                    Is.True,
                    "Pgp key has to be created for account.");
            }
        }

        [Test]
        public async Task OnMasterKeyInitialization()
        {
            using (var storage = GetStorage())
            {
                var account = Account.Default;
                account.AuthData = new BasicAuthData();

                account.Email = TestData.GetAccount().GetEmailAddress();

                ISecurityManager manager = GetSecurityManager(storage);
                await manager.CreateSeedPhraseAsync().ConfigureAwait(true);
                await manager.StartAsync(Password).ConfigureAwait(true);

                await storage.AddAccountAsync(account).ConfigureAwait(true);
                Assert.That(PgpContext.IsSecretKeyExist(account.Email.ToUserIdentity()), Is.False);

                await manager.CreateDefaultPgpKeysAsync(account).ConfigureAwait(true);
                Assert.That(
                    PgpContext.IsSecretKeyExist(account.Email.ToUserIdentity()),
                    Is.True,
                    "Pgp key has to be created for all existing accounts after master key initialization.");
            }
        }
    }
}
