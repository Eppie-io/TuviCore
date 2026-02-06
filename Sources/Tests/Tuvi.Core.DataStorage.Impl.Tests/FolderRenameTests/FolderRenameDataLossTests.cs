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
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.DataStorage.Tests;
using Tuvi.Core.Entities;

namespace Tuvi.Core.DataStorage.Impl.Tests.FolderRenameTests
{
    public class FolderRenameDataLossTests : TestWithStorageBase
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

        [TestCase("Old", "New")]
        [TestCase("Old/Sub", "New/Sub")]
        [TestCase("Old/Sub", "New")]
        [TestCase("Old", "New/Sub")]
        [TestCase("Old/Sub/Deep", "New/Sub/Deep")]
        [TestCase("Old/Sub/Deep", "New/Sub")]
        [TestCase("Old/Sub", "New/Sub/Deep")]
        [TestCase("Old.Sub", "New.Sub")]
        [TestCase("Old.Sub", "New")]
        [TestCase("Old", "New.Sub")]
        [TestCase("Old.Sub.Deep", "New.Sub.Deep")]
        [TestCase("Old.Sub.Deep", "New.Sub")]
        [TestCase("Old.Sub", "New.Sub.Deep")]
        public async Task UpdateFolderPathAsyncMustMoveMessagesToRenamedFolder(string oldFolderName, string newFolderName)
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            var account = TestData.CreateAccountWithFolder(new EmailAddress("rename-loss@test", "Rename Loss"));
            Assert.That(account.FoldersStructure.Count, Is.EqualTo(1));

            var oldFolder = account.FoldersStructure[0];
            oldFolder.FullName = oldFolderName;
            oldFolder.Attributes = FolderAttributes.None;

            await db.AddAccountAsync(account).ConfigureAwait(true);

            var message1 = TestData.CreateNewMessage(oldFolderName, 123, DateTimeOffset.UtcNow);
            var message2 = TestData.CreateNewMessage(oldFolderName, 124, DateTimeOffset.UtcNow);
            await db.AddMessageAsync(account.Email, message1).ConfigureAwait(true);
            await db.AddMessageAsync(account.Email, message2).ConfigureAwait(true);
            Assert.That(await db.IsMessageExistAsync(account.Email, oldFolderName, 123).ConfigureAwait(true), Is.True);
            Assert.That(await db.IsMessageExistAsync(account.Email, oldFolderName, 124).ConfigureAwait(true), Is.True);

            // Simulate rename flow that only updates Message.Path.
            await db.UpdateFolderPathAsync(account.Email, oldFolderName, newFolderName).ConfigureAwait(true);
            Assert.That(await db.IsMessageExistAsync(account.Email, newFolderName, 123).ConfigureAwait(true), Is.True);
            Assert.That(await db.IsMessageExistAsync(account.Email, newFolderName, 124).ConfigureAwait(true), Is.True);
        }

        [TestCase("Old", "Sub1", "Sub2", "New")]
        [TestCase("Old", "Sub1", "Sub2", "Other/New")]
        [TestCase("Old", "Sub1", "Sub2", "New/Sub")]
        [TestCase("Old", "Sub1", "Sub2", "New/Sub1/Sub2")]
        [TestCase("Old", "Sub1", "Sub2", "Old/NewSub")]
        [TestCase("Old", "Sub1", "Sub2", "Old/Sub1/NewSub")]
        [TestCase("Old", "Sub1", "Sub2", "Old/Sub1/Sub2/NewSub")]
        public async Task UpdateFolderPathAsyncMustMoveMessagesInFolderTree(string oldRoot,
                                                                            string subFolder1,
                                                                            string subFolder2,
                                                                            string newRoot)
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            const string delimiter = "/";
            string oldSub1 = oldRoot + delimiter + subFolder1;
            string oldSub2 = oldSub1 + delimiter + subFolder2;

            string newSub1 = newRoot + delimiter + subFolder1;
            string newSub2 = newSub1 + delimiter + subFolder2;

            var account = TestData.CreateAccountWithFolder(new EmailAddress("rename-loss@test", "Rename Loss"));
            Assert.That(account.FoldersStructure.Count, Is.EqualTo(1));

            account.FoldersStructure[0].FullName = oldRoot;
            account.FoldersStructure[0].Attributes = FolderAttributes.None;
            account.FoldersStructure.Add(new Folder(oldSub1, FolderAttributes.None));
            account.FoldersStructure.Add(new Folder(oldSub2, FolderAttributes.None));

            await db.AddAccountAsync(account).ConfigureAwait(true);

            await db.AddMessageAsync(account.Email, TestData.CreateNewMessage(oldRoot, 201, DateTimeOffset.UtcNow)).ConfigureAwait(true);
            await db.AddMessageAsync(account.Email, TestData.CreateNewMessage(oldSub1, 202, DateTimeOffset.UtcNow)).ConfigureAwait(true);
            await db.AddMessageAsync(account.Email, TestData.CreateNewMessage(oldSub2, 203, DateTimeOffset.UtcNow)).ConfigureAwait(true);

            Assert.That(await db.IsMessageExistAsync(account.Email, oldRoot, 201).ConfigureAwait(true), Is.True);
            Assert.That(await db.IsMessageExistAsync(account.Email, oldSub1, 202).ConfigureAwait(true), Is.True);
            Assert.That(await db.IsMessageExistAsync(account.Email, oldSub2, 203).ConfigureAwait(true), Is.True);

            await db.UpdateFolderPathAsync(account.Email, oldRoot, newRoot).ConfigureAwait(true);

            Assert.That(await db.IsMessageExistAsync(account.Email, newRoot, 201).ConfigureAwait(true), Is.True);
            Assert.That(await db.IsMessageExistAsync(account.Email, newSub1, 202).ConfigureAwait(true), Is.True);
            Assert.That(await db.IsMessageExistAsync(account.Email, newSub2, 203).ConfigureAwait(true), Is.True);
        }

        [TestCase("Old", "New")]
        [TestCase("Old/Sub", "New/Sub")]
        [TestCase("Old/Sub", "New/RenamedSub")]
        [TestCase("Old.Sub", "New.Sub")]
        public async Task UpdateAccountFolderStructureAfterRenameMustNotDeleteMessages(string oldFolderName, string newFolderName)
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            var account = TestData.CreateAccountWithFolder(new EmailAddress("rename-loss@test", "Rename Loss"));
            Assert.That(account.FoldersStructure.Count, Is.EqualTo(1));

            account.FoldersStructure[0].FullName = oldFolderName;
            account.FoldersStructure[0].Attributes = FolderAttributes.None;

            await db.AddAccountAsync(account).ConfigureAwait(true);

            await db.AddMessageAsync(account.Email, TestData.CreateNewMessage(oldFolderName, 301, DateTimeOffset.UtcNow)).ConfigureAwait(true);
            Assert.That(await db.IsMessageExistAsync(account.Email, oldFolderName, 301).ConfigureAwait(true), Is.True);

            await db.UpdateFolderPathAsync(account.Email, oldFolderName, newFolderName).ConfigureAwait(true);
            Assert.That(await db.IsMessageExistAsync(account.Email, newFolderName, 301).ConfigureAwait(true), Is.True);

            account.FoldersStructure = new List<Folder>
            {
                new Folder(newFolderName, FolderAttributes.None)
            };
            await db.UpdateAccountFolderStructureAsync(account).ConfigureAwait(true);

            Assert.That(await db.IsMessageExistAsync(account.Email, newFolderName, 301).ConfigureAwait(true), Is.True);
        }

        [TestCase("Old", "Sub1", "Sub2", "New")]
        [TestCase("Old", "Sub1", "Sub2", "Other/New")]
        [TestCase("Old", "Sub1", "Sub2", "New/Sub")]
        public async Task UpdateFolderPathAsyncMustMoveMessagesInFolderTreeWithDotDelimiter(string oldRoot,
                                                                                            string subFolder1,
                                                                                            string subFolder2,
                                                                                            string newRoot)
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            const string delimiter = "/";
            string oldSub1 = oldRoot + delimiter + subFolder1;
            string oldSub2 = oldSub1 + delimiter + subFolder2;

            string newSub1 = newRoot + delimiter + subFolder1;
            string newSub2 = newSub1 + delimiter + subFolder2;

            var account = TestData.CreateAccountWithFolder(new EmailAddress("rename-loss@test", "Rename Loss"));
            Assert.That(account.FoldersStructure.Count, Is.EqualTo(1));

            account.FoldersStructure[0].FullName = oldRoot;
            account.FoldersStructure[0].Attributes = FolderAttributes.None;
            account.FoldersStructure.Add(new Folder(oldSub1, FolderAttributes.None));
            account.FoldersStructure.Add(new Folder(oldSub2, FolderAttributes.None));

            await db.AddAccountAsync(account).ConfigureAwait(true);

            await db.AddMessageAsync(account.Email, TestData.CreateNewMessage(oldRoot, 401, DateTimeOffset.UtcNow)).ConfigureAwait(true);
            await db.AddMessageAsync(account.Email, TestData.CreateNewMessage(oldSub1, 402, DateTimeOffset.UtcNow)).ConfigureAwait(true);
            await db.AddMessageAsync(account.Email, TestData.CreateNewMessage(oldSub2, 403, DateTimeOffset.UtcNow)).ConfigureAwait(true);

            await db.UpdateFolderPathAsync(account.Email, oldRoot, newRoot).ConfigureAwait(true);

            Assert.That(await db.IsMessageExistAsync(account.Email, newRoot, 401).ConfigureAwait(true), Is.True);
            Assert.That(await db.IsMessageExistAsync(account.Email, newSub1, 402).ConfigureAwait(true), Is.True);
            Assert.That(await db.IsMessageExistAsync(account.Email, newSub2, 403).ConfigureAwait(true), Is.True);
        }

        [Test]
        public async Task UpdateFolderPathAsyncMustNotAffectSimilarPrefixFolders()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            const string oldFolderName = "Old";
            const string similarFolderName = "OldX";
            const string newFolderName = "New";

            var account = TestData.CreateAccountWithFolder(new EmailAddress("rename-loss@test", "Rename Loss"));
            Assert.That(account.FoldersStructure.Count, Is.EqualTo(1));

            account.FoldersStructure[0].FullName = oldFolderName;
            account.FoldersStructure[0].Attributes = FolderAttributes.None;
            account.FoldersStructure.Add(new Folder(similarFolderName, FolderAttributes.None));

            await db.AddAccountAsync(account).ConfigureAwait(true);

            await db.AddMessageAsync(account.Email, TestData.CreateNewMessage(oldFolderName, 501, DateTimeOffset.UtcNow)).ConfigureAwait(true);
            await db.AddMessageAsync(account.Email, TestData.CreateNewMessage(similarFolderName, 502, DateTimeOffset.UtcNow)).ConfigureAwait(true);

            await db.UpdateFolderPathAsync(account.Email, oldFolderName, newFolderName).ConfigureAwait(true);

            Assert.That(await db.IsMessageExistAsync(account.Email, newFolderName, 501).ConfigureAwait(true), Is.True);
            Assert.That(await db.IsMessageExistAsync(account.Email, similarFolderName, 502).ConfigureAwait(true), Is.True);
        }

        [Test]
        public async Task UpdateFolderPathAsyncMustPreserveFolderIdForMessages()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            const string oldFolderName = "Old";
            const string newFolderName = "New";

            var account = TestData.CreateAccountWithFolder(new EmailAddress("rename-loss@test", "Rename Loss"));
            Assert.That(account.FoldersStructure.Count, Is.EqualTo(1));

            account.FoldersStructure[0].FullName = oldFolderName;
            account.FoldersStructure[0].Attributes = FolderAttributes.None;
            await db.AddAccountAsync(account).ConfigureAwait(true);

            await db.AddMessageAsync(account.Email, TestData.CreateNewMessage(oldFolderName, 601, DateTimeOffset.UtcNow)).ConfigureAwait(true);
            var messageBefore = await db.GetMessageAsync(account.Email, oldFolderName, 601, fast: true).ConfigureAwait(true);
            Assert.That(messageBefore, Is.Not.Null);
            int folderIdBefore = messageBefore.FolderId;

            await db.UpdateFolderPathAsync(account.Email, oldFolderName, newFolderName).ConfigureAwait(true);

            var messageAfter = await db.GetMessageAsync(account.Email, newFolderName, 601, fast: true).ConfigureAwait(true);
            Assert.That(messageAfter, Is.Not.Null);
            Assert.That(messageAfter.FolderId, Is.EqualTo(folderIdBefore));
        }

        [Test]
        public async Task UpdateFolderPathAsyncMustThrowIfFolderDoesNotExist()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            var account = TestData.CreateAccountWithFolder(new EmailAddress("rename-loss@test", "Rename Loss"));
            Assert.That(account.FoldersStructure.Count, Is.EqualTo(1));

            account.FoldersStructure[0].FullName = "InboxLike";
            account.FoldersStructure[0].Attributes = FolderAttributes.None;
            await db.AddAccountAsync(account).ConfigureAwait(true);

            Assert.ThrowsAsync<DataBaseException>(async () =>
            {
                await db.UpdateFolderPathAsync(account.Email, "DoesNotExist", "New").ConfigureAwait(true);
            });
        }
    }
}
