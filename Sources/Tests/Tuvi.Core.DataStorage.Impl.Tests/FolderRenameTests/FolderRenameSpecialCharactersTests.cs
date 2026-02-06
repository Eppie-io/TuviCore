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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.DataStorage.Tests;
using Tuvi.Core.Entities;

namespace Tuvi.Core.DataStorage.Impl.Tests.FolderRenameTests
{
    public class FolderRenameSpecialCharactersTests : TestWithStorageBase
    {
        [SetUp]
        public async Task SetupAsync()
        {
            DeleteStorage();
            TestData.Setup();
            await CreateDataStorageAsync().ConfigureAwait(true);
        }

        [TearDown]
        public void Teardown()
        {
            DeleteStorage();
        }

        [Test]
        public async Task RenameFolderWithUnderscoreShouldNotAffectOtherFolders()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            var account = await CreateAccountAsync(db, "user@test.com").ConfigureAwait(true);

            // Create folders:
            // 1. "Test_Folder" (target to rename)
            // 2. "TestxFolder" (similar name, should not be affected by _)
            // 3. "Test_Folder/Child" -> message here should move
            // 4. "TestxFolder/Child" -> message here should NOT move

            string targetFolderName = "Test_Folder";
            string similarFolderName = "TestxFolder";
            string newFolderName = "Renamed_Folder";

            await CreateFolderStructureAsync(db, account, targetFolderName, similarFolderName).ConfigureAwait(true);

            // Act: Rename "Test_Folder" to "Renamed_Folder"
            await db.UpdateFolderPathAsync(account.Email, targetFolderName, newFolderName, CancellationToken.None).ConfigureAwait(true);

            // Assert
            // We need to check paths directly or query DB. 
            // GetMessageListAsync returns messages in strict folder.

            // 1. Verify target folder message is found by new name
            var messagesInNewLocation = await db.GetMessagesCountAsync(account.Email, newFolderName, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messagesInNewLocation, Is.EqualTo(1), "Message in root of renamed folder should exist.");

            // 2. Verify similar folder message is STILL in old location
            var messagesInSimilarLocation = await db.GetMessagesCountAsync(account.Email, similarFolderName, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messagesInSimilarLocation, Is.EqualTo(1), "Message in similar folder (matching wildcard _) should NOT be moved.");

            // 3. Verify subfolder message moved: "Renamed_Folder/Child"
            // Note: UpdateFolderPathAsync logic handles subfolders by string replacement.
            // We assume standard delimiter is used in path construction in test helper or manually.
            // Let's verify by checking if message exists at 'Renamed_Folder/Child' path.
            // But we need to know what delimiter CreatePath uses. Usually it is '\' in DataStorage.CreatePath.
            // However, the test helper CreateFolderStructureAsync (below) will use standard folder creation.

            // We can just rely on GetMessagesCountAsync with subfolder name "Renamed_Folder/Child"
            // But wait, UpdateFolderPathAsync implementation we saw updates the 'Path' column in Message table AND 'FullName' in Folder table.

            // Check matching subfolder of target
            var messagesInNewSubLocation = await db.GetMessagesCountAsync(account.Email, newFolderName + "/Child", CancellationToken.None).ConfigureAwait(true);
            // If the folder renaming logic supports '/' delimiter for subfolders in path updates (which it does in the code: oldPathSlashPrefix), this should work.
            if (messagesInNewSubLocation == 0)
            {
                // Try with dot separator checking
                messagesInNewSubLocation = await db.GetMessagesCountAsync(account.Email, newFolderName + ".Child", CancellationToken.None).ConfigureAwait(true);
            }
            Assert.That(messagesInNewSubLocation, Is.EqualTo(1), "Message in subfolder of renamed folder should be moved.");

            // 4. Verify subfolder of similar folder did NOT move
            var messagesInSimilarSubLocation = await db.GetMessagesCountAsync(account.Email, similarFolderName + "/Child", CancellationToken.None).ConfigureAwait(true);
            if (messagesInSimilarSubLocation == 0)
            {
                messagesInSimilarSubLocation = await db.GetMessagesCountAsync(account.Email, similarFolderName + ".Child", CancellationToken.None).ConfigureAwait(true);
            }
            Assert.That(messagesInSimilarSubLocation, Is.EqualTo(1), "Message in subfolder of similar folder should NOT be moved.");
        }

        [Test]
        public async Task RenameFolderWithPercentShouldNotAffectOtherFolders()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            var account = await CreateAccountAsync(db, "percent@test.com").ConfigureAwait(true);

            // Create folders:
            // 1. "Test%Folder" (target)
            // 2. "TestStringFolder" (matches % wildcard)

            string targetFolderName = "Test%Folder";
            string similarFolderName = "TestStringFolder";
            string newFolderName = "CleanFolder";

            await CreateFolderStructureAsync(db, account, targetFolderName, similarFolderName).ConfigureAwait(true);

            // Act
            await db.UpdateFolderPathAsync(account.Email, targetFolderName, newFolderName, CancellationToken.None).ConfigureAwait(true);

            // Assert
            var messagesInNewLocation = await db.GetMessagesCountAsync(account.Email, newFolderName, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messagesInNewLocation, Is.EqualTo(1), "Msg in renamed folder should be found.");

            var messagesInSimilarLocation = await db.GetMessagesCountAsync(account.Email, similarFolderName, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messagesInSimilarLocation, Is.EqualTo(1), "Msg in similar folder should NOT be moved.");
        }

        private static async Task<Account> CreateAccountAsync(IDataStorage db, string emailAddress)
        {
            var account = new Account
            {
                Email = new EmailAddress(emailAddress, "User"),
                AuthData = new BasicAuthData(),
                FoldersStructure = new List<Folder>()
            };
            await db.AddAccountAsync(account, CancellationToken.None).ConfigureAwait(true);
            return await db.GetAccountAsync(account.Email, CancellationToken.None).ConfigureAwait(true);
        }

        private static async Task CreateFolderStructureAsync(IDataStorage db, Account account, string folder1, string folder2)
        {
            // We manually add folders and messages because we want specific hierarchy

            // Create folder objects
            var f1 = new Folder(folder1, FolderAttributes.None);
            var f1Child = new Folder(folder1 + "/Child", FolderAttributes.None);
            var f2 = new Folder(folder2, FolderAttributes.None);
            var f2Child = new Folder(folder2 + "/Child", FolderAttributes.None);

            // Update auth/structure FIRST so folders exist in DB
            account.FoldersStructure.AddRange(new[] { f1, f1Child, f2, f2Child });
            await db.UpdateAccountFolderStructureAsync(account, CancellationToken.None).ConfigureAwait(true);

            // Now add messages
            await AddFolderWithMessageAsync(db, account, f1).ConfigureAwait(true);
            await AddFolderWithMessageAsync(db, account, f1Child).ConfigureAwait(true);
            await AddFolderWithMessageAsync(db, account, f2).ConfigureAwait(true);
            await AddFolderWithMessageAsync(db, account, f2Child).ConfigureAwait(true);
        }

        private static async Task AddFolderWithMessageAsync(IDataStorage db, Account account, Folder folder)
        {
            // Add a dummy message
            var msg = new Message()
            {
                Folder = folder,
                Date = System.DateTime.Now
            };
            msg.From.Add(new EmailAddress("sender@test.com"));

            // AddMessageAsync puts it in the DB and constructs Path
            await db.AddMessageAsync(account.Email, msg, true, CancellationToken.None).ConfigureAwait(true);
        }
    }
}
