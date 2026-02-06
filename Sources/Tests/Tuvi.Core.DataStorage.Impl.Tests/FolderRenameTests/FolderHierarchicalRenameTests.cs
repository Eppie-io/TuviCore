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
using Tuvi.Core.DataStorage.Tests;
using Tuvi.Core.Entities;

namespace Tuvi.Core.DataStorage.Impl.Tests.FolderRenameTests
{
    public class FolderHierarchicalRenameTests : TestWithStorageBase
    {
        public static IEnumerable<TestCaseData> HierarchyRenameCases
        {
            get
            {
                // Format: oldBase|newBase|descendantRelative1|descendantRelative2|...
                yield return new TestCaseData("Parent|NewParent|Child|Child/GrandChild")
                    .SetName("RenameHierarchy: Parent -> NewParent");

                yield return new TestCaseData("Projects|ProjectsRenamed|2026|2026/Q1")
                    .SetName("RenameHierarchy: Projects -> ProjectsRenamed");

                yield return new TestCaseData("Root/Sub|Root/SubRenamed|Leaf|Leaf/Deep")
                    .SetName("RenameHierarchy: Root/Sub -> Root/SubRenamed");

                yield return new TestCaseData("A/B/C|A/B/C_Renamed|D|D/E")
                    .SetName("RenameHierarchy: A/B/C -> A/B/C_Renamed");

                yield return new TestCaseData("Case/Insensitive|case/insensitive|Child|Child/GrandChild")
                    .SetName("RenameHierarchy: case-insensitive rename");

                yield return new TestCaseData(" Spaced | SpacedRenamed |Child |Child ./Grand.child")
                    .SetName("RenameHierarchy: spaces+dots");

                yield return new TestCaseData("Δοκιμή/Φάκελος|Δοκιμή/ΦάκελοςΜετονομασία|Παιδί|Παιδί/Εγγόνι")
                    .SetName("RenameHierarchy: Greek");

                yield return new TestCaseData("Deep/A/B/C/D|Deep/A/B/C/D_R|E|E/F|E/F/G/H|I/J/K")
                    .SetName("RenameHierarchy: deep paths");

                yield return new TestCaseData("A|X/Y|B|B/C")
                    .SetName("RenameHierarchy: increase depth A -> X/Y");

                yield return new TestCaseData("A/B|X|C|C/D")
                    .SetName("RenameHierarchy: decrease depth A/B -> X");

                yield return new TestCaseData("A/B|X/Y/Z|C|C/D")
                    .SetName("RenameHierarchy: branch switch + increase depth A/B -> X/Y/Z");

                yield return new TestCaseData("A/B/C|X|D|D/E")
                    .SetName("RenameHierarchy: branch switch + decrease depth A/B/C -> X");

                yield return new TestCaseData(" Alpha/Beta |Gamma/Delta/Epsilon|Zeta|Zeta/Eta")
                    .SetName("RenameHierarchy: spaces + increase depth");

                yield return new TestCaseData("Δοκιμή|X/Y|Φάκελος|Φάκελος/Παιδί")
                    .SetName("RenameHierarchy: Greek + increase depth");

                yield return new TestCaseData("Mail/✉️|Mail/✉️_R|📦|📦/📬")
                    .SetName("RenameHierarchy: emoji+variation selector");
            }
        }

        public static IEnumerable<TestCaseData> MessageMoveCases
        {
            get
            {
                // Format: oldBase|newBase|descendantRelative
                yield return new TestCaseData("Parent|NewParent|Child")
                    .SetName("MoveMessages: Parent -> NewParent");

                yield return new TestCaseData("Projects|ProjectsRenamed|2026")
                    .SetName("MoveMessages: Projects -> ProjectsRenamed");

                yield return new TestCaseData("Root/Sub|Root/SubRenamed|Leaf")
                    .SetName("MoveMessages: Root/Sub -> Root/SubRenamed");

                yield return new TestCaseData("A/B/C|A/B/C_Renamed|D")
                    .SetName("MoveMessages: A/B/C -> A/B/C_Renamed");

                yield return new TestCaseData("Case/Insensitive|case/insensitive|Child")
                    .SetName("MoveMessages: case-insensitive rename");

                yield return new TestCaseData(" Φάκελος | ΦάκελοςΝέος |Θυγατρικός φάκελος")
                    .SetName("MoveMessages: Greek+spaces");

                yield return new TestCaseData("Deep/A/B|Deep/A/B_R|C/D/E/F")
                    .SetName("MoveMessages: deep descendant");

                yield return new TestCaseData("A|X/Y|B")
                    .SetName("MoveMessages: increase depth A -> X/Y");

                yield return new TestCaseData("A/B|X|C")
                    .SetName("MoveMessages: decrease depth A/B -> X");

                yield return new TestCaseData("A/B|X/Y/Z|C")
                    .SetName("MoveMessages: branch switch + increase depth A/B -> X/Y/Z");

                yield return new TestCaseData("A/B/C|X|D")
                    .SetName("MoveMessages: branch switch + decrease depth A/B/C -> X");

                yield return new TestCaseData(" Alpha/Beta |Gamma/Delta/Epsilon|Zeta")
                    .SetName("MoveMessages: spaces + increase depth");

                yield return new TestCaseData("Mail/✉️|Mail/✉️_R|📬/📭")
                    .SetName("MoveMessages: emoji/variation selector");
            }
        }

        public static IEnumerable<TestCaseData> PrefixCollisionCases
        {
            get
            {
                // Format: oldBase|newBase|unrelatedFolder
                yield return new TestCaseData("Prefix|NewName|PrefixSuffix")
                    .SetName("PrefixCollision: Prefix vs PrefixSuffix");

                yield return new TestCaseData("Root/Sub|Root/SubRenamed|Root/SubSuffix")
                    .SetName("PrefixCollision: Root/Sub vs Root/SubSuffix");

                yield return new TestCaseData("A/B|A/B_Renamed|A/BSuffix")
                    .SetName("PrefixCollision: A/B vs A/BSuffix");

                yield return new TestCaseData("A/B|A/B_Renamed|A/B2")
                    .SetName("PrefixCollision: segment boundary A/B vs A/B2");

                yield return new TestCaseData("A/B|A/B_Renamed|A/B2/C")
                    .SetName("PrefixCollision: segment boundary A/B vs A/B2/C");

                yield return new TestCaseData("Case/Prefix|case/prefix_new|CASE/PrefixSuffix")
                    .SetName("PrefixCollision: case-insensitive");

                yield return new TestCaseData(" Space|SpaceNew| SpaceSuffix")
                    .SetName("PrefixCollision: spaces");

                yield return new TestCaseData("Dot.|DotNew.|Dot.Suffix")
                    .SetName("PrefixCollision: dots");

                yield return new TestCaseData("Root/Sub/Leaf|Root/SubLeaf|Root/Sub/LeafSuffix")
                    .SetName("PrefixCollision: decrease depth should not touch LeafSuffix");

                yield return new TestCaseData("📁|📂|📁Suffix")
                    .SetName("PrefixCollision: emoji");

                yield return new TestCaseData("Mail/✉️|Mail/✉️_R|Mail/✉️Suffix")
                    .SetName("PrefixCollision: emoji+variation selector");
            }
        }

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

        [TestCaseSource(nameof(HierarchyRenameCases))]
        public async Task UpdateFolderPathAsyncShouldRenameDescendantFoldersCorrectly(string caseData)
        {
            ArgumentNullException.ThrowIfNull(caseData);
            var parts = caseData.Split('|');
            Assert.That(parts.Length, Is.GreaterThanOrEqualTo(2), "Case data must be: oldBase|newBase|desc1|desc2|...");

            var oldBase = parts[0];
            var newBase = parts[1];
            var descendantsRelative = parts.Skip(2).ToArray();

            var oldPaths = new[] { oldBase }
                .Concat(descendantsRelative.Select(r => CombinePath(oldBase, r)))
                .ToArray();

            var newPaths = new[] { newBase }
                .Concat(descendantsRelative.Select(r => CombinePath(newBase, r)))
                .ToArray();

            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            // 1. Create Account
            var account = TestData.CreateAccountWithFolder(new EmailAddress("subrenametest@test.com", "Sub Rename Test"));
            // Clear default folders to start fresh
            account.FoldersStructure.Clear();

            // 2. Create Hierarchy
            foreach (var path in oldPaths)
            {
                account.FoldersStructure.Add(new Folder(path, FolderAttributes.None));
            }

            // Add account to DB (this adds folders too)
            await db.AddAccountAsync(account).ConfigureAwait(true);

            // 3. Rename
            await db.UpdateFolderPathAsync(account.Email, oldBase, newBase).ConfigureAwait(true);

            // 4. Verify folders in DB
            var storedAccount = await db.GetAccountAsync(account.Email).ConfigureAwait(true);
            var folders = storedAccount.FoldersStructure;

            // Debug info
            foreach (var f in folders)
            {
                TestContext.WriteLine($"Folder: {f.FullName}");
            }

            // Assertions
            foreach (var path in newPaths)
            {
                Assert.That(folders.Any(f => f.FullName == path), Is.True, $"Expected renamed folder '{path}' to exist");
            }

            foreach (var path in oldPaths)
            {
                Assert.That(folders.Any(f => f.FullName == path), Is.False, $"Old folder '{path}' should be gone after rename");
            }
        }

        [Test]
        public async Task RenameFolderWithNonExistentFolderThrowsException()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            var account = TestData.CreateAccountWithFolder(new EmailAddress("nonexistent@test.com", "NonExistent"));
            await db.AddAccountAsync(account).ConfigureAwait(true);

            Assert.ThrowsAsync<DataBaseException>(async () =>
                await db.UpdateFolderPathAsync(account.Email, "Ghost", "Buster").ConfigureAwait(true));
        }

        [TestCaseSource(nameof(PrefixCollisionCases))]
        public async Task RenameFolderWithPrefixCollisionDoesNotRenameUnrelatedFolders(string caseData)
        {
            ArgumentNullException.ThrowIfNull(caseData);
            var parts = caseData.Split('|');
            Assert.That(parts.Length, Is.EqualTo(3), "Case data must be: oldBase|newBase|unrelatedFolder");

            var oldBase = parts[0];
            var newBase = parts[1];
            var unrelatedFolder = parts[2];

            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            var account = TestData.CreateAccountWithFolder(new EmailAddress("collision@test.com", "Collision Test"));
            account.FoldersStructure.Clear();

            var target = new Folder(oldBase, FolderAttributes.None);
            var unrelated = new Folder(unrelatedFolder, FolderAttributes.None);
            account.FoldersStructure.Add(target);
            account.FoldersStructure.Add(unrelated);
            await db.AddAccountAsync(account).ConfigureAwait(true);

            await db.UpdateFolderPathAsync(account.Email, oldBase, newBase).ConfigureAwait(true);

            var storedAccount = await db.GetAccountAsync(account.Email).ConfigureAwait(true);
            var folders = storedAccount.FoldersStructure;

            Assert.That(folders.Any(f => f.FullName == newBase), Is.True);
            Assert.That(folders.Any(f => f.FullName == unrelatedFolder), Is.True, "Unrelated folder should stay unchanged");
            if (unrelatedFolder.StartsWith(oldBase, StringComparison.Ordinal))
            {
                var shouldNotExist = ReplacePrefixPath(unrelatedFolder, oldBase, newBase);
                Assert.That(folders.Any(f => f.FullName == shouldNotExist), Is.False);
            }
        }

        [TestCaseSource(nameof(MessageMoveCases))]
        public async Task RenameFolderUpdatesMessagePathsInRenamedFolderAndSubfolders(string caseData)
        {
            ArgumentNullException.ThrowIfNull(caseData);
            var parts = caseData.Split('|');
            Assert.That(parts.Length, Is.EqualTo(3), "Case data must be: oldBase|newBase|descendantRelative");

            var oldBase = parts[0];
            var newBase = parts[1];
            var descendantRelative = parts[2];

            var oldDescendantPath = CombinePath(oldBase, descendantRelative);
            var newDescendantPath = CombinePath(newBase, descendantRelative);

            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            var account = TestData.CreateAccountWithFolder(new EmailAddress("msgmove@test.com", "Msg Move Test"));
            account.FoldersStructure.Clear();

            account.FoldersStructure.Add(new Folder(oldBase, FolderAttributes.None));
            account.FoldersStructure.Add(new Folder(oldDescendantPath, FolderAttributes.None));
            await db.AddAccountAsync(account).ConfigureAwait(true);

            // Add messages
            var msg1 = TestData.CreateNewMessage(oldBase, 1, DateTimeOffset.UtcNow);
            var msg2 = TestData.CreateNewMessage(oldDescendantPath, 2, DateTimeOffset.UtcNow);
            await db.AddMessageAsync(account.Email, msg1).ConfigureAwait(true);
            await db.AddMessageAsync(account.Email, msg2).ConfigureAwait(true);

            // Rename
            await db.UpdateFolderPathAsync(account.Email, oldBase, newBase).ConfigureAwait(true);

            // Validation logic
            Assert.That(await db.IsMessageExistAsync(account.Email, newBase, 1).ConfigureAwait(true), Is.True, "Message 1 should exist at new base path");
            Assert.That(await db.IsMessageExistAsync(account.Email, newDescendantPath, 2).ConfigureAwait(true), Is.True, "Message 2 should exist at new descendant path");

            Assert.That(await db.IsMessageExistAsync(account.Email, oldBase, 1).ConfigureAwait(true), Is.False);
            Assert.That(await db.IsMessageExistAsync(account.Email, oldDescendantPath, 2).ConfigureAwait(true), Is.False);
        }

        private static string CombinePath(string basePath, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return basePath;
            }

            return $"{basePath}/{relativePath}";
        }

        private static string ReplacePrefixPath(string path, string oldBase, string newBase)
        {
            if (path.StartsWith(oldBase, StringComparison.Ordinal))
            {
                return newBase + path[oldBase.Length..];
            }

            return path;
        }
    }
}
