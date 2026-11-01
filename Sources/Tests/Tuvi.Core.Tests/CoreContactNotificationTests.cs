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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Tuvi.Core.DataStorage;
using Tuvi.Core.DataStorage.Impl;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;

namespace Tuvi.Core.Tests
{
    [NonParallelizable]
    public class CoreContactNotificationTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
        private static readonly Contact Contact = new Contact("Contact", new EmailAddress("contact@example.test"));

        private static TuviMail CreateCore(Mock<IDataStorage> storage)
        {
            var core = CoreTestBase.CreateCore(storage.Object);
            var security = Mock.Get(core.GetSecurityManager());
            security.Setup(x => x.IsNeverStartedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
            security.Setup(x => x.ResetAsync()).Returns(Task.CompletedTask);
            storage.Setup(x => x.ExistsContactWithEmailAddressAsync(Contact.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            storage.Setup(x => x.GetContactAsync(Contact.Email, It.IsAny<CancellationToken>())).ReturnsAsync(Contact);
            return core;
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CapturedStorageForwardersRejectNotificationsAfterClosing(bool dispose)
        {
            var storage = new Mock<IDataStorage>();
            using var core = CreateCore(storage);
            var added = (EventHandler<ContactAddedEventArgs>)storage.Invocations.Single(x => x.Method.Name == "add_ContactAdded").Arguments[0];
            var changed = (EventHandler<ContactChangedEventArgs>)storage.Invocations.Single(x => x.Method.Name == "add_ContactChanged").Arguments[0];
            int delivered = 0;
            core.ContactAdded += (sender, args) =>
            {
                Assert.That(sender, Is.SameAs(storage.Object));
                Assert.That(args.Contact, Is.SameAs(Contact));
                ++delivered;
            };
            core.ContactChanged += (sender, args) =>
            {
                Assert.That(sender, Is.SameAs(storage.Object));
                Assert.That(args.Contact, Is.SameAs(Contact));
                ++delivered;
            };
            void EmitCaptured()
            {
                added(storage.Object, new ContactAddedEventArgs(Contact));
                changed(storage.Object, new ContactChangedEventArgs(Contact));
            }
            EmitCaptured();
            Assert.That(delivered, Is.EqualTo(2));
            Mock.Get(core.GetSecurityManager()).Setup(x => x.ResetAsync()).Callback(EmitCaptured).Returns(Task.CompletedTask);
            if (dispose)
            {
                core.Dispose();
            }
            else
            {
                await core.ResetApplicationAsync().WaitAsync(Timeout).ConfigureAwait(false);
            }
            EmitCaptured();
            Assert.That(delivered, Is.EqualTo(2), "Even an already captured storage forwarder must reject new admission.");
            storage.VerifyRemove(x => x.ContactAdded -= It.IsAny<EventHandler<ContactAddedEventArgs>>(), Times.Once);
            storage.VerifyRemove(x => x.ContactChanged -= It.IsAny<EventHandler<ContactChangedEventArgs>>(), Times.Once);
        }

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        public async Task ClosingFromFirstSubscriberStopsMulticastTail(int kind, bool dispose)
        {
            var storage = new Mock<IDataStorage>();
            using var core = CreateCore(storage);
            int first = 0;
            int tail = 0;
            void Close()
            {
                ++first;
                if (dispose)
                {
                    core.Dispose();
                }
                else
                {
                    core.ResetApplicationAsync().WaitAsync(Timeout).GetAwaiter().GetResult();
                }
            }
            if (kind == 0)
            {
                core.ContactAdded += (_, args) => { Assert.That(args.Contact, Is.SameAs(Contact)); Close(); };
                core.ContactAdded += (_, _) => ++tail;
                storage.Raise(x => x.ContactAdded += null, new ContactAddedEventArgs(Contact));
            }
            else if (kind == 1 || kind == 2)
            {
                core.ContactChanged += (_, args) => { Assert.That(args.Contact, Is.SameAs(Contact)); Close(); };
                core.ContactChanged += (_, _) => ++tail;
                if (kind == 1)
                {
                    storage.Raise(x => x.ContactChanged += null, new ContactChangedEventArgs(Contact));
                }
                else
                {
                    await core.SetContactAvatarAsync(Contact.Email, new byte[] { 1, 2 }, 1, 2).WaitAsync(Timeout).ConfigureAwait(false);
                }
            }
            else
            {
                core.ContactDeleted += (_, args) => { Assert.That(args.ContactEmail, Is.EqualTo(Contact.Email)); Close(); };
                core.ContactDeleted += (_, _) => ++tail;
                await core.RemoveContactAsync(Contact.Email).WaitAsync(Timeout).ConfigureAwait(false);
            }
            Assert.That(first, Is.EqualTo(1));
            Assert.That(tail, Is.Zero, "Admission must be checked between external subscribers.");
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public async Task DelayedDirectContactPublicationsAreRejected(bool avatar, bool dispose)
        {
            var storage = new Mock<IDataStorage>();
            using var core = CreateCore(storage);
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            storage.Setup(x => x.SetContactAvatarAsync(Contact.Email, It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(pending.Task);
            storage.Setup(x => x.RemoveContactAsync(Contact.Email, It.IsAny<CancellationToken>())).Returns(pending.Task);
            int delivered = 0;
            core.ContactChanged += (_, _) => ++delivered;
            core.ContactDeleted += (_, _) => ++delivered;
            var operation = avatar ? core.SetContactAvatarAsync(Contact.Email, new byte[] { 1 }, 1, 1) : core.RemoveContactAsync(Contact.Email);
            try
            {
                Assert.That(operation.IsCompleted, Is.False);
                if (dispose)
                {
                    core.Dispose();
                }
                else
                {
                    await core.ResetApplicationAsync().WaitAsync(Timeout).ConfigureAwait(false);
                }
            }
            finally
            {
                pending.TrySetResult(true);
                await operation.WaitAsync(Timeout).ConfigureAwait(false);
            }
            Assert.That(delivered, Is.Zero);
        }

        [Test]
        public async Task ResetDoesNotWaitForAnAlreadyAdmittedCallback()
        {
            var storage = new Mock<IDataStorage>();
            using var core = CreateCore(storage);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var resume = new ManualResetEventSlim();
            bool finished = false;
            core.ContactAdded += (_, _) =>
            {
                entered.TrySetResult(true);
                if (!resume.Wait(Timeout))
                {
                    throw new TimeoutException("The admitted callback was not released.");
                }
                finished = true;
            };
            var publication = Task.Run(() => storage.Raise(x => x.ContactAdded += null, new ContactAddedEventArgs(Contact)));
            try
            {
                await entered.Task.WaitAsync(Timeout).ConfigureAwait(false);
                await core.ResetApplicationAsync().WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(publication.IsCompleted, Is.False);
            }
            finally
            {
                resume.Set();
                await publication.WaitAsync(Timeout).ConfigureAwait(false);
            }
            Assert.That(finished, Is.True, "Already admitted callbacks may finish after reset.");
        }

        [Test]
        public async Task ResetFromFirstCommittedBatchEventStopsOldCoreNotifications()
        {
            string path = Path.Combine(Path.GetTempPath(), "TuviCore-contact-batch-" + Guid.NewGuid().ToString("N") + ".db");
            using var storage = DataStorageProvider.GetDataStorage(path);
            try
            {
                await storage.CreateAsync("contact-batch").WaitAsync(Timeout).ConfigureAwait(false);
                var account = CoreTestBase.GetTestEmailAccount();
                await storage.AddAccountAsync(account).WaitAsync(Timeout).ConfigureAwait(false);
                var messages = Enumerable.Range(1, 2).Select(i =>
                {
                    var message = new Message { Id = (uint)i, Folder = account.FoldersStructure[0], Subject = "message-" + i, TextBody = "body-" + i };
                    message.From.Add(new EmailAddress("sender" + i + "@batch.test"));
                    message.To.Add(account.Email);
                    return message;
                }).ToList();
                int sourceEvents = 0;
                storage.ContactAdded += (_, _) => ++sourceEvents;
                using var core = CoreTestBase.CreateCore(storage);
                var security = Mock.Get(core.GetSecurityManager());
                security.Setup(x => x.IsNeverStartedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
                security.Setup(x => x.ResetAsync()).Returns(storage.ResetAsync);
                var events = new List<string>();
                core.ContactAdded += (_, _) =>
                {
                    events.Add("contact");
                    if (events.Count == 1)
                    {
                        core.ResetApplicationAsync().WaitAsync(Timeout).GetAwaiter().GetResult();
                    }
                };
                core.WipeAllDataNeeded += (_, _) => events.Add("wipe");
                await storage.AddMessageListAsync(account.Email, account.FoldersStructure[0].FullName, messages, false).WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(sourceEvents, Is.GreaterThanOrEqualTo(2), "The committed storage batch must actually publish its old tail.");
                Assert.That(events, Is.EqualTo(new[] { "contact", "wipe" }));
                Assert.That(File.Exists(path), Is.False);
            }
            finally
            {
                storage.Dispose();
                foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
                {
                    File.Delete(path + suffix);
                }
            }
        }
    }
}
