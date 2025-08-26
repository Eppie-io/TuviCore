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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Proton;

namespace Tuvi.Core.DataStorage.Tests
{
    public class ProtonTests : TestWithStorageBase
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

        private static Tuvi.Proton.Message PM(uint id, params string[] labels) => new Tuvi.Proton.Message { MessageId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), LabelIds = labels.ToList() };

        [Test]
        public async Task LabelRemovalOnUpdate()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            var protonStorage = (IStorage)db;
            var account = TestData.CreateAccount();
            await db.AddAccountAsync(account).ConfigureAwait(true);

            var msg = PM(100, "A", "B", "C");
            await protonStorage.AddOrUpdateMessagesAsync(account.Id, new List<Tuvi.Proton.Message> { msg }).ConfigureAwait(true);
            // remove label C
            msg.LabelIds = new List<string> { "A", "B" };
            await protonStorage.AddOrUpdateMessagesAsync(account.Id, new List<Tuvi.Proton.Message> { msg }).ConfigureAwait(true);

            var a = await protonStorage.GetMessagesAsync(account.Id, "A", 0, true, 10, default).ConfigureAwait(true);
            var b = await protonStorage.GetMessagesAsync(account.Id, "B", 0, true, 10, default).ConfigureAwait(true);
            var c = await protonStorage.GetMessagesAsync(account.Id, "C", 0, true, 10, default).ConfigureAwait(true);
            Assert.That(a.Count, Is.EqualTo(1));
            Assert.That(b.Count, Is.EqualTo(1));
            Assert.That(c.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task IdempotentAddOrUpdateMessagesAsync()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            var protonStorage = (IStorage)db;
            var account = TestData.CreateAccount();
            await db.AddAccountAsync(account).ConfigureAwait(true);

            var batch = new List<Tuvi.Proton.Message> { PM(200, "L"), PM(201, "L"), PM(202, "L") };
            await protonStorage.AddOrUpdateMessagesAsync(account.Id, batch).ConfigureAwait(true);
            // repeat identical batch
            await protonStorage.AddOrUpdateMessagesAsync(account.Id, batch).ConfigureAwait(true);

            var labelMessages = await protonStorage.GetMessagesAsync(account.Id, "L", 0, true, 100, default).ConfigureAwait(true);
            Assert.That(labelMessages.Select(m => m.MessageId), Is.EquivalentTo(new[] { "200", "201", "202" }));
        }

        [Test]
        public async Task SeparateAccountsSameLabels()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            var protonStorage = (IStorage)db;
            var account1 = TestData.CreateAccount();
            var account2 = new Entities.Account();
            account2.Email = new Entities.EmailAddress("second@test.local", "");

            await db.AddAccountAsync(account1).ConfigureAwait(true);
            await db.AddAccountAsync(account2).ConfigureAwait(true);

            await protonStorage.AddOrUpdateMessagesAsync(account1.Id, new List<Tuvi.Proton.Message> { PM(300, "X"), PM(301, "X") }).ConfigureAwait(true);
            await protonStorage.AddOrUpdateMessagesAsync(account2.Id, new List<Tuvi.Proton.Message> { PM(400, "X") }).ConfigureAwait(true);

            var a1 = await protonStorage.GetMessagesAsync(account1.Id, "X", 0, true, 10, default).ConfigureAwait(true);
            var a2 = await protonStorage.GetMessagesAsync(account2.Id, "X", 0, true, 10, default).ConfigureAwait(true);
            Assert.That(a1.Select(x => x.MessageId), Is.EquivalentTo(new[] { "300", "301" }));
            Assert.That(a2.Select(x => x.MessageId), Is.EquivalentTo(new[] { "400" }));
        }

        [Test]
        public async Task EmptyLabelIdsProducesNoAssociations()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            var protonStorage = (IStorage)db;
            var account = TestData.CreateAccount();
            await db.AddAccountAsync(account).ConfigureAwait(true);

            await protonStorage.AddOrUpdateMessagesAsync(account.Id, new List<Tuvi.Proton.Message> { PM(500) }).ConfigureAwait(true);
            // Query arbitrary label should return none
            var any = await protonStorage.GetMessagesAsync(account.Id, "NonExisting", 0, true, 10, default).ConfigureAwait(true);
            Assert.That(any.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task GetMessagesAsyncEarlierLaterBoundaries()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            var protonStorage = (IStorage)db;
            var account = TestData.CreateAccount();
            await db.AddAccountAsync(account).ConfigureAwait(true);
            var msgs = Enumerable.Range(1, 5).Select(i => PM((uint)(600 + i), "B")).ToList(); // MessageId = 601..605 (string)
            await protonStorage.AddOrUpdateMessagesAsync(account.Id, msgs).ConfigureAwait(true);

            // Load to obtain internal auto-increment Id values (primary key) mapped to MessageId strings
            var all = await protonStorage.GetMessagesAsync(account.Id, "B", 0, getEarlier: true, count: 100, default).ConfigureAwait(true);
            var known604InternalId = (uint)all.First(m => m.MessageId == "604").Id; // internal PK for MessageId 604
            var known602InternalId = (uint)all.First(m => m.MessageId == "602").Id; // internal PK for MessageId 602

            // earlier from known internal Id (exclusive): expect messages with smaller internal Ids => older MessageIds 603,602,601
            var earlier = await protonStorage.GetMessagesAsync(account.Id, "B", known604InternalId, getEarlier: true, count: 10, default).ConfigureAwait(true);
            Assert.That(earlier.Select(x => x.MessageId), Is.EquivalentTo(new[] { "603", "602", "601" }));

            // later from known internal Id (exclusive): expect newer messages (higher internal Ids) => 605,604,603
            var later = await protonStorage.GetMessagesAsync(account.Id, "B", known602InternalId, getEarlier: false, count: 10, default).ConfigureAwait(true);
            Assert.That(later.Select(x => x.MessageId), Is.EquivalentTo(new[] { "605", "604", "603" }));
        }

        private static Tuvi.Proton.Message CreateProtonMessage(uint id, params string[] labelIds)
        {
            return new Tuvi.Proton.Message
            {
                MessageId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                LabelIds = labelIds.ToList()
            };
        }

        [Test]
        public async Task AddOrUpdateMessagesDuplicateLabelsIgnored()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            var protonStorage = (IStorage)db;
            var account = TestData.CreateAccount();
            await db.AddAccountAsync(account).ConfigureAwait(true);

            var msg = CreateProtonMessage(1, "L1", "L2", "L1", "L2", "L3"); // duplicates
            await protonStorage.AddOrUpdateMessagesAsync(account.Id, new List<Proton.Message> { msg }).ConfigureAwait(true);

            // Update the same message with another set containing previous + duplicates + new label
            msg.LabelIds = new List<string> { "L1", "L2", "L2", "L4", "L4" };
            await protonStorage.AddOrUpdateMessagesAsync(account.Id, new List<Proton.Message> { msg }).ConfigureAwait(true);

            // Load labels via GetMessagesAsync by each label id and ensure presence/absence
            var m1 = await protonStorage.GetMessagesAsync(account.Id, "L1", 0, getEarlier: true, 10, default).ConfigureAwait(true);
            var m2 = await protonStorage.GetMessagesAsync(account.Id, "L2", 0, getEarlier: true, 10, default).ConfigureAwait(true);
            var m3 = await protonStorage.GetMessagesAsync(account.Id, "L3", 0, getEarlier: true, 10, default).ConfigureAwait(true);
            var m4 = await protonStorage.GetMessagesAsync(account.Id, "L4", 0, getEarlier: true, 10, default).ConfigureAwait(true);

            Assert.That(m1.Count, Is.EqualTo(1));
            Assert.That(m2.Count, Is.EqualTo(1));
            Assert.That(m3.Count, Is.EqualTo(0), "Label L3 should have been removed on update");
            Assert.That(m4.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task AddOrUpdateMessagesLabelTableUniquePerAccount()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            var protonStorage = (IStorage)db;
            var account = TestData.CreateAccount();
            await db.AddAccountAsync(account).ConfigureAwait(true);

            var msgs = new List<Proton.Message>
            {
                CreateProtonMessage(10, "Inbox", "Starred"),
                CreateProtonMessage(11, "Inbox", "Starred", "Starred")
            };
            await protonStorage.AddOrUpdateMessagesAsync(account.Id, msgs).ConfigureAwait(true);

            // Add another batch with same labels to ensure no duplicates in ProtonLabelV3
            var more = new List<Proton.Message>
            {
                CreateProtonMessage(12, "Inbox"),
                CreateProtonMessage(13, "Starred")
            };
            await protonStorage.AddOrUpdateMessagesAsync(account.Id, more).ConfigureAwait(true);

            // Query messages per label
            var inbox = await protonStorage.GetMessagesAsync(account.Id, "Inbox", 0, true, 100, default).ConfigureAwait(true);
            var starred = await protonStorage.GetMessagesAsync(account.Id, "Starred", 0, true, 100, default).ConfigureAwait(true);

            Assert.That(inbox.Select(x => x.MessageId), Is.EquivalentTo(new[] { "10", "11", "12" }));
            Assert.That(starred.Select(x => x.MessageId), Is.EquivalentTo(new[] { "10", "11", "13" }));
        }
    }
}
