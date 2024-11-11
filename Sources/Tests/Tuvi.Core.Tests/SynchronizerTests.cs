using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;

namespace Tuvi.Core.Tests
{
    public class SynchornizerTests
    {
        class TestSynchronizer : FolderSynchronizer
        {
            SyncDataProvider DataProvider = new SyncDataProvider();
            public List<Message> LocalMessages => DataProvider.LocalMessages;
            public List<Message> RemoteMessages => DataProvider.RemoteMessages;
            public List<Message> UpdatedMessages = new List<Message>();
            public List<Message> DeletedMessages = new List<Message>();
            public List<Message> AddedMessages = new List<Message>();

            protected override Task<IReadOnlyList<Message>> LoadLocalMessagesAsync(uint minUid,
                                                                                   uint maxUid,
                                                                                   CancellationToken cancellationToken)
            {
                return DataProvider.LoadLocalMessagesAsync(minUid, maxUid, cancellationToken);
            }

            protected override Task<IReadOnlyList<Message>> LoadRemoteMessagesAsync(Message fromMessage,
                                                                                    int count,
                                                                                    CancellationToken cancellationToken)
            {
                return DataProvider.LoadRemoteMessagesAsync(fromMessage, count, cancellationToken);
            }

            protected override Task DeleteMessagesAsync(IReadOnlyList<Message> messages,
                                                        CancellationToken cancellationToken)
            {
                DeletedMessages.AddRange(messages);
                foreach (var message in messages)
                {
                    LocalMessages.RemoveAll(x => x.Id == message.Id);
                }
                return Task.CompletedTask;
            }
            protected override Task UpdateMessagesAsync(IReadOnlyList<Message> messages,
                                                        CancellationToken cancellationToken)
            {
                UpdatedMessages.AddRange(messages);
                return Task.CompletedTask;
            }

            protected override Task AddMessagesAsync(IReadOnlyList<Message> messages,
                                                     CancellationToken cancellationToken)
            {
                AddedMessages.AddRange(messages);
                LocalMessages.AddRange(messages);
                return Task.CompletedTask;
            }
        }

        static Message CreateMessage(uint id)
        {
            return CreateMessage(id, false, DateTimeOffset.Now);
        }

        static Message CreateMessage(uint id, bool read, DateTimeOffset date)
        {
            var message = new Message()
            {
                Id = id,
                Date = date,
                IsMarkedAsRead = read
            };
            return message;
        }

        [Test]
        public async Task TestBothEmpty()
        {
            var synchronizer = new TestSynchronizer();
            await synchronizer.SynchronizeAsync(null, null, default).ConfigureAwait(true);
            Assert.That(synchronizer.UpdatedMessages.Count, Is.EqualTo(0));
            Assert.That(synchronizer.DeletedMessages.Count, Is.EqualTo(0));
            Assert.That(synchronizer.AddedMessages.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task TestLocalEmptyRemoteOne()
        {
            var synchronizer = new TestSynchronizer();
            synchronizer.RemoteMessages.Add(CreateMessage(1));

            await synchronizer.SynchronizeAsync(null, null, default).ConfigureAwait(true);
            Assert.That(synchronizer.UpdatedMessages.Count, Is.EqualTo(0));
            Assert.That(synchronizer.DeletedMessages.Count, Is.EqualTo(0));
            Assert.That(synchronizer.AddedMessages.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task TestRemoteEmptyLocalOne()
        {
            var synchronizer = new TestSynchronizer();
            var message = CreateMessage(1);
            synchronizer.LocalMessages.Add(message);

            await synchronizer.SynchronizeAsync(message, message, default).ConfigureAwait(true);
            Assert.That(synchronizer.UpdatedMessages.Count, Is.EqualTo(0));
            Assert.That(synchronizer.DeletedMessages.Count, Is.EqualTo(1));
            Assert.That(synchronizer.AddedMessages.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task TestSingleElementUpdate()
        {
            var synchronizer = new TestSynchronizer();
            var date = DateTimeOffset.Now;
            synchronizer.RemoteMessages.Add(CreateMessage(1000, true, date));
            var message = CreateMessage(1000, false, date);
            synchronizer.LocalMessages.Add(message);

            await synchronizer.SynchronizeAsync(message, message, default).ConfigureAwait(true);
            Assert.That(synchronizer.UpdatedMessages.Count, Is.EqualTo(1));
            Assert.That(synchronizer.DeletedMessages.Count, Is.EqualTo(0));
            Assert.That(synchronizer.AddedMessages.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task TestSingleElementDelete()
        {
            var synchronizer = new TestSynchronizer();
            var date = DateTimeOffset.Now;
            synchronizer.RemoteMessages.Add(CreateMessage(10, true, date));
            var message = CreateMessage(1000, false, date);
            synchronizer.LocalMessages.Add(message);

            await synchronizer.SynchronizeAsync(message, message, default).ConfigureAwait(true);
            Assert.That(synchronizer.UpdatedMessages.Count, Is.EqualTo(0));
            Assert.That(synchronizer.DeletedMessages.Count, Is.EqualTo(1));
            Assert.That(synchronizer.AddedMessages.Count, Is.EqualTo(0));
            Assert.That(synchronizer.DeletedMessages[0].Id, Is.EqualTo(1000));
        }
#pragma warning disable CA1062
        [TestCase(new[] { 1, 2, 3 }, new[] { 1, 2, 3 }, ExpectedResult = new[] { 0, 3, 0, 1, 2, 3 })]
        [TestCase(new[] { 1, 2, 3 }, new[] { 2, 3 }, ExpectedResult = new[] { 1, 2, 0, 2, 3 })]
        [TestCase(new[] { 1, 2, 3 }, new[] { 1, 3 }, ExpectedResult = new[] { 1, 2, 0, 1, 3 })]
        [TestCase(new[] { 1, 2, 3 }, new[] { 3 }, ExpectedResult = new[] { 2, 1, 0, 3 })]
        [TestCase(new[] { 1, 2, 3 }, new[] { 1 }, ExpectedResult = new[] { 2, 1, 0, 1 })]
        [TestCase(new[] { 1, 2, 3 }, new[] { 2 }, ExpectedResult = new[] { 2, 1, 0, 2 })]
        [TestCase(new[] { 1, 2, 6 }, new[] { 4, 5, 6 }, ExpectedResult = new[] { 2, 1, 2, 4, 5, 6 })]
        [TestCase(new[] { 1, 2, 4 }, new[] { 4, 5, 6 }, ExpectedResult = new[] { 2, 1, 0, 4 })]
        [TestCase(new[] { 1, 2 }, new[] { 5, 6 }, ExpectedResult = new[] { 2, 0, 0, })]
        [TestCase(new[] { 1, 2 }, new[] { 2, 5, 6 }, ExpectedResult = new[] { 1, 1, 0, 2 })]
        [TestCase(new[] { 5, 52 }, new[] { 2, 45, 52, 60 }, ExpectedResult = new[] { 1, 1, 1, 45, 52 })]
        [TestCase(new[] { 5, 52 }, new[] { 1, 2, 3, 4, 5, 45, 52, 60 }, ExpectedResult = new[] { 0, 2, 1, 5, 45, 52 })]
        [TestCase(new[] { 1, 3, 5, 6 }, new[] { 2, 4, 7, 8 }, ExpectedResult = new[] { 4, 0, 2, 2, 4 })]
        [TestCase(new[] { 1 }, new[] { 1 }, ExpectedResult = new[] { 0, 1, 0, 1 })]
        [TestCase(new[] { 1, 2 }, new[] { 1, 2 }, ExpectedResult = new[] { 0, 2, 0, 1, 2 })]
        [TestCase(new[] { 1, 2, 3 }, new[] { 1, 2, 3 }, ExpectedResult = new[] { 0, 3, 0, 1, 2, 3 })]
        public async Task<uint[]> TestDeletionsResults(IReadOnlyList<uint> localUids,
                                                            IReadOnlyList<uint> remoteUids)
        {
            var s = new TestSynchronizer();
            var date = DateTimeOffset.Now;
            foreach (var uid in remoteUids)
            {
                s.RemoteMessages.Add(CreateMessage(uid, true, date));
            }
            foreach (var uid in localUids)
            {
                s.LocalMessages.Add(CreateMessage(uid, false, date));
            }
            await s.SynchronizeAsync(s.LocalMessages[0],
                                     s.LocalMessages[s.LocalMessages.Count - 1],
                                     default).ConfigureAwait(true);
            var res = new uint[3 + s.LocalMessages.Count];
            res[0] = (uint)s.DeletedMessages.Count;
            res[1] = (uint)s.UpdatedMessages.Count;
            res[2] = (uint)s.AddedMessages.Count;

            Array.Copy(s.LocalMessages.Select(x => x.Id).OrderBy(x => x).ToArray(), 0, res, 3, s.LocalMessages.Count);
            return res;
        }
#pragma warning restore CA1062
    }
}
