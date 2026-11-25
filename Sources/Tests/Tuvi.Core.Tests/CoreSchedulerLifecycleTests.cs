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
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;
using Tuvi.Core.Mail;
using Tuvi.Core.Tests.Infrastructure;
using static Tuvi.Core.Tests.Infrastructure.CoreTestSupport;

namespace Tuvi.Core.Tests
{
    [NonParallelizable]
    public class CoreSchedulerLifecycleTests
    {
        private sealed class DisposalCounter
        {
            private int _count;
            public int Count => Volatile.Read(ref _count);

            public DisposalCounter(SchedulerWithTimer scheduler)
            {
                scheduler.Disposed += (_, _) => Interlocked.Increment(ref _count);
            }
        }

        private sealed class GatedStopScheduler : SchedulerWithTimer
        {
            public Action BeforeStop { get; set; }

            public GatedStopScheduler(Func<CancellationToken, Task> action, double interval)
                : base(action, interval)
            {
            }

            internal override Task PrepareStop()
            {
                BeforeStop?.Invoke();
                return base.PrepareStop();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ResetCallersShareTheFirstOutcomeWithoutCheckingFileExistence(bool failReset)
        {
            var storage = new Mock<IDataStorage>();
            using var core = CoreTestBase.CreateCore(storage.Object);
            var security = Mock.Get(core.GetSecurityManager());
            var entered = Signal();
            var release = Signal();
            var failure = new IOException("Database reset failed");
            security.Setup(x => x.ResetAsync()).Returns(async () =>
            {
                entered.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                if (failReset) throw failure;
            });
            var reset = core.ResetApplicationAsync();
            try
            {
                var concurrent = core.ResetApplicationAsync();
                Assert.That(concurrent, Is.SameAs(reset));
                await Assert.ThrowsAsync<ObjectDisposedException>(() => core.InitializeApplicationAsync("password", default)).ConfigureAwait(false);
                await entered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                security.Verify(x => x.IsNeverStartedAsync(It.IsAny<CancellationToken>()), Times.Never);
                release.TrySetResult(true);
                foreach (var task in new[] { reset, concurrent, core.ResetApplicationAsync() })
                {
                    Assert.That(task, Is.SameAs(reset));
                    if (failReset)
                    {
                        var actual = await Assert.ThrowsAsync<IOException>(async () => await task.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                        Assert.That(actual, Is.SameAs(failure));
                    }
                    else await task.WaitAsync(TestTimeout).ConfigureAwait(false);
                }
                security.Verify(x => x.ResetAsync(), Times.Once);
            }
            finally
            {
                release.TrySetResult(true);
                try { await reset.WaitAsync(TestTimeout).ConfigureAwait(false); }
                catch (IOException ex) when (ReferenceEquals(ex, failure)) { }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task OwnerResetWaitsForBothMessageHandlersAndRetainsTheTerminalResult(bool failReset)
        {
            using var fixture = new CoreMailboxFixture();
            var accounts = new[] { CreateAccount("first"), CreateAccount("second") };
            var entered = new[] { Signal(), Signal() };
            var cancelled = new[] { Signal(), Signal() };
            var rejected = new[] { Signal(), Signal() };
            using var tryDirectReset = new ManualResetEventSlim(false);
            using var releaseHandlers = new ManualResetEventSlim(false);
            var reads = new int[2];
            var registrations = new CancellationTokenRegistration[2];
            var failure = new IOException("Terminal reset failure");
            var security = fixture.Security;
            security.Setup(x => x.IsNeverStartedAsync(It.IsAny<CancellationToken>()))
                .Returns(async (CancellationToken token) => !await fixture.Native.IsStorageExistAsync(token).ConfigureAwait(false));
            security.Setup(x => x.ResetAsync()).Returns(async () =>
            {
                await fixture.Native.ResetAsync().ConfigureAwait(false);
                if (failReset) throw failure;
            });
            fixture.Receive = (folder, token) =>
            {
                int index = folder.Account.Email.Equals(accounts[0].Email) ? 0 : 1;
                if (Interlocked.Increment(ref reads[index]) == 1)
                {
                    return Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());
                }
                registrations[index] = token.Register(() => cancelled[index].TrySetResult(true));
                return Task.FromResult<IReadOnlyList<Message>>(new[] { new Message { Id = 7, Folder = folder, Subject = "Reset request " + index } });
            };
            fixture.Core.MessagesReceived += (_, args) =>
            {
                int index = args.ReceivedMessages[0].Account.Email.Equals(accounts[0].Email) ? 0 : 1;
                entered[index].TrySetResult(true);
                if (!tryDirectReset.Wait(TestTimeout)) throw new TimeoutException("The owner did not start reset.");
                Assert.Throws<InvalidOperationException>(() => fixture.Core.ResetApplicationAsync().WaitAsync(TestTimeout).GetAwaiter().GetResult(),
                    "An active handler cannot join the owner's reset and wait for itself.");
                rejected[index].TrySetResult(true);
                if (!releaseHandlers.Wait(TestTimeout)) throw new TimeoutException("The owner did not release the message handlers.");
            };
            Task checks = Task.CompletedTask;
            Task reset = Task.CompletedTask;
            try
            {
                await fixture.InitializeAsync(accounts).ConfigureAwait(false);
                await Task.WhenAll(accounts.Select(x => Scheduler(fixture.Core, x).GetActionTask())).WaitAsync(TestTimeout).ConfigureAwait(false);
                checks = fixture.Core.CheckForNewInboxMessagesAsync();
                await Task.WhenAll(entered.Select(x => x.Task)).WaitAsync(TestTimeout).ConfigureAwait(false);
                reset = fixture.Core.ResetApplicationAsync();
                var concurrent = fixture.Core.ResetApplicationAsync();
                Assert.That(concurrent, Is.SameAs(reset));
                await Task.WhenAll(cancelled.Select(x => x.Task)).WaitAsync(TestTimeout).ConfigureAwait(false);
                tryDirectReset.Set();
                await Task.WhenAll(rejected.Select(x => x.Task)).WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(reset.IsCompleted, Is.False);
                security.Verify(x => x.ResetAsync(), Times.Never);
                for (int i = 0; i < accounts.Length; ++i)
                {
                    var message = await fixture.Native.GetMessageAsync(accounts[i].Email, accounts[i].DefaultInboxFolder.FullName, 7).WaitAsync(TestTimeout).ConfigureAwait(false);
                    Assert.That(message.Subject, Is.EqualTo("Reset request " + i));
                }
                releaseHandlers.Set();
                await checks.WaitAsync(TestTimeout).ConfigureAwait(false);
                foreach (var task in new[] { reset, concurrent })
                {
                    if (failReset)
                    {
                        var actual = await Assert.ThrowsAsync<IOException>(async () => await task.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                        Assert.That(actual, Is.SameAs(failure));
                    }
                    else
                    {
                        await task.WaitAsync(TestTimeout).ConfigureAwait(false);
                    }
                }
                var repeated = fixture.Core.ResetApplicationAsync();
                Assert.That(repeated, Is.SameAs(reset));
                if (failReset)
                {
                    var actual = await Assert.ThrowsAsync<IOException>(async () => await repeated.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                    Assert.That(actual, Is.SameAs(failure));
                }
                else
                {
                    await repeated.WaitAsync(TestTimeout).ConfigureAwait(false);
                }
                security.Verify(x => x.IsNeverStartedAsync(It.IsAny<CancellationToken>()), Times.Never);
                security.Verify(x => x.ResetAsync(), Times.Once);
                Assert.That(File.Exists(fixture.FilePath), Is.False);
                Assert.That(OwnedCount(fixture.Core), Is.Zero);
            }
            finally
            {
                tryDirectReset.Set();
                releaseHandlers.Set();
                await checks.WaitAsync(TestTimeout).ConfigureAwait(false);
                try { await reset.WaitAsync(TestTimeout).ConfigureAwait(false); }
                catch (IOException ex) when (ReferenceEquals(ex, failure)) { }
                foreach (var registration in registrations) registration.Dispose();
            }
        }

        [Test]
        public async Task BackgroundCheckExitsWhenCoreClosesBeforeItsSchedulerStartsStopping()
        {
            GatedStopScheduler scheduler = null;
            using var fixture = new CoreMailboxFixture((action, interval) => scheduler = new GatedStopScheduler(action, interval));
            var account = CreateAccount();
            var entered = Signal();
            var closing = Signal();
            var releaseCheck = Signal();
            using var allowStop = new ManualResetEventSlim(false);
            int reads = 0;
            int stopRegistrationTimedOut = 0;
            fixture.Receive = async (_, _) =>
            {
                if (Interlocked.Increment(ref reads) == 2)
                {
                    entered.TrySetResult(true);
                    await releaseCheck.Task.ConfigureAwait(false);
                }
                return Array.Empty<Message>();
            };
            await fixture.InitializeAsync(account).ConfigureAwait(false);
            await scheduler.GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
            scheduler.BeforeStop = () =>
            {
                closing.TrySetResult(true);
                if (!allowStop.Wait(TestTimeout)) Interlocked.Exchange(ref stopRegistrationTimedOut, 1);
            };
            var counter = new DisposalCounter(scheduler);
            var notifications = new List<Exception>();
            fixture.Core.ExceptionOccurred += (_, args) => notifications.Add(args.Exception);
            Task check = Task.CompletedTask;
            Task disposal = Task.CompletedTask;
            try
            {
                check = fixture.Core.CheckForNewInboxMessagesAsync();
                await entered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                disposal = Task.Run(fixture.Core.Dispose);
                await closing.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.Throws<ObjectDisposedException>(() => fixture.Core.GetAccountsAsync(CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(scheduler.IsStopping(), Is.False);
                releaseCheck.TrySetResult(true);
                await check.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(notifications, Is.Empty);
                Assert.That(disposal.IsCompleted, Is.False);
                fixture.Storage.Verify(x => x.Dispose(), Times.Never);
                Assert.Throws<ObjectDisposedException>(() => fixture.Core.GetAccountsAsync(CancellationToken.None).GetAwaiter().GetResult());
                allowStop.Set();
                await disposal.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(Volatile.Read(ref stopRegistrationTimedOut), Is.Zero, "The stop-registration gate must be released within the timeout.");
                Assert.That(counter.Count, Is.EqualTo(1));
                fixture.Storage.Verify(x => x.Dispose(), Times.Once);
                fixture.Credentials.Verify(x => x.Dispose(), Times.Once);
            }
            finally
            {
                releaseCheck.TrySetResult(true);
                allowStop.Set();
                await Task.WhenAll(check, disposal).WaitAsync(TestTimeout).ConfigureAwait(false);
            }
        }



        [Test]
        public async Task ResetRejectsAnUpdateThatStartedBeforeSchedulerAdmissionClosed()
        {
            using var fixture = new CoreMailboxFixture();
            var account = CreateAccount();
            var entered = Signal();
            var cancelled = Signal();
            var release = Signal();
            var updateEntered = Signal();
            var releaseUpdate = Signal();
            int reads = 0;
            fixture.Receive = async (_, token) =>
            {
                Interlocked.Increment(ref reads);
                using (token.Register(() => cancelled.TrySetResult(true)))
                {
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                }
                return Array.Empty<Message>();
            };
            async Task UpdateAsync(Account value, CancellationToken token)
            {
                updateEntered.TrySetResult(true);
                await releaseUpdate.Task.ConfigureAwait(false);
                await fixture.Native.UpdateAccountAuthAsync(value, token).ConfigureAwait(false);
            }
            fixture.Storage.Setup(x => x.UpdateAccountAuthAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns((Account value, CancellationToken token) => UpdateAsync(value, token));
            Task update = Task.CompletedTask;
            Task reset = Task.CompletedTask;
            try
            {
                await fixture.InitializeAsync(account).ConfigureAwait(false);
                await entered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                update = fixture.Core.UpdateAccountAsync(account);
                await updateEntered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                reset = fixture.Core.ResetApplicationAsync();
                await cancelled.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                releaseUpdate.TrySetResult(true);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await update.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                Assert.That(reads, Is.EqualTo(1));
                Assert.That(reset.IsCompleted, Is.False);
                release.TrySetResult(true);
                await reset.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(reads, Is.EqualTo(1));
            }
            finally
            {
                release.TrySetResult(true);
                releaseUpdate.TrySetResult(true);
                await reset.WaitAsync(TestTimeout).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task InitializationResumingAccountLoadingDuringResetPropagatesRegistrationRefusal()
        {
            using var fixture = new CoreMailboxFixture();
            var account = CreateAccount();
            var loading = Signal();
            var releaseLoading = Signal();
            var resetting = Signal();
            var releaseReset = Signal();
            Task reset = Task.CompletedTask;
            try
            {
                await fixture.InitializeAsync(account).ConfigureAwait(false);
                await Scheduler(fixture.Core, account).GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
                fixture.Storage.Setup(x => x.GetAccountsAsync(It.IsAny<CancellationToken>())).Returns(async (CancellationToken token) =>
                {
                    loading.TrySetResult(true);
                    await releaseLoading.Task.ConfigureAwait(false);
                    return await fixture.Native.GetAccountsAsync(token).ConfigureAwait(false);
                });
                fixture.Security.Setup(x => x.ResetAsync()).Returns(async () =>
                {
                    resetting.TrySetResult(true);
                    await releaseReset.Task.ConfigureAwait(false);
                    await fixture.Native.ResetAsync().ConfigureAwait(false);
                });
                var initialization = fixture.Core.InitializeApplicationAsync("account-removal", CancellationToken.None);
                await loading.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                reset = fixture.Core.ResetApplicationAsync();
                await resetting.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                releaseLoading.TrySetResult(true);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await initialization.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                Assert.That(OwnedCount(fixture.Core), Is.Zero);
            }
            finally
            {
                releaseLoading.TrySetResult(true);
                releaseReset.TrySetResult(true);
                await reset.WaitAsync(TestTimeout).ConfigureAwait(false);
            }
        }



        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Core disposal is scheduled and awaited in finally with a timeout so regressions cannot hang the test host.")]
        [Test]
        public async Task ManualCoreCheckNotifiesAThrowingSubscriberOnceAndCanRetry()
        {
            var account = CreateAccount();
            var storage = new Mock<IDataStorage>();
            storage.Setup(x => x.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Account> { account });
            var mailbox = new Mock<IMailBox>();
            var failure = new IOException("Mailbox failed");
            var subscriberFailure = new InvalidOperationException("Subscriber failed");
            int reads = 0;
            mailbox.Setup(x => x.GetMessagesAsync(It.IsAny<Folder>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(() => Interlocked.Increment(ref reads) == 2
                    ? Task.FromException<IReadOnlyList<Message>>(failure)
                    : Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>()));
            var core = CoreTestBase.CreateCore(storage.Object, mailbox.Object);
            var notifications = new List<Exception>();
            object notificationSender = null;
            core.ExceptionOccurred += (sender, args) =>
            {
                notificationSender = sender;
                notifications.Add(args.Exception);
                throw subscriberFailure;
            };
            try
            {
                await core.InitializeApplicationAsync("password", CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false);
                await Scheduler(core, account).GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
                var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await core.CheckForNewInboxMessagesAsync().WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                Assert.That(actual, Is.SameAs(subscriberFailure));
                Assert.That(notifications, Is.EqualTo(new[] { failure }));
                Assert.That(notificationSender, Is.SameAs(core));
                await core.CheckForNewInboxMessagesAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(reads, Is.EqualTo(3));
                Assert.That(notifications.Count, Is.EqualTo(1));
            }
            finally
            {
                try
                {
                    await Task.Run(core.Dispose).WaitAsync(TestTimeout).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex) when (ReferenceEquals(ex, subscriberFailure))
                {
                    // The original implementation replays the completed check's failure during cleanup.
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CancellationFailureStopsBothAccountsAndPreservesTheFile(bool reinitialize)
        {
            using var fixture = new CoreMailboxFixture();
            var accounts = new[] { CreateAccount("first"), CreateAccount("second") };
            var entered = new[] { Signal(), Signal() };
            var cancelled = Signal();
            var release = Signal();
            var counts = new int[2];
            var registrations = new CancellationTokenRegistration[2];
            int cancellations = 0;
            var failure = new IOException("First cancellation failed");
            fixture.Receive = async (folder, token) =>
            {
                int index = folder.Account.Email == accounts[0].Email ? 0 : 1;
                registrations[index] = token.Register(() =>
                {
                    Interlocked.Increment(ref counts[index]);
                    int current = Interlocked.Increment(ref cancellations);
                    if (current == 2) cancelled.TrySetResult(true);
                    if (current == 1) throw failure;
                });
                entered[index].TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                return Array.Empty<Message>();
            };
            var security = fixture.Security;
            Task operation = Task.CompletedTask;
            try
            {
                await fixture.InitializeAsync(accounts).ConfigureAwait(false);
                await Task.WhenAll(entered.Select(x => x.Task)).WaitAsync(TestTimeout).ConfigureAwait(false);
                operation = reinitialize
                    ? fixture.Core.InitializeApplicationAsync("account-removal", CancellationToken.None)
                    : fixture.Core.ResetApplicationAsync();
                await cancelled.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(counts, Is.EqualTo(new[] { 1, 1 }));
                Assert.That(operation.IsCompleted, Is.False, "A cancellation failure must still wait for all actions.");
                security.Verify(x => x.ResetAsync(), Times.Never);
                release.TrySetResult(true);
                var actual = await Assert.ThrowsAsync<AggregateException>(async () => await operation.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                Assert.That(actual.Flatten().InnerExceptions, Is.EqualTo(new[] { failure }));
                Assert.That(File.Exists(fixture.FilePath), Is.True);
                Assert.That((await fixture.Native.GetAccountsAsync().WaitAsync(TestTimeout).ConfigureAwait(false)).Count, Is.EqualTo(2));
                Assert.That(OwnedCount(fixture.Core), Is.Zero);
                if (reinitialize)
                {
                    await fixture.Core.CheckForNewInboxMessagesAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
                    await fixture.Core.InitializeApplicationAsync("account-removal", CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false);
                    await Task.WhenAll(accounts.Select(x => Scheduler(fixture.Core, x).GetActionTask())).WaitAsync(TestTimeout).ConfigureAwait(false);
                    Assert.That(OwnedCount(fixture.Core), Is.EqualTo(2));
                }
                else
                {
                    Assert.That(fixture.Core.ResetApplicationAsync(), Is.SameAs(operation));
                    await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Core.CheckForNewInboxMessagesAsync()).ConfigureAwait(false);
                }
                security.Verify(x => x.ResetAsync(), Times.Never);
            }
            finally
            {
                release.TrySetResult(true);
                try { await operation.WaitAsync(TestTimeout).ConfigureAwait(false); }
                catch (AggregateException ex) when (ex.Flatten().InnerExceptions.All(x => ReferenceEquals(x, failure))) { }
                await Task.Run(fixture.Core.Dispose).WaitAsync(TestTimeout).ConfigureAwait(false);
                foreach (var registration in registrations) registration.Dispose();
            }
            Assert.That(File.Exists(fixture.FilePath), Is.True, "Ordinary disposal after a failed reset must preserve the file.");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Core disposal is scheduled and awaited in finally with a timeout so regressions cannot hang the test host.")]
        [Test]
        public async Task PasswordFailureWhileStoppingACompletedCheckDoesNotBecomeFalseInitialization()
        {
            var account = CreateAccount();
            var storage = new Mock<IDataStorage>();
            storage.Setup(x => x.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Account> { account });
            var mailbox = new Mock<IMailBox>();
            var entered = Signal();
            var cancelled = Signal();
            using var release = new ManualResetEventSlim(false);
            var failure = new DataBasePasswordException("Failure from the previous check");
            CancellationTokenRegistration registration = default;
            int reads = 0;
            mailbox.Setup(x => x.GetMessagesAsync(It.IsAny<Folder>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns((Folder folder, int count, CancellationToken token) =>
                {
                    if (Interlocked.Increment(ref reads) != 2)
                    {
                        return Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());
                    }
                    registration = token.Register(() => cancelled.TrySetResult(true));
                    return Task.FromException<IReadOnlyList<Message>>(new IOException("Mailbox failed"));
                });
            var core = CoreTestBase.CreateCore(storage.Object, mailbox.Object);
            Mock.Get(core.GetSecurityManager()).Setup(x => x.StartAsync("wrong", It.IsAny<CancellationToken>())).ThrowsAsync(failure);
            core.ExceptionOccurred += (_, _) =>
            {
                entered.TrySetResult(true);
                if (!release.Wait(TestTimeout)) throw new TimeoutException("The exception handler was not released.");
                throw failure;
            };
            Task check = null;
            Task<bool> initialization = null;
            try
            {
                Assert.That(await core.InitializeApplicationAsync("wrong", CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false), Is.False);
                storage.Verify(x => x.GetAccountsAsync(It.IsAny<CancellationToken>()), Times.Never);
                await core.InitializeApplicationAsync("password", CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false);
                await Scheduler(core, account).GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
                check = core.CheckForNewInboxMessagesAsync();
                await entered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                initialization = core.InitializeApplicationAsync("password", CancellationToken.None);
                await cancelled.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                release.Set();
                var checkFailure = await Assert.ThrowsAsync<DataBasePasswordException>(async () =>
                    await check.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                var initializationFailure = await Assert.ThrowsAsync<DataBasePasswordException>(async () =>
                    await initialization.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                Assert.That(checkFailure, Is.SameAs(failure));
                Assert.That(initializationFailure, Is.SameAs(failure));
                storage.Verify(x => x.GetAccountsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
            }
            finally
            {
                release.Set();
                try
                {
                    await Task.Run(core.Dispose).WaitAsync(TestTimeout).ConfigureAwait(false);
                }
                catch (DataBasePasswordException ex) when (ReferenceEquals(ex, failure))
                {
                    // The owner observes the retained stop failure.
                }
                if (check != null)
                {
                    await Assert.CatchAsync<DataBasePasswordException>(async () =>
                        await check.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                }
                if (initialization != null)
                {
                    await Assert.CatchAsync<DataBasePasswordException>(async () =>
                        await initialization.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                }
                registration.Dispose();
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task ActiveCheckRejectsAccountDeletionAndInitialization(bool fromMessageHandler, bool reinitialize)
        {
            using var fixture = new CoreMailboxFixture();
            var account = CreateAccount();
            var attempted = Signal();
            int reads = 0;
            int deleted = 0;
            Task reentry = null;
            Task check = Task.CompletedTask;
            void Reenter()
            {
                reentry = reinitialize
                    ? fixture.Core.InitializeApplicationAsync("account-removal", CancellationToken.None)
                    : fixture.Core.DeleteAccountAsync(account, CancellationToken.None);
                attempted.TrySetResult(true);
            }
            fixture.Receive = (folder, _) =>
            {
                int read = Interlocked.Increment(ref reads);
                if (read == 2 && !fromMessageHandler) Reenter();
                return Task.FromResult<IReadOnlyList<Message>>(read == 2 && fromMessageHandler
                    ? new[] { new Message { Id = 7, Folder = folder, Subject = "Lifecycle reentry" } }
                    : Array.Empty<Message>());
            };
            fixture.Core.MessagesReceived += (_, _) => Reenter();
            fixture.Core.AccountDeleted += (_, _) => Interlocked.Increment(ref deleted);
            try
            {
                await fixture.InitializeAsync(account).ConfigureAwait(false);
                var scheduler = Scheduler(fixture.Core, account);
                await scheduler.GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
                check = fixture.Core.CheckForNewInboxMessagesAsync();
                await attempted.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await reentry.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                await check.WaitAsync(TestTimeout).ConfigureAwait(false);
                var stored = await fixture.Native.GetAccountAsync(account.Email).WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(stored.Id, Is.EqualTo(account.Id));
                Assert.That(stored.DefaultInboxFolder.Id, Is.EqualTo(account.DefaultInboxFolder.Id));
                Assert.That(deleted, Is.Zero);
                Assert.That(Scheduler(fixture.Core, account), Is.SameAs(scheduler));
                Assert.That(scheduler.IsStopping(), Is.False);
                fixture.Security.Verify(x => x.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
                fixture.Security.Verify(x => x.RemovePgpKeys(It.IsAny<Account>()), Times.Never);
                fixture.Storage.Verify(x => x.DeleteFolderAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
                fixture.Storage.Verify(x => x.DeleteAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
                await fixture.Core.CheckForNewInboxMessagesAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(reads, Is.EqualTo(3));
            }
            finally
            {
                await check.WaitAsync(TestTimeout).ConfigureAwait(false);
                if (reentry != null)
                {
                    try { await reentry.WaitAsync(TestTimeout).ConfigureAwait(false); }
                    catch (InvalidOperationException) { }
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CancellationCallbackRejectsInitializationAndDeletionAfterTheCheckCompletes(bool reset)
        {
            using var fixture = new CoreMailboxFixture();
            var account = CreateAccount();
            var entered = Signal();
            var cancellationEntered = Signal();
            var attempted = Signal();
            var releaseCheck = Signal();
            using var tryReentry = new ManualResetEventSlim(false);
            using var releaseCancellation = new ManualResetEventSlim(false);
            CancellationTokenRegistration registration = default;
            Task initialization = Task.CompletedTask;
            Task deletion = Task.CompletedTask;
            Task closing = Task.CompletedTask;
            Task check = Task.CompletedTask;
            bool executingAction = true;
            bool executingCancellation = false;
            fixture.Receive = async (_, token) =>
            {
                var scheduler = Scheduler(fixture.Core, account);
                registration = token.Register(() =>
                {
                    cancellationEntered.TrySetResult(true);
                    if (!tryReentry.Wait(TestTimeout)) throw new TimeoutException("The cancellation callback was not resumed.");
                    executingAction = scheduler.IsExecutingAction;
                    executingCancellation = scheduler.IsExecutingCancellation;
                    initialization = fixture.Core.InitializeApplicationAsync("account-removal", CancellationToken.None);
                    deletion = fixture.Core.DeleteAccountAsync(account, CancellationToken.None);
                    attempted.TrySetResult(true);
                    if (!releaseCancellation.Wait(TestTimeout)) throw new TimeoutException("The cancellation callback was not released.");
                });
                entered.TrySetResult(true);
                await releaseCheck.Task.ConfigureAwait(false);
                return Array.Empty<Message>();
            };
            try
            {
                await fixture.InitializeAsync(account).ConfigureAwait(false);
                await entered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                var scheduler = Scheduler(fixture.Core, account);
                var counter = new DisposalCounter(scheduler);
                check = scheduler.GetActionTask();
                closing = reset ? fixture.Core.ResetApplicationAsync() : Task.Run(fixture.Core.Dispose);
                await cancellationEntered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                releaseCheck.TrySetResult(true);
                await check.WaitAsync(TestTimeout).ConfigureAwait(false);
                tryReentry.Set();
                await attempted.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(executingAction, Is.False);
                Assert.That(executingCancellation, Is.True);
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await initialization.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await deletion.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                fixture.Security.Verify(x => x.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
                fixture.Storage.Verify(x => x.DeleteFolderAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
                fixture.Storage.Verify(x => x.DeleteAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
                fixture.Storage.Verify(x => x.Dispose(), Times.Never);
                Assert.That(counter.Count, Is.Zero);
                Assert.That(OwnedCount(fixture.Core), Is.EqualTo(1));
                Assert.That(closing.IsCompleted, Is.False);
                releaseCancellation.Set();
                await closing.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(counter.Count, Is.EqualTo(1));
                Assert.That(OwnedCount(fixture.Core), Is.Zero);
                await Task.Run(fixture.Core.Dispose).WaitAsync(TestTimeout).ConfigureAwait(false);
                fixture.Storage.Verify(x => x.Dispose(), Times.Once);
            }
            finally
            {
                tryReentry.Set();
                releaseCheck.TrySetResult(true);
                releaseCancellation.Set();
                await Task.WhenAll(check, closing).WaitAsync(TestTimeout).ConfigureAwait(false);
                foreach (var operation in new[] { initialization, deletion })
                {
                    try { await operation.WaitAsync(TestTimeout).ConfigureAwait(false); }
                    catch (InvalidOperationException) { }
                }
                registration.Dispose();
            }
        }

        [Test]
        public async Task MessageHandlersCannotDeleteEachOthersAccounts()
        {
            using var fixture = new CoreMailboxFixture();
            var accounts = new[] { CreateAccount("first"), CreateAccount("second") };
            var entered = new[] { Signal(), Signal() };
            var attempted = new[] { Signal(), Signal() };
            var reads = new int[2];
            var deletions = new[] { Task.CompletedTask, Task.CompletedTask };
            using var tryDeletion = new ManualResetEventSlim(false);
            using var releaseHandlers = new ManualResetEventSlim(false);
            int deleted = 0;
            Task checks = Task.CompletedTask;
            fixture.Receive = (folder, _) =>
            {
                int index = folder.Account.Email.Equals(accounts[0].Email) ? 0 : 1;
                return Task.FromResult<IReadOnlyList<Message>>(Interlocked.Increment(ref reads[index]) == 2
                    ? new[] { new Message { Id = 7, Folder = folder, Subject = "Cross-account deletion " + index } }
                    : Array.Empty<Message>());
            };
            fixture.Core.MessagesReceived += (_, args) =>
            {
                int index = args.ReceivedMessages[0].Account.Email.Equals(accounts[0].Email) ? 0 : 1;
                entered[index].TrySetResult(true);
                if (!tryDeletion.Wait(TestTimeout)) throw new TimeoutException("The deletion attempts were not started.");
                deletions[index] = fixture.Core.DeleteAccountAsync(accounts[1 - index], CancellationToken.None);
                attempted[index].TrySetResult(true);
                if (!releaseHandlers.Wait(TestTimeout)) throw new TimeoutException("The message handlers were not released.");
            };
            fixture.Core.AccountDeleted += (_, _) => Interlocked.Increment(ref deleted);
            try
            {
                await fixture.InitializeAsync(accounts).ConfigureAwait(false);
                await Task.WhenAll(accounts.Select(x => Scheduler(fixture.Core, x).GetActionTask())).WaitAsync(TestTimeout).ConfigureAwait(false);
                checks = fixture.Core.CheckForNewInboxMessagesAsync();
                await Task.WhenAll(entered.Select(x => x.Task)).WaitAsync(TestTimeout).ConfigureAwait(false);
                tryDeletion.Set();
                await Task.WhenAll(attempted.Select(x => x.Task)).WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(deletions.Select(x => x.IsCompleted), Is.All.True, "Both calls must be rejected before either handler returns.");
                foreach (var deletion in deletions)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                        await deletion.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                }
                foreach (var account in accounts)
                {
                    var stored = await fixture.Native.GetAccountAsync(account.Email).WaitAsync(TestTimeout).ConfigureAwait(false);
                    Assert.That(stored.Id, Is.EqualTo(account.Id));
                    Assert.That(stored.DefaultInboxFolder.Id, Is.EqualTo(account.DefaultInboxFolder.Id));
                    Assert.That(Scheduler(fixture.Core, account).IsStopping(), Is.False);
                }
                Assert.That(deleted, Is.Zero);
                fixture.Security.Verify(x => x.RemovePgpKeys(It.IsAny<Account>()), Times.Never);
                fixture.Storage.Verify(x => x.DeleteAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
                releaseHandlers.Set();
                await checks.WaitAsync(TestTimeout).ConfigureAwait(false);
            }
            finally
            {
                tryDeletion.Set();
                releaseHandlers.Set();
                await checks.WaitAsync(TestTimeout).ConfigureAwait(false);
                foreach (var deletion in deletions)
                {
                    try { await deletion.WaitAsync(TestTimeout).ConfigureAwait(false); }
                    catch (InvalidOperationException) { }
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task FolderDeletionFailureRestoresSynchronization(bool withoutAccountId)
        {
            using var fixture = new CoreMailboxFixture();
            var account = CreateAccount();
            var failure = new IOException("Folder deletion failed before writing");
            int reads = 0;
            int deleted = 0;
            Account fresh = null;
            Account checkedAccount = null;
            fixture.Receive = (folder, _) =>
            {
                checkedAccount = folder.Account;
                Interlocked.Increment(ref reads);
                return Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());
            };
            fixture.Core.AccountDeleted += (_, _) => Interlocked.Increment(ref deleted);
            await fixture.InitializeAsync(account).ConfigureAwait(false);
            var old = Scheduler(fixture.Core, account);
            var counter = new DisposalCounter(old);
            await old.GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
            fixture.Storage.Setup(x => x.GetAccountAsync(account.Email, It.IsAny<CancellationToken>()))
                .Returns(async (EmailAddress email, CancellationToken token) => fresh = await fixture.Native.GetAccountAsync(email, token).ConfigureAwait(false));
            fixture.Storage.Setup(x => x.DeleteFolderAsync(account.Email, account.DefaultInboxFolder.FullName, CancellationToken.None)).ThrowsAsync(failure);
            var request = withoutAccountId ? CreateAccount() : account;

            var actual = await Assert.ThrowsAsync<IOException>(async () =>
                await fixture.Core.DeleteAccountAsync(request, CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);

            Assert.That(actual, Is.SameAs(failure));
            var stored = await fixture.Native.GetAccountAsync(account.Email).WaitAsync(TestTimeout).ConfigureAwait(false);
            Assert.That(stored.Id, Is.EqualTo(account.Id));
            Assert.That(stored.FoldersStructure.Select(x => x.Id), Is.EqualTo(account.FoldersStructure.Select(x => x.Id)));
            Assert.That(stored.DefaultInboxFolder.FullName, Is.EqualTo(account.DefaultInboxFolder.FullName));
            Assert.That(deleted, Is.Zero);
            Assert.That(counter.Count, Is.EqualTo(1));
            Assert.That(OwnedCount(fixture.Core), Is.EqualTo(1));
            var restored = Scheduler(fixture.Core, account);
            Assert.That(restored, Is.Not.SameAs(old));
            await restored.GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
            Assert.That(checkedAccount, Is.SameAs(fresh), "The replacement check must use the account reread from storage.");
            int afterStartup = Volatile.Read(ref reads);
            await fixture.Core.CheckForNewInboxMessagesAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
            Assert.That(Volatile.Read(ref reads), Is.EqualTo(afterStartup + 1), "A separate manual check must reach the mailbox after the replacement startup check.");
            fixture.Security.Verify(x => x.RemovePgpKeys(It.IsAny<Account>()), Times.Never);
            fixture.Storage.Verify(x => x.DeleteAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ShutdownPreventsDelayedSchedulerRestoration(bool reset)
        {
            int created = 0;
            using var fixture = new CoreMailboxFixture((action, interval) =>
            {
                Interlocked.Increment(ref created);
                return new SchedulerWithTimer(action, interval);
            });
            var account = CreateAccount();
            var failure = new IOException("Folder deletion failed");
            var reading = Signal();
            var release = Signal();
            int deleted = 0;
            fixture.Core.AccountDeleted += (_, _) => Interlocked.Increment(ref deleted);
            await fixture.InitializeAsync(account).ConfigureAwait(false);
            await Scheduler(fixture.Core, account).GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
            fixture.Storage.Setup(x => x.DeleteFolderAsync(account.Email, account.DefaultInboxFolder.FullName, CancellationToken.None)).ThrowsAsync(failure);
            fixture.Storage.Setup(x => x.GetAccountAsync(account.Email, CancellationToken.None))
                .Returns(async () =>
                {
                    var stored = await fixture.Native.GetAccountAsync(account.Email).ConfigureAwait(false);
                    reading.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    return stored;
                });
            Task deletion = fixture.Core.DeleteAccountAsync(account, CancellationToken.None);
            Task closing = Task.CompletedTask;
            try
            {
                await reading.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(OwnedCount(fixture.Core), Is.Zero);
                closing = reset ? fixture.Core.ResetApplicationAsync() : Task.Run(fixture.Core.Dispose);
                await closing.WaitAsync(TestTimeout).ConfigureAwait(false);
                release.TrySetResult(true);
                var actual = await Assert.ThrowsAsync<IOException>(async () => await deletion.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                Assert.That(actual, Is.SameAs(failure));
                Assert.That(created, Is.EqualTo(1));
                Assert.That(OwnedCount(fixture.Core), Is.Zero);
                Assert.That(deleted, Is.Zero);
            }
            finally
            {
                release.TrySetResult(true);
                await closing.WaitAsync(TestTimeout).ConfigureAwait(false);
                try { await deletion.WaitAsync(TestTimeout).ConfigureAwait(false); }
                catch (IOException ex) when (ReferenceEquals(ex, failure)) { }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task NewerDeletionPreventsDelayedSchedulerRestoration(bool completeNewerDeletionFirst)
        {
            int created = 0;
            using var fixture = new CoreMailboxFixture((action, interval) =>
            {
                Interlocked.Increment(ref created);
                return new SchedulerWithTimer(action, interval);
            });
            var account = CreateAccount();
            var failure = new IOException("First deletion failed");
            var reading = Signal();
            var releaseRead = Signal();
            var deleting = Signal();
            var releaseDelete = Signal();
            int folderDeletes = 0;
            int deleted = 0;
            fixture.Core.AccountDeleted += (_, _) => Interlocked.Increment(ref deleted);
            await fixture.InitializeAsync(account).ConfigureAwait(false);
            await Scheduler(fixture.Core, account).GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
            fixture.Storage.Setup(x => x.DeleteFolderAsync(account.Email, account.DefaultInboxFolder.FullName, CancellationToken.None))
                .Returns(async () =>
                {
                    if (Interlocked.Increment(ref folderDeletes) == 1) throw failure;
                    deleting.TrySetResult(true);
                    await releaseDelete.Task.ConfigureAwait(false);
                    await fixture.Native.DeleteFolderAsync(account.Email, account.DefaultInboxFolder.FullName).ConfigureAwait(false);
                });
            fixture.Storage.Setup(x => x.GetAccountAsync(account.Email, CancellationToken.None))
                .Returns(async () =>
                {
                    var stored = await fixture.Native.GetAccountAsync(account.Email).ConfigureAwait(false);
                    reading.TrySetResult(true);
                    await releaseRead.Task.ConfigureAwait(false);
                    return stored;
                });
            Task first = fixture.Core.DeleteAccountAsync(account, CancellationToken.None);
            Task second = Task.CompletedTask;
            try
            {
                await reading.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                second = fixture.Core.DeleteAccountAsync(account, CancellationToken.None);
                await deleting.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                if (completeNewerDeletionFirst)
                {
                    releaseDelete.TrySetResult(true);
                    await second.WaitAsync(TestTimeout).ConfigureAwait(false);
                }
                releaseRead.TrySetResult(true);
                var actual = await Assert.ThrowsAsync<IOException>(async () => await first.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                Assert.That(actual, Is.SameAs(failure));
                Assert.That(created, Is.EqualTo(1));
                Assert.That(OwnedCount(fixture.Core), Is.Zero);
                if (!completeNewerDeletionFirst) Assert.That(second.IsCompleted, Is.False);
                releaseDelete.TrySetResult(true);
                await second.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(await fixture.Native.ExistsAccountWithEmailAddressAsync(account.Email).WaitAsync(TestTimeout).ConfigureAwait(false), Is.False);
                Assert.That(deleted, Is.EqualTo(1));
                fixture.Security.Verify(x => x.RemovePgpKeys(account), Times.Once);
            }
            finally
            {
                releaseRead.TrySetResult(true);
                releaseDelete.TrySetResult(true);
                await second.WaitAsync(TestTimeout).ConfigureAwait(false);
                try { await first.WaitAsync(TestTimeout).ConfigureAwait(false); }
                catch (IOException ex) when (ReferenceEquals(ex, failure)) { }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RestorationFailurePreservesTheFolderDeletionError(bool failSchedulerCreation)
        {
            int created = 0;
            var secondary = new IOException("Synchronization restoration failed");
            using var fixture = new CoreMailboxFixture((action, interval) =>
            {
                if (Interlocked.Increment(ref created) == 2 && failSchedulerCreation) throw secondary;
                return new SchedulerWithTimer(action, interval);
            });
            var account = CreateAccount();
            var failure = new IOException("Original folder deletion error");
            int deleted = 0;
            fixture.Core.AccountDeleted += (_, _) => Interlocked.Increment(ref deleted);
            await fixture.InitializeAsync(account).ConfigureAwait(false);
            await Scheduler(fixture.Core, account).GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
            fixture.Storage.Setup(x => x.DeleteFolderAsync(account.Email, account.DefaultInboxFolder.FullName, CancellationToken.None)).ThrowsAsync(failure);
            if (!failSchedulerCreation) fixture.Storage.Setup(x => x.GetAccountAsync(account.Email, CancellationToken.None)).ThrowsAsync(secondary);

            var actual = await Assert.ThrowsAsync<IOException>(async () =>
                await fixture.Core.DeleteAccountAsync(account, CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);

            Assert.That(actual, Is.SameAs(failure));
            Assert.That(created, Is.EqualTo(failSchedulerCreation ? 2 : 1));
            Assert.That(OwnedCount(fixture.Core), Is.Zero);
            var stored = await fixture.Native.GetAccountAsync(account.Email).WaitAsync(TestTimeout).ConfigureAwait(false);
            Assert.That(stored.Id, Is.EqualTo(account.Id));
            Assert.That(stored.DefaultInboxFolder.Id, Is.EqualTo(account.DefaultInboxFolder.Id));
            Assert.That(deleted, Is.Zero);
            fixture.Security.Verify(x => x.RemovePgpKeys(It.IsAny<Account>()), Times.Never);
        }

        [Test]
        public async Task FolderDeletionFailureDoesNotStartAnUnregisteredScheduler()
        {
            using var fixture = new CoreMailboxFixture();
            var account = CreateAccount();
            await fixture.Native.CreateAsync("account-removal").WaitAsync(TestTimeout).ConfigureAwait(false);
            await fixture.Native.AddAccountAsync(account).WaitAsync(TestTimeout).ConfigureAwait(false);
            var failure = new IOException("No scheduler was registered");
            fixture.Storage.Setup(x => x.DeleteFolderAsync(account.Email, account.DefaultInboxFolder.FullName, CancellationToken.None)).ThrowsAsync(failure);

            var actual = await Assert.ThrowsAsync<IOException>(async () =>
                await fixture.Core.DeleteAccountAsync(account, CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);

            Assert.That(actual, Is.SameAs(failure));
            Assert.That(OwnedCount(fixture.Core), Is.Zero);
            Assert.That(await fixture.Native.ExistsAccountWithEmailAddressAsync(account.Email).WaitAsync(TestTimeout).ConfigureAwait(false), Is.True);
            fixture.Storage.Verify(x => x.GetAccountAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task FinalAccountDeletionFailureDoesNotRestoreSynchronization()
        {
            int created = 0;
            using var fixture = new CoreMailboxFixture((action, interval) =>
            {
                Interlocked.Increment(ref created);
                return new SchedulerWithTimer(action, interval);
            });
            var account = CreateAccount();
            var failure = new IOException("Final account deletion failed");
            int deleted = 0;
            fixture.Core.AccountDeleted += (_, _) => Interlocked.Increment(ref deleted);
            await fixture.InitializeAsync(account).ConfigureAwait(false);
            await Scheduler(fixture.Core, account).GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
            var message = new Message { Id = 7, Folder = account.DefaultInboxFolder, Subject = "Committed deletion", TextBody = "Removed before the account write fails" };
            await fixture.Native.AddMessageAsync(account.Email, message).WaitAsync(TestTimeout).ConfigureAwait(false);
            var before = await fixture.Native.GetMessageAsync(account.Email, account.DefaultInboxFolder.FullName, message.Id).WaitAsync(TestTimeout).ConfigureAwait(false);
            Assert.That(before.Subject, Is.EqualTo(message.Subject));
            Assert.That(before.TextBody, Is.EqualTo(message.TextBody));
            fixture.Storage.Setup(x => x.DeleteAccountAsync(account, CancellationToken.None)).ThrowsAsync(failure);

            var actual = await Assert.ThrowsAsync<IOException>(async () =>
                await fixture.Core.DeleteAccountAsync(account, CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);

            Assert.That(actual, Is.SameAs(failure));
            Assert.That(created, Is.EqualTo(1));
            Assert.That(OwnedCount(fixture.Core), Is.Zero);
            var stored = await fixture.Native.GetAccountAsync(account.Email).WaitAsync(TestTimeout).ConfigureAwait(false);
            Assert.That(stored.Id, Is.EqualTo(account.Id));
            Assert.That(stored.DefaultInboxFolder.Id, Is.EqualTo(account.DefaultInboxFolder.Id));
            Assert.That(await fixture.Native.IsMessageExistAsync(account.Email, account.DefaultInboxFolder.FullName, message.Id).WaitAsync(TestTimeout).ConfigureAwait(false), Is.False,
                "Messages removed by the completed folder cleanup must not be restored.");
            Assert.That(deleted, Is.Zero);
            fixture.Storage.Verify(x => x.GetAccountAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task FolderDeletionFailureDoesNotRestoreAReplacementAccount()
        {
            using var fixture = new CoreMailboxFixture();
            var account = CreateAccount();
            var replacement = CreateAccount();
            var failure = new IOException("The original account no longer exists");
            await fixture.InitializeAsync(account).ConfigureAwait(false);
            await Scheduler(fixture.Core, account).GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
            fixture.Storage.Setup(x => x.DeleteFolderAsync(account.Email, account.DefaultInboxFolder.FullName, CancellationToken.None))
                .Returns(async () =>
                {
                    await fixture.Native.DeleteAccountAsync(account).ConfigureAwait(false);
                    await fixture.Native.AddAccountAsync(replacement).ConfigureAwait(false);
                    throw failure;
                });

            var actual = await Assert.ThrowsAsync<IOException>(async () =>
                await fixture.Core.DeleteAccountAsync(account, CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);

            Assert.That(actual, Is.SameAs(failure));
            Assert.That(replacement.Id, Is.Not.EqualTo(account.Id));
            var stored = await fixture.Native.GetAccountAsync(account.Email).WaitAsync(TestTimeout).ConfigureAwait(false);
            Assert.That(stored.Id, Is.EqualTo(replacement.Id));
            Assert.That(stored.DefaultInboxFolder.Id, Is.EqualTo(replacement.DefaultInboxFolder.Id));
            Assert.That(OwnedCount(fixture.Core), Is.Zero);
            fixture.Security.Verify(x => x.RemovePgpKeys(It.IsAny<Account>()), Times.Never);
        }

        [Test]
        public async Task CancellationWhileResolvingAccountIdPreservesTheScheduler()
        {
            using var fixture = new CoreMailboxFixture();
            using var cancellation = new CancellationTokenSource();
            var account = CreateAccount();
            var reading = Signal();
            var release = Signal();
            int reads = 0;
            fixture.Receive = (_, _) =>
            {
                Interlocked.Increment(ref reads);
                return Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());
            };
            await fixture.InitializeAsync(account).ConfigureAwait(false);
            var scheduler = Scheduler(fixture.Core, account);
            await scheduler.GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
            fixture.Storage.Setup(x => x.GetAccountAsync(account.Email, cancellation.Token))
                .Returns(async () =>
                {
                    var stored = await fixture.Native.GetAccountAsync(account.Email, cancellation.Token).ConfigureAwait(false);
                    reading.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    return stored;
                });
            Task deletion = fixture.Core.DeleteAccountAsync(CreateAccount(), cancellation.Token);
            try
            {
                await reading.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                cancellation.Cancel();
                release.TrySetResult(true);
                await Assert.CatchAsync<OperationCanceledException>(async () => await deletion.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                Assert.That(Scheduler(fixture.Core, account), Is.SameAs(scheduler));
                Assert.That(scheduler.IsStopping(), Is.False);
                int beforeManualCheck = Volatile.Read(ref reads);
                await fixture.Core.CheckForNewInboxMessagesAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(Volatile.Read(ref reads), Is.EqualTo(beforeManualCheck + 1));
                var stored = await fixture.Native.GetAccountAsync(account.Email).WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(stored.Id, Is.EqualTo(account.Id));
                Assert.That(stored.DefaultInboxFolder.Id, Is.EqualTo(account.DefaultInboxFolder.Id));
                fixture.Storage.Verify(x => x.DeleteFolderAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                release.TrySetResult(true);
                try { await deletion.WaitAsync(TestTimeout).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            }
        }

        [Test]
        public async Task AlreadyCancelledAccountDeletionPreservesSynchronization()
        {
            using var fixture = new CoreMailboxFixture();
            using var cancellation = new CancellationTokenSource();
            var account = CreateAccount();
            int reads = 0;
            int deleted = 0;
            fixture.Receive = (_, _) =>
            {
                Interlocked.Increment(ref reads);
                return Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());
            };
            fixture.Core.AccountDeleted += (_, _) => ++deleted;
            await fixture.InitializeAsync(account).ConfigureAwait(false);
            await Scheduler(fixture.Core, account).GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
            int readsBeforeDeletion = Volatile.Read(ref reads);
            cancellation.Cancel();

            await Assert.CatchAsync<OperationCanceledException>(async () =>
                await fixture.Core.DeleteAccountAsync(account, cancellation.Token).WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);

            var stored = await fixture.Native.GetAccountAsync(account.Email).WaitAsync(TestTimeout).ConfigureAwait(false);
            Assert.That(stored.Id, Is.EqualTo(account.Id));
            Assert.That(stored.Email, Is.EqualTo(account.Email));
            Assert.That(stored.FoldersStructure.Select(x => x.Id), Is.EqualTo(account.FoldersStructure.Select(x => x.Id)));
            Assert.That(stored.DefaultInboxFolder.FullName, Is.EqualTo(account.DefaultInboxFolder.FullName));
            Assert.That(deleted, Is.Zero);
            await fixture.Core.CheckForNewInboxMessagesAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
            Assert.That(Volatile.Read(ref reads), Is.EqualTo(readsBeforeDeletion + 1), "Rejected deletion must leave mailbox synchronization running.");
            fixture.Storage.Verify(x => x.DeleteFolderAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            fixture.Storage.Verify(x => x.DeleteAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task AccountDeletionFinishesWhenCallerCancelsDuringSchedulerStop()
        {
            using var fixture = new CoreMailboxFixture();
            using var cancellation = new CancellationTokenSource();
            var account = CreateAccount();
            var entered = Signal();
            var schedulerCancelled = Signal();
            var release = Signal();
            int deleted = 0;
            fixture.Receive = async (_, token) =>
            {
                using (token.Register(() => schedulerCancelled.TrySetResult(true)))
                {
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                }
                return Array.Empty<Message>();
            };
            fixture.Core.AccountDeleted += (_, _) => ++deleted;
            Task deletion = Task.CompletedTask;
            try
            {
                await fixture.InitializeAsync(account).ConfigureAwait(false);
                await entered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                deletion = fixture.Core.DeleteAccountAsync(account, cancellation.Token);
                await schedulerCancelled.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                cancellation.Cancel();
                Assert.That(deletion.IsCompleted, Is.False, "Deletion must wait for the active mailbox check.");
                var stored = await fixture.Native.GetAccountAsync(account.Email).WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(stored.Id, Is.EqualTo(account.Id));
                Assert.That(stored.FoldersStructure.Select(x => x.Id), Is.EqualTo(account.FoldersStructure.Select(x => x.Id)));
                release.TrySetResult(true);
                await deletion.WaitAsync(TestTimeout).ConfigureAwait(false);

                Assert.That(await fixture.Native.ExistsAccountWithEmailAddressAsync(account.Email).WaitAsync(TestTimeout).ConfigureAwait(false), Is.False);
                Assert.That(deleted, Is.EqualTo(1));
                fixture.Storage.Verify(x => x.DeleteFolderAsync(account.Email, account.DefaultInboxFolder.FullName, CancellationToken.None), Times.Once);
                fixture.Storage.Verify(x => x.DeleteAccountAsync(account, CancellationToken.None), Times.Once);
            }
            finally
            {
                release.TrySetResult(true);
                try { await deletion.WaitAsync(TestTimeout).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RepeatedInitializationAndDeletionReclaimsSchedulersWhileCoreRemainsAlive(bool delete)
        {
            var account = CreateAccount();
            account.Id = 1;
            var storage = new Mock<IDataStorage>();
            storage.Setup(x => x.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Account> { account });
            storage.Setup(x => x.GetAccountAsync(account.Email, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            using var core = CoreTestBase.CreateCore(storage.Object);
            var counters = new List<DisposalCounter>();
            for (int i = 0; i < 3; ++i)
            {
                await core.InitializeApplicationAsync("password", CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false);
                var scheduler = Scheduler(core, account);
                counters.Add(new DisposalCounter(scheduler));
                await scheduler.GetActionTask().WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(OwnedCount(core), Is.EqualTo(1));
                if (delete)
                {
                    await core.DeleteAccountAsync(account, CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false);
                    Assert.That(OwnedCount(core), Is.Zero);
                    Assert.That(counters.Select(x => x.Count), Is.All.EqualTo(1));
                }
                else
                {
                    Assert.That(counters.Take(i).Select(x => x.Count), Is.All.EqualTo(1));
                }
            }
            await Task.Run(core.Dispose).WaitAsync(TestTimeout).ConfigureAwait(false);
            Assert.That(counters.Select(x => x.Count), Is.All.EqualTo(1));
            Assert.That(OwnedCount(core), Is.Zero);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public async Task CleanupWaitsForCancellationToReturnEvenWhenTheCheckHasFinished(int operationKind)
        {
            var account = CreateAccount();
            account.Id = 1;
            var storage = new Mock<IDataStorage>();
            storage.Setup(x => x.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Account> { account });
            storage.Setup(x => x.GetAccountAsync(account.Email, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            var mailbox = new Mock<IMailBox>();
            var entered = Signal();
            var cancellationEntered = Signal();
            var releaseCheck = Signal();
            using var releaseCancellation = new ManualResetEventSlim(false);
            CancellationToken capturedToken = default;
            CancellationTokenRegistration registration = default;
            int reads = 0;
            mailbox.Setup(x => x.GetMessagesAsync(It.IsAny<Folder>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(async (Folder folder, int count, CancellationToken token) =>
                {
                    if (Interlocked.Increment(ref reads) == 1)
                    {
                        capturedToken = token;
                        registration = token.Register(() =>
                        {
                            cancellationEntered.TrySetResult(true);
                            if (!releaseCancellation.Wait(TestTimeout))
                            {
                                throw new TimeoutException("The cancellation callback was not released.");
                            }
                        });
                        entered.TrySetResult(true);
                        await releaseCheck.Task.ConfigureAwait(false);
                    }
                    return (IReadOnlyList<Message>)Array.Empty<Message>();
                });
            using var core = CoreTestBase.CreateCore(storage.Object, mailbox.Object);
            Mock.Get(core.GetSecurityManager()).Setup(x => x.IsNeverStartedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
            Task operation = Task.CompletedTask;
            try
            {
                await core.InitializeApplicationAsync("password", CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false);
                await entered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                var scheduler = Scheduler(core, account);
                var active = scheduler.GetActionTask();
                var counter = new DisposalCounter(scheduler);
                _ = capturedToken.WaitHandle;
                operation = Task.Run(async () =>
                {
                    switch (operationKind)
                    {
                        case 0:
                            await core.ResetApplicationAsync().ConfigureAwait(false);
                            break;
                        case 1:
                            await core.InitializeApplicationAsync("password", CancellationToken.None).ConfigureAwait(false);
                            break;
                        case 2:
                            await core.DeleteAccountAsync(account, CancellationToken.None).ConfigureAwait(false);
                            break;
                        default:
                            core.Dispose();
                            break;
                    }
                });
                await cancellationEntered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                releaseCheck.TrySetResult(true);
                await active.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.DoesNotThrow(() => _ = capturedToken.WaitHandle, "CTS must remain alive until Cancel has returned.");
                Assert.That(counter.Count, Is.Zero);
                Assert.That(OwnedCount(core), Is.EqualTo(1));
                Assert.That(operation.IsCompleted, Is.False);
                releaseCancellation.Set();
                await operation.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(counter.Count, Is.EqualTo(1));
                Assert.Throws<ObjectDisposedException>(() => _ = capturedToken.WaitHandle);
                Assert.That(OwnedCount(core), Is.EqualTo(operationKind == 1 ? 1 : 0));
            }
            finally
            {
                releaseCheck.TrySetResult(true);
                releaseCancellation.Set();
                await operation.WaitAsync(TestTimeout).ConfigureAwait(false);
                await Task.Run(core.Dispose).WaitAsync(TestTimeout).ConfigureAwait(false);
                registration.Dispose();
            }
        }
    }
}
