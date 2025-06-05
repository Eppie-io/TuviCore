using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.DataStorage;
using Tuvi.Core.DataStorage.Impl;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Tests
{
    public class MessagesTests
    {        
        IDataStorage _storage;
        Account _account1;
        Account _account2;
        readonly EmailAddress _contactAddress1 = new("contact1@test.mail");
        readonly EmailAddress _contactAddress2 = new("contact2@test.mail", "Contact2");

        public static Message CreateNewMessage(EmailAddress from, EmailAddress to, string folder, uint id)
        {
            return CreateNewMessage(DateTimeOffset.Now, from, to, folder, id);
        }

        public static Message CreateNewMessage(DateTimeOffset date, EmailAddress from, EmailAddress to, string folder, uint id)
        {
            var message = new Message();
            message.From.Add(from);
            message.To.Add(to);
            message.Subject = "Messsage number " + id.ToString(CultureInfo.InvariantCulture);
            message.TextBody = "Hello world message body";
            message.Id = id;
            if (string.Equals(folder, "INBOX", StringComparison.OrdinalIgnoreCase))
            {
                message.Folder = new Folder(folder, FolderAttributes.Inbox);
            }
            else if (string.Equals(folder, "SENT", StringComparison.OrdinalIgnoreCase))
            {
                message.Folder = new Folder(folder, FolderAttributes.Sent);
            }
            else
            {
                message.Folder = new Folder(folder, FolderAttributes.None);
            }            
            message.Date = date;

            return message;
        }

        static readonly string _dbPath = Path.Combine(Environment.CurrentDirectory, nameof(MessagesTests) + ".db");
        [OneTimeSetUp]
        public async Task OneTimeSetup()
        {
            File.Delete(_dbPath);

            var storage = DataStorageProvider.GetDataStorage(_dbPath);
            await storage.CreateAsync("123").ConfigureAwait(true);
            _storage = storage;

            _account1 = Account.Default;
            _account1.Email = new EmailAddress("Test@test.test");
            _account1.IncomingServerAddress = "localhost";
            _account1.OutgoingServerAddress = "localhost";
            _account1.IncomingServerPort = 143;
            _account1.OutgoingServerPort = 143;
            _account1.AuthData = new BasicAuthData() { Password = "Pass123" };
            _account1.FoldersStructure.Add(new Folder("INBOX", FolderAttributes.Inbox));
            _account1.FoldersStructure.Add(new Folder("Folder1", FolderAttributes.None));
            _account1.FoldersStructure.Add(new Folder("Folder2", FolderAttributes.None));
            _account1.DefaultInboxFolder = _account1.FoldersStructure[0];

            uint id1 = 1;
            uint id2 = 1;

            await storage.AddAccountAsync(_account1).ConfigureAwait(true);

            await storage.AddMessageListAsync(_account1.Email, "INBOX", new List<Message>()
            {
                CreateNewMessage(_contactAddress1, _account1.Email, "INBOX", id1++),
                CreateNewMessage(_contactAddress1, _account1.Email, "INBOX", id1++),
                CreateNewMessage(_contactAddress2, _account1.Email, "INBOX", id1++),
                CreateNewMessage(_contactAddress1, _account1.Email, "INBOX", id1++)
            }).ConfigureAwait(true);
            // TODO: We need to do something with this `folder` parameter
            await storage.AddMessageListAsync(_account1.Email, "Folder1", new List<Message>()
            {
                CreateNewMessage(_contactAddress2, _account1.Email, "Folder1", id1++),
                CreateNewMessage(_contactAddress1, _account1.Email, "Folder1", id1++),
                CreateNewMessage(_contactAddress1, _account1.Email, "Folder1", id1++)
            }).ConfigureAwait(true);
            await storage.AddMessageListAsync(_account1.Email, "Folder2", new List<Message>()
            {
                CreateNewMessage(_contactAddress2, _account1.Email, "Folder2", id1++),

            }).ConfigureAwait(true);

            _account2 = Account.Default;
            _account2.Email = new EmailAddress("Test2@test.test");
            _account2.IncomingServerAddress = "localhost";
            _account2.OutgoingServerAddress = "localhost";
            _account2.IncomingServerPort = 143;
            _account2.OutgoingServerPort = 143;
            _account2.AuthData = new BasicAuthData() { Password = "Pass123" };
            _account2.FoldersStructure.Add(new Folder("INBOX", FolderAttributes.Inbox));
            _account2.FoldersStructure.Add(new Folder("Folder1", FolderAttributes.None));
            _account2.DefaultInboxFolder = _account2.FoldersStructure[0];

            await storage.AddAccountAsync(_account2).ConfigureAwait(true);

            await storage.AddMessageListAsync(_account2.Email, "INBOX", new List<Message>()
            {
                CreateNewMessage(_contactAddress1, _account2.Email, "INBOX", id2++),
                CreateNewMessage(_contactAddress1, _account2.Email, "INBOX", id2++),
                CreateNewMessage(_contactAddress2, _account2.Email, "INBOX", id2++)
            }).ConfigureAwait(true);

            await storage.AddMessageListAsync(_account2.Email, "Folder1", new List<Message>()
            {
                CreateNewMessage(_contactAddress2, _account2.Email, "Folder1", id2++),
                CreateNewMessage(_contactAddress1, _account2.Email, "Folder1", id2++),
                CreateNewMessage(_contactAddress2, _account2.Email, "Folder1", id2++),
                CreateNewMessage(_contactAddress1, _account2.Email, "Folder1", id2++)
            }).ConfigureAwait(true);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _storage.Dispose();
            File.Delete(_dbPath);
        }       

        [Test]
        public async Task GetAllEarlierMessagesTest()
        {
            var inboxes = new List<Folder>() { _account1.FoldersStructure[0], _account2.FoldersStructure[0] };
            var messages = await _storage.GetEarlierMessagesInFoldersAsync(inboxes, 0, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(0));
            var allMessages = await _storage.GetEarlierMessagesInFoldersAsync(inboxes, 100, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(allMessages.Count, Is.EqualTo(7));

            EnsureTimestampOrder(allMessages);
            Assert.That(allMessages.All(x => x.Folder.IsInbox), Is.True);

            var subrange = await _storage.GetEarlierMessagesInFoldersAsync(inboxes, 3, allMessages[3], CancellationToken.None).ConfigureAwait(true);
            Assert.That(subrange.Count, Is.EqualTo(3));
            EnsureSubrange(allMessages, subrange, 4);
        }

        private static void EnsureSubrange(IReadOnlyList<Message> allMessages, IReadOnlyList<Message> subrange, int start)
        {
            for (int i = 0; i < subrange.Count; ++i)
            {
                Assert.That(subrange[i].Pk == allMessages[i + start].Pk);
            }
        }

        private static void EnsureTimestampOrder(IReadOnlyList<Message> allMessages)
        {
            DateTimeOffset? prev = null;
            foreach (var message in allMessages)
            {
                if (prev == null)
                {
                    prev = message.Date;
                }
                Assert.That(message.Date, Is.LessThanOrEqualTo(prev.Value));
                prev = message.Date;
            }
        }

        [Test]
        public async Task GetContactEarlierMessagesTest()
        {
            var contact = new EmailAddress("contact@test.io");
            var messages = await _storage.GetEarlierContactMessagesAsync(contact, 0, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(0));

            messages = await _storage.GetEarlierContactMessagesAsync(contact, 100, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(0));

            var contact1Messages = await _storage.GetEarlierContactMessagesAsync(_contactAddress1, 100, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(contact1Messages.Count, Is.EqualTo(5));
            EnsureTimestampOrder(contact1Messages);
            Assert.That(contact1Messages.All(x => x.IsFromCorrespondenceWithContact(_contactAddress1)), Is.True);
            var subrange = await _storage.GetEarlierContactMessagesAsync(_contactAddress1, 4, contact1Messages[1], CancellationToken.None).ConfigureAwait(true);
            Assert.That(subrange.Count, Is.EqualTo(3));
            EnsureSubrange(contact1Messages, subrange, 2);

            var contact2Messages = await _storage.GetEarlierContactMessagesAsync(_contactAddress2, 100, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(contact2Messages.Count, Is.EqualTo(2));
            EnsureTimestampOrder(contact2Messages);
            Assert.That(contact2Messages.All(x => x.IsFromCorrespondenceWithContact(_contactAddress2)), Is.True);
            subrange = await _storage.GetEarlierContactMessagesAsync(_contactAddress2, 3, contact2Messages[1], CancellationToken.None).ConfigureAwait(true);
            EnsureSubrange(contact2Messages, subrange, 3);
        }

        [Test]
        public async Task GetFolderEarlierMessagesTest()
        {
            var messages = await _storage.GetEarlierMessagesInFoldersAsync(new[] { _account1.FoldersStructure[0] }, 0, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(0));

            messages = await _storage.GetEarlierMessagesInFoldersAsync(new [] { _account1.FoldersStructure[0] }, 5, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(4));
            EnsureTimestampOrder(messages);
            Assert.That(messages.All(x => x.Folder.IsInbox), Is.True);

            messages = await _storage.GetEarlierMessagesInFoldersAsync(new[] { _account1.FoldersStructure[1] }, 5, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(3));
            EnsureTimestampOrder(messages);
            Assert.That(messages.All(x => x.Folder.IsInbox), Is.False);

            messages = await _storage.GetEarlierMessagesInFoldersAsync(new[] { _account1.FoldersStructure[2] }, 5, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(1));
            EnsureTimestampOrder(messages);
            Assert.That(messages.All(x => x.Folder.IsInbox), Is.False);

            messages = await _storage.GetEarlierMessagesInFoldersAsync(new[] { _account2.FoldersStructure[0] }, 5, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(3));
            EnsureTimestampOrder(messages);
            Assert.That(messages.All(x => x.Folder.IsInbox), Is.True);

            messages = await _storage.GetEarlierMessagesInFoldersAsync(new[] { _account2.FoldersStructure[1] }, 5, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(4));
            EnsureTimestampOrder(messages);
            Assert.That(messages.All(x => x.Folder.IsInbox), Is.False);

        }


        [Test]
        public async Task GetFolderEarlierMessagesWithOldMessagesAddedTest()
        {
            var latest = await _storage.GetLatestMessageAsync(_account1.Email, "INBOX").ConfigureAwait(true);
            uint id = latest.Id + 1;
            var date = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(10));
            await _storage.AddMessageListAsync(_account1.Email, "INBOX", new List<Message>()
            {
                // old with same date
                CreateNewMessage(date, _contactAddress1, _account1.Email, "INBOX", id++),
                CreateNewMessage(date, _contactAddress1, _account1.Email, "INBOX", id++),
                CreateNewMessage(date, _contactAddress2, _account1.Email, "INBOX", id++),

                // old with different date
                CreateNewMessage(date.Subtract(TimeSpan.FromSeconds(10)), _contactAddress1, _account1.Email, "INBOX", id++),
                CreateNewMessage(date.Subtract(TimeSpan.FromSeconds(20)), _contactAddress1, _account1.Email, "INBOX", id++),
                CreateNewMessage(date.Subtract(TimeSpan.FromSeconds(30)), _contactAddress2, _account1.Email, "INBOX", id++)
            }).ConfigureAwait(true);

            var messages = await _storage.GetEarlierMessagesInFoldersAsync(new[] { _account1.FoldersStructure[0] }, 15, null, CancellationToken.None).ConfigureAwait(true);
            Assert.That(messages.Count, Is.EqualTo(10));
            EnsureTimestampOrder(messages);
            Assert.That(messages.All(x => x.Folder.IsInbox), Is.True);

            Assert.That(messages[0].Id, Is.EqualTo(latest.Id));
            Assert.That(messages[1].Id, Is.EqualTo(latest.Id - 1));
            Assert.That(messages[2].Id, Is.EqualTo(latest.Id - 2));
            Assert.That(messages[3].Id, Is.EqualTo(latest.Id - 3));

            Assert.That(messages[4].Id, Is.EqualTo(id - 4));
            Assert.That(messages[5].Id, Is.EqualTo(id - 5));
            Assert.That(messages[6].Id, Is.EqualTo(id - 6));

            Assert.That(messages[7].Id, Is.EqualTo(id - 3));
            Assert.That(messages[8].Id, Is.EqualTo(id - 2));
            Assert.That(messages[9].Id, Is.EqualTo(id - 1));

            for (int i = 0; i < 10; ++i)
            {
                var subMessages = await _storage.GetEarlierMessagesInFoldersAsync(new[] { _account1.FoldersStructure[0] }, 15, messages[i], CancellationToken.None).ConfigureAwait(true);
                Assert.That(subMessages.Count, Is.EqualTo(messages.Count - i - 1));
                EnsureTimestampOrder(subMessages);
                for (int j = 0; j < subMessages.Count; ++j)
                {
                    int k = i + 1 + j;
                    Assert.That(messages[k].Date, Is.EqualTo(subMessages[j].Date));
                    Assert.That(messages[k].Id, Is.EqualTo(subMessages[j].Id));
                    Assert.That(messages[k].Pk, Is.EqualTo(subMessages[j].Pk));
                }
            }
            await _storage.DeleteMessagesAsync(_account1.Email, "INBOX", messages.Select(x => x.Id).ToList()).ConfigureAwait(true);
        }
    }
}
