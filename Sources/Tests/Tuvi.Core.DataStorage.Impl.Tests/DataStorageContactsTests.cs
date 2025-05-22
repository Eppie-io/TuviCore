using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Entities;

namespace Tuvi.Core.DataStorage.Tests
{
    public class ContactsTests : TestWithStorageBase
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
        public async Task AddContactToDataStorage()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            Assert.That(await db.ExistsContactWithEmailAddressAsync(TestData.Contact.Email, default).ConfigureAwait(true), Is.False);

            await db.AddContactAsync(TestData.Contact, default).ConfigureAwait(true);
            var isAdded = await db.ExistsContactWithEmailAddressAsync(TestData.Contact.Email, default).ConfigureAwait(true);

            Assert.That(isAdded, Is.True);
            var contact = await db.GetContactAsync(TestData.Contact.Email, default).ConfigureAwait(true);
            Assert.That(ContactsAreEqual(contact, TestData.Contact), Is.True);
            Assert.That(contact.Email, Is.EqualTo(TestData.Contact.Email));
        }

        [Test]
        public async Task AddContactWithoutEmailToDataStorage()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            var contact = new Contact()
            {
                FullName = "Contact Name"
            };

            Assert.CatchAsync<DataBaseException>(async () => await db.AddContactAsync(contact, default).ConfigureAwait(true), "no email");
            contact.Email = new EmailAddress("contact@address1.io");
            Assert.DoesNotThrowAsync(async () => await db.AddContactAsync(contact, default).ConfigureAwait(true));
            Assert.That(await db.ExistsContactWithEmailAddressAsync(contact.Email, default).ConfigureAwait(true), Is.True);
        }

        [Test]
        public async Task AddContactWithAvatarToDataStorage()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            Assert.That(await db.ExistsContactWithEmailAddressAsync(TestData.ContactWithAvatar.Email, default).ConfigureAwait(true), Is.False);

            await db.AddContactAsync(TestData.ContactWithAvatar, default).ConfigureAwait(true);
            var isAdded = await db.ExistsContactWithEmailAddressAsync(TestData.ContactWithAvatar.Email, default).ConfigureAwait(true);
            Assert.That(isAdded, Is.True);

            var contact = await db.GetContactAsync(TestData.ContactWithAvatar.Email, default).ConfigureAwait(true);
            Assert.That(ContactsAreEqual(contact, TestData.ContactWithAvatar), Is.True);
        }

        private static bool ContactsAreEqual(Contact c1, Contact c2)
        {
            if (c1.Email == c2.Email &&
                c1.FullName == c2.FullName &&
                ImageInfosAreEqual(c1.AvatarInfo, c2.AvatarInfo))
            {
                return true;
            }

            return false;
        }

        private static bool ImageInfosAreEqual(ImageInfo i1, ImageInfo i2)
        {
            if (i1.Width == i2.Width && i1.Height == i2.Height)
            {
                if ((i1.Bytes == null && i2.Bytes == null) ||
                    i1.Bytes != null && i2.Bytes != null && i1.Bytes.SequenceEqual(i2.Bytes))
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public async Task SetContactAvatar()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            Assert.That(await db.ExistsContactWithEmailAddressAsync(TestData.Contact.Email, default).ConfigureAwait(true), Is.False);

            await db.AddContactAsync(TestData.Contact, default).ConfigureAwait(true);
            var isAdded = await db.ExistsContactWithEmailAddressAsync(TestData.Contact.Email, default).ConfigureAwait(true);
            Assert.That(isAdded, Is.True);

            Assert.CatchAsync<DataBaseException>(async () => await db.AddContactAsync(TestData.ContactWithAvatar, default).ConfigureAwait(true), "Duplicate contacts are prohibited");

            await db.SetContactAvatarAsync(TestData.Contact.Email, TestData.ContactAvatar.Bytes, TestData.ContactAvatar.Width, TestData.ContactAvatar.Height, default).ConfigureAwait(true);
            var contact = await db.GetContactAsync(TestData.ContactWithAvatar.Email, default).ConfigureAwait(true);

            Assert.That(ImageInfosAreEqual(contact.AvatarInfo, TestData.ContactAvatar), Is.True);
        }

        [Test]
        public async Task RemoveContactAvatar()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            await db.AddContactAsync(TestData.ContactWithAvatar, default).ConfigureAwait(true);
            var isAdded = await db.ExistsContactWithEmailAddressAsync(TestData.ContactWithAvatar.Email, default).ConfigureAwait(true);
            Assert.That(isAdded, Is.True);

            await db.RemoveContactAvatarAsync(TestData.ContactWithAvatar.Email, default).ConfigureAwait(true);

            var contact = await db.GetContactAsync(TestData.ContactWithAvatar.Email, default).ConfigureAwait(true);

            Assert.That(contact.AvatarInfo.IsEmpty, Is.True);
        }

        [Test]
        public async Task UpdateContact()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            await db.AddContactAsync(TestData.Contact, default).ConfigureAwait(true);
            var isAdded = await db.ExistsContactWithEmailAddressAsync(TestData.Contact.Email, default).ConfigureAwait(true);
            Assert.That(isAdded, Is.True);

            var contact = await db.GetContactAsync(TestData.Contact.Email, default).ConfigureAwait(true);
            contact.FullName = "Updated name";
            await db.UpdateContactAsync(contact, default).ConfigureAwait(true);

            var updatedContact = await db.GetContactAsync(TestData.Contact.Email, default).ConfigureAwait(true);

            Assert.That(ContactsAreEqual(contact, updatedContact), Is.True);
        }

        [Test]
        public async Task ChangeContactAvatar()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            await db.AddContactAsync(TestData.ContactWithAvatar, default).ConfigureAwait(true);
            var isAdded = await db.ExistsContactWithEmailAddressAsync(TestData.ContactWithAvatar.Email, default).ConfigureAwait(true);
            Assert.That(isAdded, Is.True);

            var updatedAvatarInfo = new ImageInfo(360, 360, new byte[] { 25, 182, 137, 59, 46, 78, 69, 214 });
            await db.SetContactAvatarAsync(TestData.ContactWithAvatar.Email, updatedAvatarInfo.Bytes, updatedAvatarInfo.Width, updatedAvatarInfo.Height, default).ConfigureAwait(true);
            var contact = await db.GetContactAsync(TestData.ContactWithAvatar.Email, default).ConfigureAwait(true);

            Assert.That(ImageInfosAreEqual(contact.AvatarInfo, updatedAvatarInfo), Is.True);
        }

        [Test]
        public async Task RemoveContactByEmail()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            await db.AddContactAsync(TestData.Contact, default).ConfigureAwait(true);

            await db.RemoveContactAsync(TestData.Contact.Email, default).ConfigureAwait(true);
            Assert.That(await db.ExistsContactWithEmailAddressAsync(TestData.Contact.Email, default).ConfigureAwait(true), Is.False);
        }

        [Test]
        public async Task CheckLastMessageData()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            await db.AddContactAsync(TestData.Contact, default).ConfigureAwait(true);

            var contact = await db.GetContactAsync(TestData.Contact.Email, default).ConfigureAwait(true);
            Assert.That(contact.LastMessageData, Is.Null);
            Assert.That(contact.LastMessageDataId == 0, Is.True);

            await db.AddAccountAsync(TestData.Account).ConfigureAwait(true);
            contact.LastMessageData = new LastMessageData(1, TestData.Account.Email, TestData.Message.Id, System.DateTimeOffset.Now);

            await db.UpdateContactAsync(contact, default).ConfigureAwait(true);

            contact = await db.GetContactAsync(TestData.Contact.Email, default).ConfigureAwait(true);
            Assert.That(contact.LastMessageData, Is.Not.Null);
            Assert.That(contact.LastMessageData.MessageId == TestData.Message.Id, Is.True);
            Assert.That(contact.LastMessageData.Date > System.DateTimeOffset.MinValue, Is.True);
            Assert.That(contact.LastMessageData.AccountEmail, Is.EqualTo(TestData.Account.Email));
        }

        [Test]
        public async Task TryAddContactAsync()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);

            Assert.That(await db.TryAddContactAsync(TestData.Contact, default).ConfigureAwait(true), Is.True);
            Assert.That(await db.TryAddContactAsync(TestData.Contact, default).ConfigureAwait(true), Is.False);
        }

        [Test]
        public async Task GetUnknownContactShouldThrowException()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            await db.AddContactAsync(TestData.Contact, default).ConfigureAwait(true);

            Assert.CatchAsync<DataBaseException>(async () => await db.GetContactAsync(new EmailAddress("unknown@mail.box"), default).ConfigureAwait(true));
        }

        [Test]
        public async Task AddUnreadMessagesShouldShouldIncrementContactUnreadCount()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(true);
            var accountEmail = TestData.AccountWithFolder.Email;
            await db.AddContactAsync(TestData.Contact, default).ConfigureAwait(true);
            var message = TestData.GetNewUnreadMessage();
            message.From.Add(TestData.Contact.Email);

            await db.AddMessageListAsync(accountEmail, TestData.Folder, new List<Message>() { message }, updateUnreadAndTotal: false).ConfigureAwait(true);


            int count = await db.GetContactUnreadMessagesCountAsync(TestData.Contact.Email, default).ConfigureAwait(true);
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public async Task AddUnreadMessagesShouldShouldIncrementSeveralContactUnreadCount()
        {
            using var db = await OpenDataStorageAsync().ConfigureAwait(true);
            await db.AddAccountAsync(TestData.AccountWithFolder).ConfigureAwait(true);
            var accountEmail = TestData.AccountWithFolder.Email;
            await db.AddContactAsync(new Contact("To", TestData.To), default).ConfigureAwait(true);
            await db.AddContactAsync(new Contact("Cc", TestData.Cc), default).ConfigureAwait(true);
            await db.AddContactAsync(new Contact("Bcc", TestData.Bcc), default).ConfigureAwait(true);

            var message = TestData.GetNewUnreadMessage();
            message.From.Clear();
            message.To.Clear();
            message.From.Add(accountEmail);
            message.To.Add(TestData.To);
            message.Cc.Add(TestData.Cc);
            message.Bcc.Add(TestData.Bcc);

            await db.AddMessageListAsync(accountEmail, TestData.Folder, new List<Message>() { message }, updateUnreadAndTotal: false).ConfigureAwait(true);

            var counts = (await db.GetUnreadMessagesCountByContactAsync(default).ConfigureAwait(true)).OrderBy(x => x.Key).ToList();
            Assert.That(counts.Count, Is.EqualTo(3));
            Assert.That(counts[0].Value, Is.EqualTo(1));
            Assert.That(counts[1].Value, Is.EqualTo(1));
            Assert.That(counts[2].Value, Is.EqualTo(1));

            Assert.That(counts[0].Key, Is.EqualTo(TestData.Bcc));
            Assert.That(counts[1].Key, Is.EqualTo(TestData.Cc));
            Assert.That(counts[2].Key, Is.EqualTo(TestData.To));
        }
    }
}
