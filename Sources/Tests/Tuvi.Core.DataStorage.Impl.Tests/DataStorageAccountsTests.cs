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
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Entities;

namespace Tuvi.Core.DataStorage.Tests
{
    public class DataStorageAccountsTests : TestWithStorageBase
    {
        [SetUp]
        public async Task SetupAsync()
        {
            DeleteStorage();
            TestData.Setup();

            await CreateDataStorageAsync().ConfigureAwait(true);
        }

        [Test]
        public async Task AddAccountInfoToDataStorage()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);
                if (await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true))
                {
                    await db.DeleteAccountByEmailAsync(TestData.Account.Email).ConfigureAwait(true);
                }
                Assert.That(await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true), Is.False);

                await db.AddAccountAsync(TestData.Account).ConfigureAwait(true);
                var isAdded = await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true);
                Assert.That(isAdded, Is.True);

                var account = await db.GetAccountAsync(TestData.Account.Email).ConfigureAwait(true);
                Assert.That(account.AuthData.Type, Is.EqualTo(TestData.Account.AuthData.Type));

                Assert.That(
                    account.AuthData is BasicAuthData basicData &&
                    TestData.Account.AuthData is BasicAuthData data &&
                    basicData.Password?.Equals(data.Password, StringComparison.Ordinal) == true,
                    Is.True
                    );
            }
        }

        [Test]
        public async Task ExistsAccountWithEmail()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);
                if ((await db.GetAccountsAsync().ConfigureAwait(true)).Count == 0)
                {
                    await db.AddAccountAsync(TestData.Account).ConfigureAwait(true);
                }

                var accounts = await db.GetAccountsAsync().ConfigureAwait(true);
                bool result = await db.ExistsAccountWithEmailAddressAsync(accounts[0].Email).ConfigureAwait(true);

                Assert.That(result, Is.True);
            }
        }

        [Test]
        public async Task AddAccountWithSameEmailAddress()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);
                if (!await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true))
                {
                    await db.AddAccountAsync(TestData.Account).ConfigureAwait(true);
                }

                Assert.ThrowsAsync<AccountAlreadyExistInDatabaseException>(() => db.AddAccountAsync(TestData.Account));
            }
        }

        [Test]
        public async Task DeleteAccountFromDataStorage()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                if ((await db.GetAccountsAsync().ConfigureAwait(true)).Count == 0)
                {
                    await db.AddAccountAsync(TestData.Account).ConfigureAwait(true);
                }

                var accounts = await db.GetAccountsAsync().ConfigureAwait(true);
                Assert.That(accounts.Count, Is.GreaterThanOrEqualTo(0));

                var accountToDelete = accounts[0];
                await db.DeleteAccountAsync(accountToDelete).ConfigureAwait(true);

                accounts = await db.GetAccountsAsync().ConfigureAwait(true);
                Assert.That(accounts.Exists(account => account.Email == accountToDelete.Email), Is.False);
            }
        }

        [Test]
        public async Task DeleteAccountByEmail()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);
                if (!await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true))
                {
                    await db.AddAccountAsync(TestData.Account).ConfigureAwait(true);
                }

                await db.DeleteAccountByEmailAsync(TestData.Account.Email).ConfigureAwait(true);
                Assert.That(await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true), Is.False);
            }
        }

        [Test]
        public async Task UpdateAccountInfoInDataStorage()
        {
            string newName = "Test New Name";

            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);
                if (!await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true))
                {
                    await db.AddAccountAsync(TestData.Account).ConfigureAwait(true);
                }

                var account = TestData.Account;
                account.Email = new EmailAddress(account.Email.Address, newName);
                await db.UpdateAccountAsync(account).ConfigureAwait(true);

                var accounts = await db.GetAccountsAsync().ConfigureAwait(true);
                var updatedAccount = accounts.First(x => x.Email.Address == account.Email.Address);

                Assert.That(newName, Is.EqualTo(updatedAccount.Email.Name), "Account was not updated properly.");

                await db.DeleteAccountAsync(updatedAccount).ConfigureAwait(true);
            }
        }

        [Test]
        public async Task UpdateAccountAuthData()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);
                if (!await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true))
                {
                    await db.AddAccountAsync(TestData.Account).ConfigureAwait(true);
                }

                var account = TestData.Account;
                var newAuth = new BasicAuthData() { Password = "1111" };
                account.AuthData = newAuth;
                await db.UpdateAccountAuthAsync(account).ConfigureAwait(true);

                var accounts = await db.GetAccountsAsync().ConfigureAwait(true);
                var updatedAccount = accounts.First(x => x.Email.Address == account.Email.Address);

                Assert.That(newAuth, Is.EqualTo(updatedAccount.AuthData), "Account AuthData wasn't updated properly.");

                await db.DeleteAccountAsync(updatedAccount).ConfigureAwait(true);
            }
        }

        [Test]
        public async Task AddAccountWithInvalidLocalCountFolder()
        {
            using var db = GetDataStorage();
            await db.OpenAsync(Password).ConfigureAwait(true);
            var account = TestData.CreateAccountWithFolder();
            account.FoldersStructure[0].LocalCount = 13; // this field shouldn't be serialized or provided from other sources
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(true);
            var accounts = await db.GetAccountsAsync().ConfigureAwait(true);
            Assert.That(accounts[0].FoldersStructure[0].LocalCount == 0);
        }

        [Test]
        public async Task UpdateAccountFolders()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);
                if (!await db.ExistsAccountWithEmailAddressAsync(TestData.AccountWithFolder.Email).ConfigureAwait(true))
                {
                    await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(true);
                }

                await db.AddMessageAsync(TestData.AccountWithFolder.Email, TestData.Message).ConfigureAwait(true);

                var account = TestData.AccountWithFolder;
                var newFolders = new List<Folder>
                {
                    new Folder(TestData.Folder, FolderAttributes.Inbox){ UnreadCount = 102, TotalCount=123},
                    new Folder("Trash", FolderAttributes.Trash),
                };

                account.FoldersStructure = newFolders;
                account.DefaultInboxFolder = account.FoldersStructure[0];

                await db.UpdateAccountFolderStructureAsync(account).ConfigureAwait(true);

                var accounts = await db.GetAccountsAsync().ConfigureAwait(true);
                var updatedAccount = accounts.First(x => x.Email.Address == account.Email.Address);

                Assert.That(newFolders, Is.EqualTo(updatedAccount.FoldersStructure), "Account folders weren't updated properly.");
                Assert.That(newFolders[0], Is.EqualTo(updatedAccount.DefaultInboxFolder), "Account default folder wasn't updated properly.");
                Assert.That(updatedAccount.FoldersStructure[0].UnreadCount, Is.EqualTo(102));
                Assert.That(updatedAccount.FoldersStructure[0].TotalCount, Is.EqualTo(123));

                var message = await db.GetMessageAsync(TestData.AccountWithFolder.Email, TestData.Message.Folder.FullName, TestData.Message.Id).ConfigureAwait(true);

                Assert.That(message.Folder, Is.EqualTo(TestData.Message.Folder));

                // check message in the removed folder
                {
                    newFolders.RemoveAt(0);
                    account.FoldersStructure = newFolders;
                    account.DefaultInboxFolder = account.FoldersStructure[0];
                    await db.UpdateAccountFolderStructureAsync(account).ConfigureAwait(true);
                    bool exists = await db.IsMessageExistAsync(TestData.AccountWithFolder.Email, TestData.Message.Folder.FullName, TestData.Message.Id).ConfigureAwait(true);
                    Assert.That(exists, Is.False);
                }

                await db.DeleteAccountAsync(updatedAccount).ConfigureAwait(true);
            }
        }

        [Test]
        public async Task UpdateIfAccountNotExist()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);
                if (await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true))
                {
                    await db.DeleteAccountByEmailAsync(TestData.Account.Email).ConfigureAwait(true);
                }
                Assert.That(await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true), Is.False);

                await db.UpdateAccountAsync(TestData.Account).ConfigureAwait(true);
                Assert.That(await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true), Is.False);
            }
        }

        [Test]
        public async Task AddSeveralAccounts()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);
                if (await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true))
                {
                    await db.DeleteAccountByEmailAsync(TestData.Account.Email).ConfigureAwait(true);
                }

                await db.AddAccountAsync(TestData.Account).ConfigureAwait(true);
                Assert.That(await db.ExistsAccountWithEmailAddressAsync(TestData.Account.Email).ConfigureAwait(true), Is.True);

                await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(true);
                Assert.That(await db.ExistsAccountWithEmailAddressAsync(TestData.AccountWithFolder.Email).ConfigureAwait(true), Is.True);
            }
        }

        [Test]
        public async Task AccountExternalContentPolicyPersistence()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var testAccount = TestData.CreateAccount();
                testAccount.Email = new EmailAddress("externalcontent@test.test");
                testAccount.ExternalContentPolicy = ExternalContentPolicy.Block;

                await db.AddAccountAsync(testAccount).ConfigureAwait(true);

                var retrievedAccount = await db.GetAccountAsync(testAccount.Email).ConfigureAwait(true);

                Assert.That(retrievedAccount.ExternalContentPolicy, Is.EqualTo(ExternalContentPolicy.Block));

                await db.DeleteAccountAsync(testAccount).ConfigureAwait(true);
            }
        }

        [Test]
        public async Task UpdateAccountExternalContentPolicy()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var testAccount = TestData.CreateAccount();
                testAccount.Email = new EmailAddress("updatepolicy@test.test");
                testAccount.ExternalContentPolicy = ExternalContentPolicy.AlwaysAllow;

                await db.AddAccountAsync(testAccount).ConfigureAwait(true);

                testAccount.ExternalContentPolicy = ExternalContentPolicy.AskEachTime;
                await db.UpdateAccountAsync(testAccount).ConfigureAwait(true);

                var updatedAccount = await db.GetAccountAsync(testAccount.Email).ConfigureAwait(true);

                Assert.That(updatedAccount.ExternalContentPolicy, Is.EqualTo(ExternalContentPolicy.AskEachTime));

                await db.DeleteAccountAsync(testAccount).ConfigureAwait(true);
            }
        }

        [Test]
        public async Task DefaultExternalContentPolicyValue()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var testAccount = TestData.CreateAccount();
                testAccount.Email = new EmailAddress("defaultpolicy@test.test");

                await db.AddAccountAsync(testAccount).ConfigureAwait(true);

                var retrievedAccount = await db.GetAccountAsync(testAccount.Email).ConfigureAwait(true);

                Assert.That(retrievedAccount.ExternalContentPolicy, Is.EqualTo(ExternalContentPolicy.AlwaysAllow));

                await db.DeleteAccountAsync(testAccount).ConfigureAwait(true);
            }
        }

        [Test]
        public async Task AccountExternalContentPolicyAllValues()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var policies = new[]
                {
                    ExternalContentPolicy.AlwaysAllow,
                    ExternalContentPolicy.AskEachTime,
                    ExternalContentPolicy.Block
                };

                foreach (var policy in policies)
                {
                    var testAccount = TestData.CreateAccount();
                    testAccount.Email = new EmailAddress($"policy{policy}@test.test");
                    testAccount.ExternalContentPolicy = policy;

                    await db.AddAccountAsync(testAccount).ConfigureAwait(true);

                    var retrievedAccount = await db.GetAccountAsync(testAccount.Email).ConfigureAwait(true);

                    Assert.That(retrievedAccount.ExternalContentPolicy, Is.EqualTo(policy),
                        $"ExternalContentPolicy {policy} was not persisted correctly.");

                    await db.DeleteAccountAsync(testAccount).ConfigureAwait(true);
                }
            }
        }
    }
}
