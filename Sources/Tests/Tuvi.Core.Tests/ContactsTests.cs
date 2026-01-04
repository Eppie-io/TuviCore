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
using Tuvi.Core.Impl;

namespace Tuvi.Core.Tests
{
    public class ContactsTests : CoreTestBase
    {
        [SetUp]
        public async Task SetupAsync()
        {
            DeleteStorage();

            await CreateDataStorageAsync().ConfigureAwait(true);
        }

        [TearDown]
        public void TearDown()
        {
            DeleteStorage();
        }

        class ContactEventsCounter
        {
            public List<Contact> AddedContacts { get; }
            public List<Contact> ChangedContacts { get; }
            public List<EmailAddress> DeletedContacts { get; }

            public ContactEventsCounter(TuviMail core)
            {
                AddedContacts = new List<Contact>();
                ChangedContacts = new List<Contact>();
                DeletedContacts = new List<EmailAddress>();
                core.ContactAdded += Core_ContactAdded;
                core.ContactChanged += Core_ContactChanged;
                core.ContactDeleted += Core_ContactDeleted;
            }

            private void Core_ContactAdded(object sender, ContactAddedEventArgs e)
            {
                AddedContacts.Add(e.Contact);
            }

            private void Core_ContactChanged(object sender, ContactChangedEventArgs e)
            {
                ChangedContacts.Add(e.Contact);
            }

            private void Core_ContactDeleted(object sender, ContactDeletedEventArgs e)
            {
                DeletedContacts.Add(e.ContactEmail);
            }
        }

        [Test]
        public async Task AddMessageShouldEmitContactAddedEvent()
        {
            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);

            var account = GetTestEmailAccount();
            // TODO: we add directly to the storage for the moment, it shpuld be replaced to core
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var contactEventsCounter = new ContactEventsCounter(core);

            var message = CreateMessage(0);
            message.From.Add(new EmailAddress("from@mail.box"));
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               new List<Message>() { message },
                                                               default).ConfigureAwait(true);

            Assert.That(contactEventsCounter.AddedContacts.Count, Is.EqualTo(1));
            Assert.That(contactEventsCounter.ChangedContacts.Count, Is.EqualTo(0));
            Assert.That(contactEventsCounter.DeletedContacts.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task AddMessageFromSpamAndTrashShouldBeIgnored()
        {
            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);

            var account = GetTestEmailAccount();
            // adde ignored folders
            account.FoldersStructure.Add(new Folder("Spam", FolderAttributes.Junk));
            account.FoldersStructure.Add(new Folder("Trash", FolderAttributes.Trash));
            account.FoldersStructure.Add(new Folder("All", FolderAttributes.All));
            account.FoldersStructure.Add(new Folder("Important", FolderAttributes.Important));
            // TODO: we add directly to the storage for the moment, it shpuld be replaced to core
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var contactEventsCounter = new ContactEventsCounter(core);

            var message1 = CreateMessage(1);
            var message2 = CreateMessage(2);
            var message3 = CreateMessage(3);
            var message4 = CreateMessage(4);
            message1.From.Add(new EmailAddress("from@mail.box"));
            message2.From.Add(new EmailAddress("from@mail.box"));
            message3.From.Add(new EmailAddress("from@mail.box"));
            message4.From.Add(new EmailAddress("from@mail.box"));
            await accountService.AddMessagesToDataStorageAsync(account.FoldersStructure[2],
                                                               new List<Message>() { message1 },
                                                               default).ConfigureAwait(true);

            await accountService.AddMessagesToDataStorageAsync(account.FoldersStructure[3],
                                                               new List<Message>() { message2 },
                                                               default).ConfigureAwait(true);
            await accountService.AddMessagesToDataStorageAsync(account.FoldersStructure[4],
                                                               new List<Message>() { message3 },
                                                               default).ConfigureAwait(true);
            await accountService.AddMessagesToDataStorageAsync(account.FoldersStructure[5],
                                                               new List<Message>() { message4 },
                                                               default).ConfigureAwait(true);
            Assert.That(contactEventsCounter.AddedContacts.Count, Is.EqualTo(0));
            Assert.That(contactEventsCounter.ChangedContacts.Count, Is.EqualTo(0));
            Assert.That(contactEventsCounter.DeletedContacts.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task DeleteMessageFromSpamAndTrashShouldBeIgnored()
        {
            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);

            var account = GetTestEmailAccount();
            // adde ignored folders
            account.FoldersStructure.Add(new Folder("Spam", FolderAttributes.Junk));
            account.FoldersStructure.Add(new Folder("Trash", FolderAttributes.Trash));
            // TODO: we add directly to the storage for the moment, it shpuld be replaced to core
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var contactEventsCounter = new ContactEventsCounter(core);

            var message1 = CreateMessage(1);
            var message2 = CreateMessage(2);
            var message3 = CreateMessage(3);
            var from = new EmailAddress("from@mail.box");
            message1.From.Add(from);
            message2.From.Add(from);
            message3.From.Add(from);
            await accountService.AddMessagesToDataStorageAsync(account.FoldersStructure[2],
                                                               new List<Message>() { message1 },
                                                               default).ConfigureAwait(true);

            await accountService.AddMessagesToDataStorageAsync(account.FoldersStructure[3],
                                                               new List<Message>() { message2 },
                                                               default).ConfigureAwait(true);

            // shoud add a contact with 1 unread
            await accountService.AddMessagesToDataStorageAsync(account.FoldersStructure[0],
                                                               new List<Message>() { message3 },
                                                               default).ConfigureAwait(true);

            var contact = await dataStorage.GetContactAsync(from, default).ConfigureAwait(true);
            Assert.That(contact, Is.Not.Null);
            Assert.That(contact.UnreadCount, Is.EqualTo(1));

            await core.DeleteMessagesAsync(new List<Message>() { message1, message2 },
                                           default).ConfigureAwait(true);

            contact = await dataStorage.GetContactAsync(from, default).ConfigureAwait(true);
            Assert.That(contact.UnreadCount, Is.EqualTo(1));

            Assert.That(contactEventsCounter.AddedContacts.Count, Is.EqualTo(1));
            Assert.That(contactEventsCounter.ChangedContacts.Count, Is.EqualTo(0));
            Assert.That(contactEventsCounter.DeletedContacts.Count, Is.EqualTo(0));

        }

        [Test]
        public async Task AddMessagesWithSameOriginShouldEmitOneContactAddedEvent()
        {
            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);

            var account = GetTestEmailAccount();
            // TODO: we add directly to the storage for the moment, it shpuld be replaced to core
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var contactEventsCounter = new ContactEventsCounter(core);

            var from = new EmailAddress("from@mail.box");
            var messages = new List<Message>()
            {
                CreateMessage(0),
                CreateMessage(2)
            };
            messages[0].From.Add(from);
            messages[1].From.Add(from);
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);

            Assert.That(contactEventsCounter.AddedContacts.Count, Is.EqualTo(1));
            Assert.That(contactEventsCounter.ChangedContacts.Count, Is.EqualTo(1)); // the second message updates last message 
            Assert.That(contactEventsCounter.DeletedContacts.Count, Is.EqualTo(0));
            Assert.That(contactEventsCounter.AddedContacts[0].Email, Is.EqualTo(from));
        }

        [Test]
        public async Task AddMessagesWithDifferentOriginShouldAddTwoContacts()
        {
            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);

            var account = GetTestEmailAccount();
            // TODO: we add directly to the storage for the moment, it shpuld be replaced to core
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var contactEventsCounter = new ContactEventsCounter(core);

            var from = new EmailAddress("from@mail.box");
            var from2 = new EmailAddress("from2@mail.box");
            var messages = new List<Message>()
            {
                CreateMessage(0),
                CreateMessage(2)
            };
            messages[0].From.Add(from);
            messages[1].From.Add(from2); ;
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);

            Assert.That(contactEventsCounter.AddedContacts.Count, Is.EqualTo(2));
            Assert.That(contactEventsCounter.ChangedContacts.Count, Is.EqualTo(0));
            Assert.That(contactEventsCounter.DeletedContacts.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task AddNewerMessagesShouldUpdateContact()
        {
            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);

            var account = GetTestEmailAccount();
            // TODO: we add directly to the storage for the moment, it shpuld be replaced to core
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var contactEventsCounter = new ContactEventsCounter(core);

            var from = new EmailAddress("from@mail.box");
            var messages = new List<Message>()
            {
                CreateMessage(0)
            };
            messages[0].From.Add(from);
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);

            messages[0].Id = 3;
            messages[0].Date = messages[0].Date.AddMinutes(3);
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);
            Assert.That(contactEventsCounter.AddedContacts.Count, Is.EqualTo(1));
            Assert.That(contactEventsCounter.ChangedContacts.Count, Is.EqualTo(1));
            Assert.That(contactEventsCounter.DeletedContacts.Count, Is.EqualTo(0));
            Assert.That(contactEventsCounter.ChangedContacts[0].LastMessageData.Date, Is.EqualTo(messages[0].Date));
            Assert.That(contactEventsCounter.ChangedContacts[0].LastMessageData.MessageId, Is.EqualTo(3));
        }

        [Test]
        public async Task RemoveMessagesShouldUpdateContactLatestMessage()
        {
            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);

            var account = GetTestEmailAccount();
            // TODO: we add directly to the storage for the moment, it shpuld be replaced to core
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);


            var from = new EmailAddress("from@mail.box");
            var message1 = CreateMessage(1);
            message1.From.Add(from);
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               new List<Message>() { message1 },
                                                               default).ConfigureAwait(true);

            var message3 = CreateMessage(3);
            message3.From.Add(from);
            message3.Date = message1.Date.AddMinutes(3);
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               new List<Message>() { message3 },
                                                               default).ConfigureAwait(true);

            var contactEventsCounter = new ContactEventsCounter(core);
            await accountService.DeleteMessageAsync(message3).ConfigureAwait(true);
            Assert.That(contactEventsCounter.AddedContacts.Count, Is.EqualTo(0));
            Assert.That(contactEventsCounter.ChangedContacts.Count, Is.EqualTo(1));
            Assert.That(contactEventsCounter.DeletedContacts.Count, Is.EqualTo(0));
            Assert.That(contactEventsCounter.ChangedContacts[0].LastMessageData.Date, Is.EqualTo(message1.Date));
            Assert.That(contactEventsCounter.ChangedContacts[0].LastMessageData.MessageId, Is.EqualTo(1));
        }



        [Test]
        public async Task AddNewerMessagesShouldNotUpdateContact()
        {
            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);

            var account = GetTestEmailAccount();
            // TODO: we add directly to the storage for the moment, it shpuld be replaced to core
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var contactEventsCounter = new ContactEventsCounter(core);

            var from = new EmailAddress("from@mail.box");
            var messages = new List<Message>()
            {
                CreateMessage(0)
            };
            messages[0].From.Add(from);
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);

            messages[0].Id = 3;
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);
            Assert.That(contactEventsCounter.AddedContacts.Count, Is.EqualTo(1));
            Assert.That(contactEventsCounter.ChangedContacts.Count, Is.EqualTo(0));
            Assert.That(contactEventsCounter.DeletedContacts.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task RemoveContactShouldEmitDeletedEvent()
        {
            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);

            var account = GetTestEmailAccount();
            // TODO: we add directly to the storage for the moment, it should be replaced to core
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var contactEventsCounter = new ContactEventsCounter(core);

            var from = new EmailAddress("from@mail.box");
            var messages = new List<Message>()
            {
                CreateMessage(0),
            };
            messages[0].From.Add(from);
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);
            await core.RemoveContactAsync(from).ConfigureAwait(true);

            Assert.That(contactEventsCounter.AddedContacts.Count, Is.EqualTo(1));
            Assert.That(contactEventsCounter.ChangedContacts.Count, Is.EqualTo(0));
            Assert.That(contactEventsCounter.DeletedContacts.Count, Is.EqualTo(1));
            Assert.That(contactEventsCounter.DeletedContacts[0], Is.EqualTo(from));
        }

        [Test]
        public async Task ContactShouldEmitDeletedEvent()
        {
            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);

            var account = GetTestEmailAccount();
            // TODO: we add directly to the storage for the moment, it shpuld be replaced to core
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            var contactEventsCounter = new ContactEventsCounter(core);

            var from = new EmailAddress("from@mail.box");
            var messages = new List<Message>()
            {
                CreateMessage(0),
            };
            messages[0].From.Add(from);
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder,
                                                               messages,
                                                               default).ConfigureAwait(true);
            await core.RemoveContactAsync(from).ConfigureAwait(true);

            Assert.That(contactEventsCounter.AddedContacts.Count, Is.EqualTo(1));
            Assert.That(contactEventsCounter.ChangedContacts.Count, Is.EqualTo(0));
            Assert.That(contactEventsCounter.DeletedContacts.Count, Is.EqualTo(1));
            Assert.That(contactEventsCounter.DeletedContacts[0], Is.EqualTo(from));
        }

        [Test]
        public async Task SetContactAvatarShouldNotFail()
        {
            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var account = GetTestEmailAccount();
            // TODO: we add directly to the storage for the moment, it shpuld be replaced to core
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            var contact = new Contact("contact1", new EmailAddress("contact1@mail.box"));
            await dataStorage.TryAddContactAsync(contact, default).ConfigureAwait(true);

            var avatar = new byte[213];
            int v = 1;
            for (int i = 0; i < avatar.Length; ++i)
            {
                v = (v * i) / avatar.Length;
                avatar[i] = (byte)v;
            }

            Assert.DoesNotThrowAsync(async () => await core.SetContactAvatarAsync(contact.Email, avatar, avatarWidth: 16, avatarHeight: 16).ConfigureAwait(true));

            var contacts = (await core.GetContactsAsync().ConfigureAwait(true)).ToList();

            Assert.That(contacts.Count, Is.EqualTo(1));
            Assert.That(contacts[0].Email, Is.EqualTo(contact.Email));
            Assert.That(contacts[0].AvatarInfo, Is.Not.Null);
            Assert.That(contacts[0].AvatarInfo.Width, Is.EqualTo(16));
            Assert.That(contacts[0].AvatarInfo.Height, Is.EqualTo(16));
            Assert.That(contacts[0].AvatarInfo.Bytes.SequenceEqual(avatar), Is.True);
        }


        [Test]
        public async Task CoreAccountContactUnreadMessagesTest()
        {
            const int ContactCount = 10;
            const int MessageCount = 10;
            var account = GetTestEmailAccount();
            var contacts = GenerateTestContacts(ContactCount);
            var messages = GenerateTestMessages(account, ContactCount, MessageCount, contacts);

            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);

            // TODO: we add directly to the storage for the moment, it shpuld be replaced to core
            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);

            var accountService = await core.GetAccountServiceAsync(account.Email, default).ConfigureAwait(true);
            await accountService.AddMessagesToDataStorageAsync(account.DefaultInboxFolder, messages, default).ConfigureAwait(true);

            var counts = await core.GetUnreadMessagesCountByContactAsync().ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(counts.Select(x => x.Value).Sum()));
        }

        private static List<Message> GenerateTestMessages(Account account, int ContactCount, int MessageCount, List<EmailAddress> contacts)
        {
            var messages = new List<Message>(ContactCount * MessageCount);
            uint messageId = 0;
            foreach (var c in contacts)
            {
                for (int i = 0; i < MessageCount; ++i)
                {
                    var message = new Message();
                    for (int j = 0; j < 20; ++j)
                    {
                        var attachment = new Attachment();
                        attachment.Data = new byte[16];
                        attachment.FileName = $"attachmennt_{i}_{j}.doc";
                        message.Attachments.Add(attachment);
                    }

                    message.Subject = $"Subject {i} from {c.Name}";
                    message.Date = DateTime.Now;
                    message.Folder = new Folder("INBOX", FolderAttributes.Inbox);
                    message.From.Add(c);
                    message.To.Add(account.Email);
                    message.Id = messageId++;

                    messages.Add(message);
                }
            }

            return messages;
        }

        private static List<EmailAddress> GenerateTestContacts(int ContactCount)
        {
            var contacts = new List<EmailAddress>();
            for (int i = 0; i < ContactCount; ++i)
            {
                var emailAddress = new EmailAddress($"Contact{i}", $"contact{i}@test.test");
                contacts.Add(emailAddress);
            }

            return contacts;
        }

        [Test]
        public async Task SetContactNameAsyncShouldUpdateContactNameAndEmitChangedEvent()
        {
            var contactEmail = new EmailAddress("contact1@mail.box", "Old Name");
            var contact = new Contact("Old Name", contactEmail);
            var account = GetTestEmailAccount();
            const string newName = "New Name";

            var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            var contactEventsCounter = new ContactEventsCounter(core);

            await dataStorage.AddAccountAsync(account).ConfigureAwait(true);
            await dataStorage.TryAddContactAsync(contact, default).ConfigureAwait(true);
            await core.SetContactNameAsync(contactEmail, newName).ConfigureAwait(true);

            var updatedContact = await dataStorage.GetContactAsync(contactEmail, default).ConfigureAwait(true);

            Assert.That(updatedContact.FullName, Is.EqualTo(newName));
            Assert.That(updatedContact.Email.Name, Is.EqualTo(newName));
            Assert.That(contactEventsCounter.ChangedContacts.Count, Is.EqualTo(1));
            Assert.That(contactEventsCounter.ChangedContacts[0].FullName, Is.EqualTo(newName));
        }
    }
}
