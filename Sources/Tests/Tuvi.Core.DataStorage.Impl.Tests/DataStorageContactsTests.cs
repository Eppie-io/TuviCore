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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Entities;
using Tuvi.Core.DataStorage;

namespace Tuvi.Core.DataStorage.Tests
{
    public class ContactsTests : TestWithStorageBase
    {
        private static Message CreateCleanUnreadMessageFrom(EmailAddress from, DateTimeOffset? date = null)
        {
            var message = TestData.GetNewUnreadMessage();

            message.From.Clear();
            message.To.Clear();
            message.Cc.Clear();
            message.Bcc.Clear();
            message.ReplyTo.Clear();

            message.From.Add(from);
            if (date is not null)
            {
                message.Date = date.Value;
            }

            return message;
        }

        private static Task AddMessageAsync(IDataStorage db, EmailAddress accountEmail, Message message, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            return db.AddMessageListAsync(accountEmail, TestData.Folder, new List<Message> { message }, updateUnreadAndTotal, cancellationToken);
        }

        [SetUp]
        public async Task SetupAsync()
        {
            DeleteStorage();
            TestData.Setup();

            await CreateDataStorageAsync().ConfigureAwait(false);
        }

        [TearDown]
        public void Teardown()
        {
            DeleteStorage();
        }

        [Test]
        public async Task AddContactToDataStorage()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            var email = TestData.Contact.Email;
            var contactToAdd = TestData.Contact;

            var preExists = await db.ExistsContactWithEmailAddressAsync(email, default).ConfigureAwait(false);
            Assert.That(preExists, Is.False);

            // Act
            await db.AddContactAsync(contactToAdd, default).ConfigureAwait(false);
            var isAdded = await db.ExistsContactWithEmailAddressAsync(email, default).ConfigureAwait(false);
            var stored = await db.GetContactAsync(email, default).ConfigureAwait(false);

            // Assert
            Assert.That(isAdded, Is.True);
            Assert.That(ContactsAreEqual(stored, contactToAdd), Is.True);
            Assert.That(stored.Email, Is.EqualTo(email));
        }

        [Test]
        public async Task AddContactWithoutEmailToDataStorage()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            var contact = new Contact { FullName = "Contact Name" };

            // Act + Assert
            var ex = Assert.CatchAsync<DataBaseException>(async () =>
                await db.AddContactAsync(contact, default).ConfigureAwait(false));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex!.Message, Does.Contain("invalid").IgnoreCase);

            // Arrange
            contact.Email = new EmailAddress("contact@address1.io");

            // Act + Assert
            Assert.DoesNotThrowAsync(async () =>
                await db.AddContactAsync(contact, default).ConfigureAwait(false));

            var exists = await db.ExistsContactWithEmailAddressAsync(contact.Email, default).ConfigureAwait(false);
            Assert.That(exists, Is.True);
        }

        [Test]
        public async Task AddContactWithAvatarToDataStorage()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            var contactToAdd = TestData.ContactWithAvatar;

            var preExists = await db.ExistsContactWithEmailAddressAsync(contactToAdd.Email, default).ConfigureAwait(false);
            Assert.That(preExists, Is.False);

            // Act
            await db.AddContactAsync(contactToAdd, default).ConfigureAwait(false);
            var isAdded = await db.ExistsContactWithEmailAddressAsync(contactToAdd.Email, default).ConfigureAwait(false);
            var stored = await db.GetContactAsync(contactToAdd.Email, default).ConfigureAwait(false);

            // Assert
            Assert.That(isAdded, Is.True);
            Assert.That(ContactsAreEqual(stored, contactToAdd), Is.True);
        }

        private static bool ContactsAreEqual(Contact c1, Contact c2)
        {
            if (c1 is null || c2 is null)
            {
                return c1 is null && c2 is null;
            }

            return Equals(c1.Email, c2.Email) &&
                   c1.FullName == c2.FullName &&
                   ImageInfosAreEqual(c1.AvatarInfo, c2.AvatarInfo);
        }

        private static bool ImageInfosAreEqual(ImageInfo i1, ImageInfo i2)
        {
            if (i1 is null || i2 is null)
            {
                return i1 is null && i2 is null;
            }

            if (i1.Width != i2.Width || i1.Height != i2.Height)
            {
                return false;
            }

            if (i1.Bytes == null && i2.Bytes == null)
            {
                return true;
            }

            return i1.Bytes != null && i2.Bytes != null && i1.Bytes.SequenceEqual(i2.Bytes);
        }

        [Test]
        public async Task SetContactAvatar()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            var contact = TestData.Contact;
            var avatar = TestData.ContactAvatar;

            var preExists = await db.ExistsContactWithEmailAddressAsync(contact.Email, default).ConfigureAwait(false);
            Assert.That(preExists, Is.False);

            await db.AddContactAsync(contact, default).ConfigureAwait(false);
            var existsAfterAdd = await db.ExistsContactWithEmailAddressAsync(contact.Email, default).ConfigureAwait(false);
            Assert.That(existsAfterAdd, Is.True);

            // Act + Assert
            var dupEx = Assert.CatchAsync<DataBaseException>(async () =>
                await db.AddContactAsync(TestData.ContactWithAvatar, default).ConfigureAwait(false));
            Assert.That(dupEx, Is.Not.Null);

            // Act
            await db.SetContactAvatarAsync(contact.Email, avatar.Bytes, avatar.Width, avatar.Height, default).ConfigureAwait(false);
            var stored = await db.GetContactAsync(contact.Email, default).ConfigureAwait(false);

            // Assert
            Assert.That(ImageInfosAreEqual(stored.AvatarInfo, avatar), Is.True);
        }

        [Test]
        public async Task RemoveContactAvatar()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            var contact = TestData.ContactWithAvatar;

            await db.AddContactAsync(contact, default).ConfigureAwait(false);
            var exists = await db.ExistsContactWithEmailAddressAsync(contact.Email, default).ConfigureAwait(false);
            Assert.That(exists, Is.True);

            // Act
            await db.RemoveContactAvatarAsync(contact.Email, default).ConfigureAwait(false);
            var stored = await db.GetContactAsync(contact.Email, default).ConfigureAwait(false);

            // Assert
            Assert.That(stored.AvatarInfo.IsEmpty, Is.True);
        }

        [Test]
        public async Task UpdateContact()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            var contact = TestData.Contact;

            await db.AddContactAsync(contact, default).ConfigureAwait(false);
            var exists = await db.ExistsContactWithEmailAddressAsync(contact.Email, default).ConfigureAwait(false);
            Assert.That(exists, Is.True);

            var stored = await db.GetContactAsync(contact.Email, default).ConfigureAwait(false);
            stored.FullName = "Updated name";

            // Act
            await db.UpdateContactAsync(stored, default).ConfigureAwait(false);
            var updated = await db.GetContactAsync(contact.Email, default).ConfigureAwait(false);

            // Assert
            Assert.That(ContactsAreEqual(stored, updated), Is.True);
        }

        [Test]
        public async Task ChangeContactAvatar()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            var contact = TestData.ContactWithAvatar;

            await db.AddContactAsync(contact, default).ConfigureAwait(false);
            var exists = await db.ExistsContactWithEmailAddressAsync(contact.Email, default).ConfigureAwait(false);
            Assert.That(exists, Is.True);

            var updatedAvatarInfo = new ImageInfo(360, 360, new byte[] { 25, 182, 137, 59, 46, 78, 69, 214 });

            // Act
            await db.SetContactAvatarAsync(contact.Email, updatedAvatarInfo.Bytes, updatedAvatarInfo.Width, updatedAvatarInfo.Height, default).ConfigureAwait(false);
            var stored = await db.GetContactAsync(contact.Email, default).ConfigureAwait(false);

            // Assert
            Assert.That(ImageInfosAreEqual(stored.AvatarInfo, updatedAvatarInfo), Is.True);
        }

        [Test]
        public async Task RemoveContactByEmail()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            var contact = TestData.Contact;

            await db.AddContactAsync(contact, default).ConfigureAwait(false);

            // Act
            await db.RemoveContactAsync(contact.Email, default).ConfigureAwait(false);
            var exists = await db.ExistsContactWithEmailAddressAsync(contact.Email, default).ConfigureAwait(false);

            // Assert
            Assert.That(exists, Is.False);
        }

        [Test]
        public async Task CheckLastMessageData()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            await db.AddContactAsync(TestData.Contact, default).ConfigureAwait(false);

            var contact = await db.GetContactAsync(TestData.Contact.Email, default).ConfigureAwait(false);
            Assert.That(contact.LastMessageData, Is.Null);
            Assert.That(contact.LastMessageDataId == 0, Is.True);

            await db.AddAccountAsync(TestData.Account).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            contact.LastMessageData = new LastMessageData(1, TestData.Account.Email, TestData.Message.Id, now);

            // Act
            await db.UpdateContactAsync(contact, default).ConfigureAwait(false);
            var stored = await db.GetContactAsync(TestData.Contact.Email, default).ConfigureAwait(false);

            // Assert
            Assert.That(stored.LastMessageData, Is.Not.Null);
            Assert.That(stored.LastMessageData.MessageId == TestData.Message.Id, Is.True);
            Assert.That(stored.LastMessageData.Date > DateTimeOffset.MinValue, Is.True);
            Assert.That(stored.LastMessageData.AccountEmail, Is.EqualTo(TestData.Account.Email));
        }

        [Test]
        public async Task TryAddContactAsync()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);

            // Act
            var first = await db.TryAddContactAsync(TestData.Contact, default).ConfigureAwait(false);
            var second = await db.TryAddContactAsync(TestData.Contact, default).ConfigureAwait(false);

            // Assert
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
        }

        [Test]
        public async Task GetUnknownContactShouldThrowException()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            await db.AddContactAsync(TestData.Contact, default).ConfigureAwait(false);

            // Act + Assert
            Assert.CatchAsync<DataBaseException>(async () =>
                await db.GetContactAsync(new EmailAddress("unknown@mail.box"), default).ConfigureAwait(false));
        }

        [Test]
        public async Task AddUnreadMessagesShouldIncrementContactUnreadCount()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(false);

            var accountEmail = TestData.AccountWithFolder.Email;
            var contact = TestData.Contact;
            await db.AddContactAsync(contact, default).ConfigureAwait(false);

            var message = TestData.GetNewUnreadMessage();
            message.From.Add(contact.Email);

            // Act
            await db.AddMessageListAsync(accountEmail, TestData.Folder, new List<Message> { message }, updateUnreadAndTotal: false).ConfigureAwait(false);
            var unreadCount = await db.GetContactUnreadMessagesCountAsync(contact.Email, default).ConfigureAwait(false);

            // Assert
            Assert.That(unreadCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddUnreadMessagesShouldIncrementSeveralContactUnreadCount()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(false);

            var accountEmail = TestData.AccountWithFolder.Email;
            var to = new Contact("To", TestData.To);
            var cc = new Contact("Cc", TestData.Cc);
            var bcc = new Contact("Bcc", TestData.Bcc);

            await db.AddContactAsync(to, default).ConfigureAwait(false);
            await db.AddContactAsync(cc, default).ConfigureAwait(false);
            await db.AddContactAsync(bcc, default).ConfigureAwait(false);

            var message = TestData.GetNewUnreadMessage();
            message.From.Clear();
            message.To.Clear();
            message.From.Add(accountEmail);
            message.To.Add(TestData.To);
            message.Cc.Add(TestData.Cc);
            message.Bcc.Add(TestData.Bcc);

            // Act
            await db.AddMessageListAsync(accountEmail, TestData.Folder, new List<Message> { message }, updateUnreadAndTotal: false).ConfigureAwait(false);
            var counts = (await db.GetUnreadMessagesCountByContactAsync(default).ConfigureAwait(false)).OrderBy(x => x.Key).ToList();

            // Assert
            Assert.That(counts.Count, Is.EqualTo(3));
            Assert.That(counts[0].Value, Is.EqualTo(1));
            Assert.That(counts[1].Value, Is.EqualTo(1));
            Assert.That(counts[2].Value, Is.EqualTo(1));

            Assert.That(counts[0].Key, Is.EqualTo(TestData.Bcc));
            Assert.That(counts[1].Key, Is.EqualTo(TestData.Cc));
            Assert.That(counts[2].Key, Is.EqualTo(TestData.To));
        }

        [Test]
        public async Task GetContactsPagedByTimeReturnsInExpectedOrderAndPaginates()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(false);

            var c1 = new Contact("C1", new EmailAddress("c1@ex.io"));
            var c2 = new Contact("C2", new EmailAddress("c2@ex.io"));
            var c3 = new Contact("C3", new EmailAddress("c3@ex.io"));

            await db.AddContactAsync(c1, CancellationToken.None).ConfigureAwait(false);
            await db.AddContactAsync(c2, CancellationToken.None).ConfigureAwait(false);
            await db.AddContactAsync(c3, CancellationToken.None).ConfigureAwait(false);

            var accountEmail = TestData.AccountWithFolder.Email;

            var t0 = DateTimeOffset.UtcNow;
            await AddMessageAsync(db, accountEmail, CreateCleanUnreadMessageFrom(c1.Email, t0.AddMinutes(1)), updateUnreadAndTotal: false, CancellationToken.None).ConfigureAwait(false);
            await AddMessageAsync(db, accountEmail, CreateCleanUnreadMessageFrom(c2.Email, t0.AddMinutes(2)), updateUnreadAndTotal: false, CancellationToken.None).ConfigureAwait(false);
            await AddMessageAsync(db, accountEmail, CreateCleanUnreadMessageFrom(c3.Email, t0.AddMinutes(3)), updateUnreadAndTotal: false, CancellationToken.None).ConfigureAwait(false);

            // Act
            var firstPage = await db.GetContactsAsync(2, lastContact: null, sortOrder: ContactsSortOrder.ByTime, cancellationToken: CancellationToken.None).ConfigureAwait(false);
            var secondPage = await db.GetContactsAsync(2, lastContact: firstPage[1], sortOrder: ContactsSortOrder.ByTime, cancellationToken: CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.That(firstPage.Count, Is.EqualTo(2));
            Assert.That(firstPage[0].Email.Address, Is.EqualTo(c3.Email.Address));
            Assert.That(firstPage[1].Email.Address, Is.EqualTo(c2.Email.Address));

            Assert.That(secondPage.Count, Is.EqualTo(1));
            Assert.That(secondPage[0].Email.Address, Is.EqualTo(c1.Email.Address));
        }

        [Test]
        public async Task GetContactsSortedByTimePlacesContactsWithoutLastMessageLast()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(false);

            var accountEmail = TestData.AccountWithFolder.Email;
            var withMsg = new Contact("With", new EmailAddress("with@ex.io"));
            var noMsg = new Contact("No", new EmailAddress("nomsg@ex.io"));

            await db.AddContactAsync(withMsg, CancellationToken.None).ConfigureAwait(false);
            await db.AddContactAsync(noMsg, CancellationToken.None).ConfigureAwait(false);

            await AddMessageAsync(db, accountEmail, CreateCleanUnreadMessageFrom(withMsg.Email), updateUnreadAndTotal: false, CancellationToken.None).ConfigureAwait(false);

            // Act
            var items = await db.GetContactsAsync(10, null, ContactsSortOrder.ByTime, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.That(items.Select(x => x.Email.Address).ToArray(), Is.EqualTo(new[] { "with@ex.io", "nomsg@ex.io" }));
        }

        [Test]
        public async Task GetContactsSortedByTimeDoesNotRegressWhenAddingOlderMessage()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(false);

            var accountEmail = TestData.AccountWithFolder.Email;
            var c = new Contact("C", new EmailAddress("c@ex.io"));
            await db.AddContactAsync(c, CancellationToken.None).ConfigureAwait(false);

            var newerDate = DateTimeOffset.UtcNow;
            await AddMessageAsync(db, accountEmail, CreateCleanUnreadMessageFrom(c.Email, newerDate), updateUnreadAndTotal: false, CancellationToken.None).ConfigureAwait(false);

            var contactAfterNew = (await db.GetContactsAsync(10, null, ContactsSortOrder.ByTime, CancellationToken.None).ConfigureAwait(false))
                .First(x => x.Email.Address == c.Email.Address);
            var lastIdAfterNew = contactAfterNew.LastMessageDataId;
            Assert.That(lastIdAfterNew, Is.GreaterThan(0));

            var older = CreateCleanUnreadMessageFrom(c.Email, newerDate.AddMinutes(-10));

            // Act
            await db.AddMessageListAsync(accountEmail, TestData.Folder, new List<Message> { older }, updateUnreadAndTotal: false, CancellationToken.None).ConfigureAwait(false);
            var contactAfterOld = (await db.GetContactsAsync(10, null, ContactsSortOrder.ByTime, CancellationToken.None).ConfigureAwait(false))
                .First(x => x.Email.Address == c.Email.Address);

            // Assert
            Assert.That(contactAfterOld.LastMessageDataId, Is.EqualTo(lastIdAfterNew));
        }

        [Test]
        public async Task DeleteNewestMessageRecomputesContactLastMessage()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(false);

            var accountEmail = TestData.AccountWithFolder.Email;
            var c = new Contact("C", new EmailAddress("c@ex.io"));
            await db.AddContactAsync(c, CancellationToken.None).ConfigureAwait(false);

            var t0 = DateTimeOffset.UtcNow;
            var older = CreateCleanUnreadMessageFrom(c.Email, t0.AddMinutes(-10));
            await AddMessageAsync(db, accountEmail, older, updateUnreadAndTotal: true, CancellationToken.None).ConfigureAwait(false);

            var newer = CreateCleanUnreadMessageFrom(c.Email, t0);
            await AddMessageAsync(db, accountEmail, newer, updateUnreadAndTotal: true, CancellationToken.None).ConfigureAwait(false);

            var contactBeforeDelete = await db.GetContactAsync(c.Email, CancellationToken.None).ConfigureAwait(false);
            Assert.That(contactBeforeDelete.LastMessageData, Is.Not.Null);

            var storedMessages = await db.GetMessageListAsync(accountEmail, TestData.Folder, 0, CancellationToken.None).ConfigureAwait(false);
            var newest = storedMessages.Single(x => x.Id == newer.Id);

            // Act
            await db.DeleteMessageAsync(accountEmail, TestData.Folder, newest.Id, updateUnreadAndTotal: true, CancellationToken.None).ConfigureAwait(false);
            var contactAfterDelete = await db.GetContactAsync(c.Email, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.That(contactAfterDelete.LastMessageData, Is.Not.Null);
            Assert.That(contactAfterDelete.LastMessageData.Date, Is.EqualTo(older.Date));
        }

        [Test]
        public async Task GetContactsHonorsCancellationToken()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);

            for (int i = 0; i < 50; i++)
            {
                await db.AddContactAsync(new Contact($"C{i}", new EmailAddress($"c{i}@ex.io")), CancellationToken.None).ConfigureAwait(false);
            }

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act + Assert
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await db.GetContactsAsync(
                        10,
                        lastContact: null,
                        sortOrder: ContactsSortOrder.ByName,
                        cancellationToken: cts.Token)
                    .ConfigureAwait(false));
        }

        [Test]
        public async Task GetContactsReturnsNextItemsWhenLastContactIsNotFromDb()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);

            await db.AddContactAsync(new Contact("A", new EmailAddress("a@ex.io")), CancellationToken.None).ConfigureAwait(false);
            await db.AddContactAsync(new Contact("C", new EmailAddress("c@ex.io")), CancellationToken.None).ConfigureAwait(false);

            var unknownLast = new Contact("B", new EmailAddress("b@ex.io"));

            // Act
            var page = await db.GetContactsAsync(10, unknownLast, ContactsSortOrder.ByName, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.That(page, Is.Not.Null);
            Assert.That(page.Count, Is.EqualTo(1));
            Assert.That(page[0].Email.Address, Is.EqualTo("c@ex.io"));
        }

        [Test]
        public async Task GetContactsAsyncSortedByUnreadWithPaginationReturnsNextPage()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(false);

            var accountEmail = TestData.AccountWithFolder.Email;

            var c1 = new Contact("C1", new EmailAddress("c1@example.com"));
            var c2 = new Contact("C2", new EmailAddress("c2@example.com"));
            var c3 = new Contact("C3", new EmailAddress("c3@example.com"));

            await db.AddContactAsync(c1, CancellationToken.None).ConfigureAwait(false);
            await db.AddContactAsync(c2, CancellationToken.None).ConfigureAwait(false);
            await db.AddContactAsync(c3, CancellationToken.None).ConfigureAwait(false);

            var t0 = DateTimeOffset.UtcNow;
            await AddMessageAsync(db, accountEmail, CreateCleanUnreadMessageFrom(c2.Email, t0.AddMinutes(3)), updateUnreadAndTotal: true, CancellationToken.None).ConfigureAwait(false);
            await AddMessageAsync(db, accountEmail, CreateCleanUnreadMessageFrom(c2.Email, t0.AddMinutes(4)), updateUnreadAndTotal: true, CancellationToken.None).ConfigureAwait(false);
            await AddMessageAsync(db, accountEmail, CreateCleanUnreadMessageFrom(c1.Email, t0.AddMinutes(2)), updateUnreadAndTotal: true, CancellationToken.None).ConfigureAwait(false);

            // Act
            var page1 = await db.GetContactsAsync(1, null, ContactsSortOrder.ByUnread, CancellationToken.None).ConfigureAwait(false);
            var page2 = await db.GetContactsAsync(1, page1[0], ContactsSortOrder.ByUnread, CancellationToken.None).ConfigureAwait(false);
            var page3 = await db.GetContactsAsync(1, page2[0], ContactsSortOrder.ByUnread, CancellationToken.None).ConfigureAwait(false);
            var page4 = await db.GetContactsAsync(1, page3[0], ContactsSortOrder.ByUnread, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.That(page1.Count, Is.EqualTo(1));
            Assert.That(page2.Count, Is.EqualTo(1));
            Assert.That(page3.Count, Is.EqualTo(1));
            Assert.That(page4.Count, Is.EqualTo(0));

            var emailsOrdered = new[]
            {
                page1[0].Email.Address,
                page2[0].Email.Address,
                page3[0].Email.Address
            };

            Assert.That(emailsOrdered, Is.EqualTo(new[] { c2.Email.Address, c1.Email.Address, c3.Email.Address }));
        }

        [Test]
        public async Task GetContactsSortedByUnreadChangesOrderAfterMarkingRead()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(false);

            var accountEmail = TestData.AccountWithFolder.Email;
            var unread = new Contact("Unread", new EmailAddress("u@ex.io"));
            var read = new Contact("Read", new EmailAddress("r@ex.io"));

            await db.AddContactAsync(unread, CancellationToken.None).ConfigureAwait(false);
            await db.AddContactAsync(read, CancellationToken.None).ConfigureAwait(false);

            var t0 = DateTimeOffset.UtcNow;
            var m = CreateCleanUnreadMessageFrom(unread.Email, t0);
            await db.AddMessageListAsync(accountEmail, TestData.Folder, new List<Message> { m }, updateUnreadAndTotal: true, CancellationToken.None).ConfigureAwait(false);

            var before = await db.GetContactsAsync(10, null, ContactsSortOrder.ByUnread, CancellationToken.None).ConfigureAwait(false);
            var beforeEmails = before.Select(x => x.Email.Address).ToList();

            Assert.That(beforeEmails.IndexOf(unread.Email.Address), Is.LessThan(beforeEmails.IndexOf(read.Email.Address)));

            var stored = await db.GetMessageListAsync(accountEmail, TestData.Folder, 1, CancellationToken.None).ConfigureAwait(false);
            Assert.That(stored.Count, Is.EqualTo(1));

            var msg = stored[0];
            msg.IsMarkedAsRead = true;

            // Act
            await db.UpdateMessagesFlagsAsync(accountEmail, new[] { msg }, updateUnreadAndTotal: true, CancellationToken.None).ConfigureAwait(false);
            var after = await db.GetContactsAsync(10, null, ContactsSortOrder.ByUnread, CancellationToken.None).ConfigureAwait(false);

            // Assert
            var updatedUnread = after.First(x => x.Email.Address == unread.Email.Address);
            Assert.That(updatedUnread.UnreadCount, Is.EqualTo(0));

            var updatedRead = after.First(x => x.Email.Address == read.Email.Address);
            Assert.That(updatedRead.UnreadCount, Is.EqualTo(0));

            var afterEmails = after.Select(x => x.Email.Address).ToList();
            Assert.That(afterEmails.Contains(unread.Email.Address), Is.True);
            Assert.That(afterEmails.Contains(read.Email.Address), Is.True);

            // When unread counts are equal (both 0), ordering falls back to last activity time (desc) then Id (asc).
            // 'unread' contact has a last message, 'read' contact doesn't, so 'unread' stays first.
            Assert.That(afterEmails.IndexOf(unread.Email.Address), Is.LessThan(afterEmails.IndexOf(read.Email.Address)));
        }

        [Test]
        public async Task GetAllContactsAsyncReturnsAllContacts()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);

            var contacts = new List<Contact>
            {
                new Contact("Alice", new EmailAddress("alice@example.com")),
                new Contact("Bob", new EmailAddress("bob@example.com")),
                new Contact("Charlie", new EmailAddress("charlie@example.com"))
            };

            foreach (var c in contacts)
            {
                await db.AddContactAsync(c, CancellationToken.None).ConfigureAwait(false);
            }

            // Act
            var storedContacts = await db.GetContactsAsync(CancellationToken.None).ConfigureAwait(false);
            var storedList = storedContacts.ToList();
            var storedEmails = storedList.Select(x => x.Email.Address).ToList();

            // Assert
            Assert.That(storedList.Count, Is.EqualTo(3));
            Assert.That(storedEmails, Contains.Item("alice@example.com"));
            Assert.That(storedEmails, Contains.Item("bob@example.com"));
            Assert.That(storedEmails, Contains.Item("charlie@example.com"));
        }

        [Test]
        public async Task GetContactsAsyncSortedByNameReturnsContactsInAlphabeticalOrder()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);

            var contacts = new List<Contact>
            {
                new Contact("Charlie", new EmailAddress("charlie@example.com")),
                new Contact("Alice", new EmailAddress("alice@example.com")),
                new Contact("Bob", new EmailAddress("bob@example.com"))
            };

            foreach (var c in contacts)
            {
                await db.AddContactAsync(c, CancellationToken.None).ConfigureAwait(false);
            }

            // Act
            var sortedContacts = await db.GetContactsAsync(
                    count: 10,
                    lastContact: null,
                    sortOrder: ContactsSortOrder.ByName,
                    cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);

            // Assert
            Assert.That(sortedContacts.Count, Is.EqualTo(3));
            Assert.That(sortedContacts[0].FullName, Is.EqualTo("Alice"));
            Assert.That(sortedContacts[1].FullName, Is.EqualTo("Bob"));
            Assert.That(sortedContacts[2].FullName, Is.EqualTo("Charlie"));
        }

        [Test]
        public async Task GetContactsAsyncSortedByNameWithPaginationReturnsNextPage()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);

            var contacts = new List<Contact>
            {
                new Contact("Alice", new EmailAddress("alice@example.com")),
                new Contact("Bob", new EmailAddress("bob@example.com")),
                new Contact("Charlie", new EmailAddress("charlie@example.com")),
                new Contact("Dave", new EmailAddress("dave@example.com"))
            };

            foreach (var c in contacts)
            {
                await db.AddContactAsync(c, CancellationToken.None).ConfigureAwait(false);
            }

            // Act
            var page1 = await db.GetContactsAsync(
                    count: 2,
                    lastContact: null,
                    sortOrder: ContactsSortOrder.ByName,
                    cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);

            var page2 = await db.GetContactsAsync(
                    count: 2,
                    lastContact: page1[page1.Count - 1],
                    sortOrder: ContactsSortOrder.ByName,
                    cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);

            // Assert
            Assert.That(page1.Count, Is.EqualTo(2));
            Assert.That(page1[0].FullName, Is.EqualTo("Alice"));
            Assert.That(page1[1].FullName, Is.EqualTo("Bob"));

            Assert.That(page2.Count, Is.EqualTo(2));
            Assert.That(page2[0].FullName, Is.EqualTo("Charlie"));
            Assert.That(page2[1].FullName, Is.EqualTo("Dave"));
        }

        [Test]
        public async Task GetContactsAsyncSortedByNameMixedCaseReturnsCaseInsensitiveOrder()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);

            var contacts = new List<Contact>
            {
                new Contact("bob", new EmailAddress("bob@example.com")),
                new Contact("Alice", new EmailAddress("alice@example.com")),
                new Contact("Charlie", new EmailAddress("charlie@example.com"))
            };

            foreach (var c in contacts)
            {
                await db.AddContactAsync(c, CancellationToken.None).ConfigureAwait(false);
            }

            // Act
            var sortedContacts = await db.GetContactsAsync(
                    count: 10,
                    lastContact: null,
                    sortOrder: ContactsSortOrder.ByName,
                    cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);

            // Assert
            Assert.That(sortedContacts.Count, Is.EqualTo(3));
            Assert.That(sortedContacts[0].FullName, Is.EqualTo("Alice"));
            Assert.That(sortedContacts[1].FullName, Is.EqualTo("bob"));
            Assert.That(sortedContacts[2].FullName, Is.EqualTo("Charlie"));
        }

        [Test]
        public async Task GetContactsAsyncSortedByNameEmptyNameUsesEmailForSort()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);

            var contacts = new List<Contact>
            {
                new Contact(string.Empty, new EmailAddress("b_no_name@example.com")),
                new Contact(null, new EmailAddress("d_null_name@example.com")),
                new Contact("Alice", new EmailAddress("alice@example.com")),
                new Contact("Charlie", new EmailAddress("charlie@example.com"))
            };

            foreach (var c in contacts)
            {
                await db.AddContactAsync(c, CancellationToken.None).ConfigureAwait(false);
            }

            // Act
            var sortedContacts = await db.GetContactsAsync(
                    count: 10,
                    lastContact: null,
                    sortOrder: ContactsSortOrder.ByName,
                    cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);

            // Assert
            Assert.That(sortedContacts.Count, Is.EqualTo(4));
            Assert.That(sortedContacts[0].FullName, Is.EqualTo("Alice"));

            Assert.That(string.IsNullOrEmpty(sortedContacts[1].FullName));
            Assert.That(sortedContacts[1].Email.Address, Is.EqualTo("b_no_name@example.com"));

            Assert.That(sortedContacts[2].FullName, Is.EqualTo("Charlie"));

            Assert.That(string.IsNullOrEmpty(sortedContacts[3].FullName));
            Assert.That(sortedContacts[3].Email.Address, Is.EqualTo("d_null_name@example.com"));
        }

        [Test]
        public async Task GetContactsAsyncSortedByNameSameNameUsesEmailAsTieBreaker()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);

            var contacts = new List<Contact>
            {
                new Contact("John Smith", new EmailAddress("john.b@example.com")),
                new Contact("John Smith", new EmailAddress("john.a@example.com")),
                new Contact("John Smith", new EmailAddress("john.c@example.com"))
            };

            foreach (var c in contacts)
            {
                await db.AddContactAsync(c, CancellationToken.None).ConfigureAwait(false);
            }

            // Act
            var sortedContacts = await db.GetContactsAsync(
                    count: 10,
                    lastContact: null,
                    sortOrder: ContactsSortOrder.ByName,
                    cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);

            // Assert
            Assert.That(sortedContacts.Count, Is.EqualTo(3));
            Assert.That(sortedContacts[0].Email.Address, Is.EqualTo("john.a@example.com"));
            Assert.That(sortedContacts[1].Email.Address, Is.EqualTo("john.b@example.com"));
            Assert.That(sortedContacts[2].Email.Address, Is.EqualTo("john.c@example.com"));
        }

        [Test]
        public async Task GetContactsAsyncSortedByUnreadReturnsUnreadFirst()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(false);

            var accountEmail = TestData.AccountWithFolder.Email;
            var readContact = new Contact("Read Contact", new EmailAddress("read@example.com"));
            var unreadContact = new Contact("Unread Contact", new EmailAddress("unread@example.com"));

            await db.AddContactAsync(readContact, CancellationToken.None).ConfigureAwait(false);
            await db.AddContactAsync(unreadContact, CancellationToken.None).ConfigureAwait(false);

            var m = TestData.GetNewUnreadMessage();
            m.From.Clear();
            m.From.Add(unreadContact.Email);
            await db.AddMessageListAsync(accountEmail, TestData.Folder, new List<Message> { m }, updateUnreadAndTotal: true, CancellationToken.None).ConfigureAwait(false);

            // Act
            var sortedContacts = await db.GetContactsAsync(
                    count: 10,
                    lastContact: null,
                    sortOrder: ContactsSortOrder.ByUnread,
                    cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);

            // Assert
            Assert.That(sortedContacts.Count, Is.EqualTo(2));
            Assert.That(sortedContacts[0].Email.Address, Is.EqualTo(unreadContact.Email.Address));
            Assert.That(sortedContacts[0].UnreadCount, Is.GreaterThan(0));

            Assert.That(sortedContacts[1].Email.Address, Is.EqualTo(readContact.Email.Address));
            Assert.That(sortedContacts[1].UnreadCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetContactsAsyncThrowsArgumentOutOfRangeExceptionForInvalidCount()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);

            // Act + Assert
            Assert.CatchAsync<ArgumentOutOfRangeException>(async () =>
                await db.GetContactsAsync(
                        count: 0,
                        lastContact: null,
                        sortOrder: ContactsSortOrder.ByName,
                        cancellationToken: CancellationToken.None)
                    .ConfigureAwait(false));

            Assert.CatchAsync<ArgumentOutOfRangeException>(async () =>
                await db.GetContactsAsync(
                        count: -1,
                        lastContact: null,
                        sortOrder: ContactsSortOrder.ByName,
                        cancellationToken: CancellationToken.None)
                    .ConfigureAwait(false));
        }

        [Test]
        public async Task GetContactsAsyncSortedByNamePaginationHandlesDuplicateNamesCorrectly()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);

            var c1 = new Contact("John Doe", new EmailAddress("john.a@example.com"));
            var c2 = new Contact("John Doe", new EmailAddress("john.b@example.com"));

            await db.AddContactAsync(c1, CancellationToken.None).ConfigureAwait(false);
            await db.AddContactAsync(c2, CancellationToken.None).ConfigureAwait(false);

            // Act
            var page1 = await db.GetContactsAsync(1, null, ContactsSortOrder.ByName, CancellationToken.None).ConfigureAwait(false);
            var page2 = await db.GetContactsAsync(1, page1[0], ContactsSortOrder.ByName, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.That(page1.Count, Is.EqualTo(1));
            Assert.That(page1[0].Email.Address, Is.EqualTo(c1.Email.Address));

            Assert.That(page2.Count, Is.EqualTo(1));
            Assert.That(page2[0].Email.Address, Is.EqualTo(c2.Email.Address));
        }

        [Test]
        public async Task GetContactsAsyncSortedByTimePaginationHandlesContactsWithoutMessagesCorrectly()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);

            var c1 = new Contact("A", new EmailAddress("a@ex.io"));
            var c2 = new Contact("B", new EmailAddress("b@ex.io"));

            await db.AddContactAsync(c1, CancellationToken.None).ConfigureAwait(false);
            await db.AddContactAsync(c2, CancellationToken.None).ConfigureAwait(false);

            // Act
            var page1 = await db.GetContactsAsync(1, null, ContactsSortOrder.ByTime, CancellationToken.None).ConfigureAwait(false);
            var page2 = await db.GetContactsAsync(1, page1[0], ContactsSortOrder.ByTime, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.That(page1.Count, Is.EqualTo(1));
            Assert.That(page1[0].Email.Address, Is.EqualTo(c1.Email.Address));

            Assert.That(page2.Count, Is.EqualTo(1));
            Assert.That(page2[0].Email.Address, Is.EqualTo(c2.Email.Address));
        }

        [Test]
        public async Task GetContactsAsyncSortedByUnreadPaginationTransitionsFromUnreadToReadCorrectly()
        {
            // Arrange
            using var db = await OpenDataStorageAsync().ConfigureAwait(false);
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(false);

            var accountEmail = TestData.AccountWithFolder.Email;
            var unreadContact = new Contact("Unread", new EmailAddress("unread@ex.io"));
            var readContact = new Contact("Read", new EmailAddress("read@ex.io"));

            await db.AddContactAsync(unreadContact, CancellationToken.None).ConfigureAwait(false);
            await db.AddContactAsync(readContact, CancellationToken.None).ConfigureAwait(false);

            var m = TestData.GetNewUnreadMessage();
            m.From.Clear();
            m.From.Add(unreadContact.Email);
            await db.AddMessageListAsync(accountEmail, TestData.Folder, new List<Message> { m }, updateUnreadAndTotal: true, CancellationToken.None).ConfigureAwait(false);

            // Act
            var page1 = await db.GetContactsAsync(1, null, ContactsSortOrder.ByUnread, CancellationToken.None).ConfigureAwait(false);
            var page2 = await db.GetContactsAsync(1, page1[0], ContactsSortOrder.ByUnread, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.That(page1.Count, Is.EqualTo(1));
            Assert.That(page1[0].Email.Address, Is.EqualTo(unreadContact.Email.Address));
            Assert.That(page1[0].UnreadCount, Is.GreaterThan(0));

            Assert.That(page2.Count, Is.EqualTo(1));
            Assert.That(page2[0].Email.Address, Is.EqualTo(readContact.Email.Address));
            Assert.That(page2[0].UnreadCount, Is.EqualTo(0));
        }
    }
}
