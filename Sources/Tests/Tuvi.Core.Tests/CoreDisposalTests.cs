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
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Tuvi.Core.DataStorage;
using Tuvi.Core.DataStorage.Impl;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;
using Tuvi.Core.Mail;
using Tuvi.Core.Tests.Infrastructure;
using TuviPgpLib.Entities;

namespace Tuvi.Core.Tests
{
    [NonParallelizable]
    public class CoreDisposalTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

        private static TaskCompletionSource<bool> Signal()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static Account CreateAccount()
        {
            var account = CoreTestBase.GetTestEmailAccount();
            account.SynchronizationInterval = 0;
            account.FoldersStructure.RemoveAt(1);
            return account;
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task MessageHandlerDisposeIsRejectedWhileExternalDisposeWaitsForTheHandler(bool externalFirst)
        {
            string path = Path.Combine(Path.GetTempPath(), "TuviCore-dispose-" + Guid.NewGuid().ToString("N") + ".db");
            using var nativeStorage = DataStorageProvider.GetDataStorage(path);
            var entered = Signal();
            var returned = Signal();
            var cancelled = Signal();
            var externalEntered = Signal();
            using var callDispose = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            CancellationTokenRegistration cancellationRegistration = default;
            Task check = Task.CompletedTask;
            Task external = Task.CompletedTask;
            TuviMail core = null;
            try
            {
                await nativeStorage.CreateAsync("disposal").WaitAsync(Timeout).ConfigureAwait(false);
                var account = CreateAccount();
                await nativeStorage.AddAccountAsync(account).WaitAsync(Timeout).ConfigureAwait(false);
                var message = new Message { Id = 7, Folder = account.DefaultInboxFolder, Subject = "Shutdown message", TextBody = "Persisted before shutdown" };
                var storage = ForwardStorage(nativeStorage);
                var mailbox = new Mock<IMailBox>();
                int reads = 0;
                mailbox.Setup(x => x.GetMessagesAsync(It.IsAny<Folder>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .Returns(async (Folder folder, int count, CancellationToken token) =>
                    {
                        await Task.Yield();
                        if (Interlocked.Increment(ref reads) == 1)
                        {
                            return (IReadOnlyList<Message>)Array.Empty<Message>();
                        }
                        cancellationRegistration = token.Register(() => cancelled.TrySetResult(true));
                        return new[] { message };
                    });
                core = CoreTestBase.CreateCore(storage.Object, mailbox.Object);
                var credentials = Mock.Get(core.CredentialsManager);
                void DisposeExternally()
                {
                    externalEntered.TrySetResult(true);
                    core.Dispose();
                    storage.Verify(x => x.Dispose(), Times.Once, "An external Dispose must return only after storage cleanup.");
                    credentials.Verify(x => x.Dispose(), Times.Once);
                }
                core.MessagesReceived += (_, _) =>
                {
                    entered.TrySetResult(true);
                    if (!callDispose.Wait(Timeout))
                    {
                        throw new TimeoutException("The handler was not allowed to dispose the core.");
                    }
                    Assert.Throws<InvalidOperationException>(core.Dispose);
                    returned.TrySetResult(true);
                    if (!release.Wait(Timeout))
                    {
                        throw new TimeoutException("The message handler was not released.");
                    }
                };
                await core.InitializeApplicationAsync("disposal", CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
                check = core.CheckForNewInboxMessagesAsync();
                await entered.Task.WaitAsync(Timeout).ConfigureAwait(false);

                if (externalFirst)
                {
                    external = Task.Run(DisposeExternally);
                    await cancelled.Task.WaitAsync(Timeout).ConfigureAwait(false);
                }
                callDispose.Set();
                await returned.Task.WaitAsync(Timeout).ConfigureAwait(false);
                if (!externalFirst)
                {
                    external = Task.Run(DisposeExternally);
                    await externalEntered.Task.WaitAsync(Timeout).ConfigureAwait(false);
                }

                Assert.That(check.IsCompleted, Is.False);
                Assert.That(external.IsCompleted, Is.False);
                storage.Verify(x => x.Dispose(), Times.Never);
                credentials.Verify(x => x.Dispose(), Times.Never);
                var stored = await nativeStorage.GetMessageAsync(account.Email, account.DefaultInboxFolder.FullName, message.Id)
                    .WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(stored.Subject, Is.EqualTo(message.Subject));
                Assert.That(stored.TextBody, Is.EqualTo(message.TextBody));

                release.Set();
                await Task.WhenAll(check, external).WaitAsync(Timeout).ConfigureAwait(false);
                core.Dispose();
                storage.Verify(x => x.Dispose(), Times.Once);
                credentials.Verify(x => x.Dispose(), Times.Once);
                Assert.That(reads, Is.EqualTo(2), "The automatic check must be empty and the forced check must receive the message.");
                using var reopened = DataStorageProvider.GetDataStorage(path);
                await reopened.OpenAsync("disposal").WaitAsync(Timeout).ConfigureAwait(false);
                stored = await reopened.GetMessageAsync(account.Email, account.DefaultInboxFolder.FullName, message.Id)
                    .WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(stored.Subject, Is.EqualTo(message.Subject));
                Assert.That(stored.TextBody, Is.EqualTo(message.TextBody));
            }
            finally
            {
                callDispose.Set();
                release.Set();
                cancellationRegistration.Dispose();
                try
                {
                    if (core != null)
                    {
                        await Task.Run(core.Dispose).WaitAsync(Timeout).ConfigureAwait(false);
                    }
                    await Task.WhenAll(check, external).WaitAsync(Timeout).ConfigureAwait(false);
                }
                finally
                {
                    nativeStorage.Dispose();
                    foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
                    {
                        File.Delete(path + suffix);
                    }
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Core disposal is scheduled and awaited in finally with a timeout so regressions cannot hang the test host.")]
        [TestCase(false)]
        [TestCase(true)]
        public async Task RemovedSchedulerRemainsOwnedUntilItsHandlerCompletes(bool reset)
        {
            var account = CreateAccount();
            var storage = new Mock<IDataStorage>();
            int reads = 0;
            var cancelled = Signal();
            CancellationTokenRegistration registration = default;
            var core = CreateCore(storage, account, token =>
            {
                if (++reads == 1) return Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());
                registration = token.Register(() => cancelled.TrySetResult(true));
                return Task.FromResult<IReadOnlyList<Message>>(new[] { new Message { Id = 7, Folder = account.DefaultInboxFolder } });
            });
            var credentials = Mock.Get(core.CredentialsManager);
            var returned = Signal();
            var rejected = Signal();
            using var tryDispose = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            Task check = Task.CompletedTask;
            Task external = Task.CompletedTask;
            Task deletion = Task.CompletedTask;
            core.MessagesReceived += (_, _) =>
            {
                if (reset)
                {
                    Assert.Throws<InvalidOperationException>(() => core.ResetApplicationAsync().WaitAsync(Timeout).GetAwaiter().GetResult(),
                        "A message handler must request reset through the core owner and return.");
                }
                returned.TrySetResult(true);
                if (!tryDispose.Wait(Timeout)) throw new TimeoutException("The handler was not allowed to attempt disposal.");
                Assert.Throws<InvalidOperationException>(core.Dispose);
                rejected.TrySetResult(true);
                if (!release.Wait(Timeout))
                {
                    throw new TimeoutException("The retired scheduler's handler was not released.");
                }
            };
            try
            {
                await core.InitializeApplicationAsync("password", CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
                check = core.CheckForNewInboxMessagesAsync();
                await returned.Task.WaitAsync(Timeout).ConfigureAwait(false);
                if (reset) external = Task.Run(core.Dispose);
                else deletion = core.DeleteAccountAsync(account, CancellationToken.None);
                await cancelled.Task.WaitAsync(Timeout).ConfigureAwait(false);
                tryDispose.Set();
                await rejected.Task.WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(core.OwnedSchedulerCount, Is.EqualTo(1));
                if (!reset)
                {
                    Assert.That(deletion.IsCompleted, Is.False);
                    storage.Verify(x => x.DeleteAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
                    external = Task.Run(core.Dispose);
                }
                storage.Verify(x => x.Dispose(), Times.Never);
                credentials.Verify(x => x.Dispose(), Times.Never);
                Assert.That(check.IsCompleted, Is.False);
                release.Set();
                await Task.WhenAll(check, deletion, external).WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(core.OwnedSchedulerCount, Is.Zero);
                storage.Verify(x => x.DeleteAccountAsync(account, CancellationToken.None), reset ? Times.Never() : Times.Once());
                storage.Verify(x => x.Dispose(), Times.Once);
                credentials.Verify(x => x.Dispose(), Times.Once);
            }
            finally
            {
                tryDispose.Set();
                release.Set();
                await Task.Run(core.Dispose).WaitAsync(Timeout).ConfigureAwait(false);
                await Task.WhenAll(check, deletion, external).WaitAsync(Timeout).ConfigureAwait(false);
                registration.Dispose();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Core disposal is scheduled and awaited in finally with a timeout so regressions cannot hang the test host.")]
        [Test]
        public async Task ChildOfCompletedCheckMustWaitForAnotherActiveCheck()
        {
            var account = CreateAccount();
            var storage = new Mock<IDataStorage>();
            var entered = Signal();
            var cancelled = Signal();
            var release = Signal();
            var startChild = Signal();
            int reads = 0;
            var core = CreateCore(storage, account, async token =>
            {
                switch (Interlocked.Increment(ref reads))
                {
                    case 1:
                        return Array.Empty<Message>();
                    case 2:
                        return new[] { new Message { Id = 7, Folder = account.DefaultInboxFolder } };
                    default:
                        using (token.Register(() => cancelled.TrySetResult(true)))
                        {
                            entered.TrySetResult(true);
                            await release.Task.ConfigureAwait(false);
                        }
                        return Array.Empty<Message>();
                }
            });
            var credentials = Mock.Get(core.CredentialsManager);
            Task child = Task.CompletedTask;
            Task check = Task.CompletedTask;
            core.MessagesReceived += (_, _) => child = Task.Run(async () =>
            {
                await startChild.Task.ConfigureAwait(false);
                core.Dispose();
                storage.Verify(x => x.Dispose(), Times.Once, "An inherited marker from a completed check must not skip waiting.");
                credentials.Verify(x => x.Dispose(), Times.Once);
            });
            try
            {
                await core.InitializeApplicationAsync("password", CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
                await core.CheckForNewInboxMessagesAsync().WaitAsync(Timeout).ConfigureAwait(false);
                check = core.CheckForNewInboxMessagesAsync();
                await entered.Task.WaitAsync(Timeout).ConfigureAwait(false);
                startChild.TrySetResult(true);
                await cancelled.Task.WaitAsync(Timeout).ConfigureAwait(false);
                storage.Verify(x => x.Dispose(), Times.Never);
                Assert.That(child.IsCompleted, Is.False);
                release.TrySetResult(true);
                await Task.WhenAll(check, child).WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(reads, Is.EqualTo(3));
            }
            finally
            {
                startChild.TrySetResult(true);
                release.TrySetResult(true);
                await Task.Run(core.Dispose).WaitAsync(Timeout).ConfigureAwait(false);
                await Task.WhenAll(check, child).WaitAsync(Timeout).ConfigureAwait(false);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Core disposal is scheduled and awaited in finally with a timeout so regressions cannot hang the test host.")]
        [Test]
        public async Task HandlerOfAnotherCoreMustWaitForTheDisposedCoresCheck()
        {
            var account = CreateAccount();
            var storage = new Mock<IDataStorage>();
            var entered = Signal();
            var cancelled = Signal();
            var release = Signal();
            var otherCore = CreateCore(storage, account, async token =>
            {
                using (token.Register(() => cancelled.TrySetResult(true)))
                {
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                }
                return Array.Empty<Message>();
            });
            var ownStorage = new Mock<IDataStorage>();
            int reads = 0;
            var core = CreateCore(ownStorage, account, _ => Task.FromResult<IReadOnlyList<Message>>(++reads == 1
                ? Array.Empty<Message>() : new[] { new Message { Id = 7, Folder = account.DefaultInboxFolder } }));
            bool storageClosedAtReturn = false;
            storage.Setup(x => x.Dispose()).Callback(() => storageClosedAtReturn = true);
            bool waitedForStorage = false;
            core.MessagesReceived += (_, _) =>
            {
                otherCore.Dispose();
                waitedForStorage = storageClosedAtReturn;
            };
            Task check = Task.CompletedTask;
            try
            {
                await otherCore.InitializeApplicationAsync("password", CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
                await entered.Task.WaitAsync(Timeout).ConfigureAwait(false);
                await core.InitializeApplicationAsync("password", CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
                check = core.CheckForNewInboxMessagesAsync();
                await cancelled.Task.WaitAsync(Timeout).ConfigureAwait(false);
                storage.Verify(x => x.Dispose(), Times.Never);
                Assert.That(check.IsCompleted, Is.False);
                release.TrySetResult(true);
                await check.WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(waitedForStorage, Is.True, "The current check belongs to another Core.");
                ownStorage.Verify(x => x.Dispose(), Times.Never);
            }
            finally
            {
                release.TrySetResult(true);
                await Task.WhenAll(Task.Run(core.Dispose), Task.Run(otherCore.Dispose)).WaitAsync(Timeout).ConfigureAwait(false);
                await check.WaitAsync(Timeout).ConfigureAwait(false);
            }
            storage.Verify(x => x.Dispose(), Times.Once);
            ownStorage.Verify(x => x.Dispose(), Times.Once);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Core disposal is scheduled and awaited in finally with a timeout so regressions cannot hang the test host.")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task CleanupFailureIsRetainedAndDoesNotSkipOtherResources(int failureStage)
        {
            var account = CreateAccount();
            var storage = new Mock<IDataStorage>();
            var entered = Signal();
            var cancelled = Signal();
            var release = Signal();
            var failure = new IOException("Cleanup failed");
            TuviMail core = null;
            core = CreateCore(storage, account, async token =>
            {
                using (token.Register(() =>
                {
                    cancelled.TrySetResult(true);
                    if (failureStage == 0)
                    {
                        throw failure;
                    }
                }))
                {
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                }
                return Array.Empty<Message>();
            });
            var credentials = Mock.Get(core.CredentialsManager);
            if (failureStage == 1)
            {
                credentials.Setup(x => x.Dispose()).Throws(failure);
            }
            if (failureStage == 2)
            {
                storage.Setup(x => x.Dispose()).Throws(failure);
            }
            Task external = null;
            try
            {
                await core.InitializeApplicationAsync("password", CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
                await entered.Task.WaitAsync(Timeout).ConfigureAwait(false);
                external = Task.Run(core.Dispose);
                await cancelled.Task.WaitAsync(Timeout).ConfigureAwait(false);
                storage.Verify(x => x.Dispose(), Times.Never);
                release.TrySetResult(true);
                var actual = await Assert.CatchAsync<Exception>(async () =>
                    await external.WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);
                var repeated = await Assert.CatchAsync<Exception>(async () =>
                    await Task.Run(core.Dispose).WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);
                Assert.That(repeated, Is.SameAs(actual));
                if (failureStage == 0)
                {
                    Assert.That(((AggregateException)actual).InnerExceptions, Is.EqualTo(new[] { failure }));
                }
                else
                {
                    Assert.That(actual, Is.SameAs(failure));
                }
                storage.Verify(x => x.Dispose(), Times.Once);
                credentials.Verify(x => x.Dispose(), Times.Once);
                await Assert.ThrowsAsync<ObjectDisposedException>(() => core.GetAccountsAsync(CancellationToken.None)).ConfigureAwait(false);
            }
            finally
            {
                release.TrySetResult(true);
                await Assert.CatchAsync<Exception>(async () =>
                    await Task.Run(core.Dispose).WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);
                if (external != null)
                {
                    await Assert.CatchAsync<Exception>(async () =>
                        await external.WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);
                }
            }
        }

        [Test]
        public async Task InitializationCannotRegisterASchedulerAfterDisposal()
        {
            var entered = Signal();
            var accounts = new TaskCompletionSource<List<Account>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var storage = new Mock<IDataStorage>();
            storage.Setup(x => x.GetAccountsAsync(It.IsAny<CancellationToken>())).Returns(() =>
            {
                entered.TrySetResult(true);
                return accounts.Task;
            });
            var mailbox = new Mock<IMailBox>();
            using var core = CoreTestBase.CreateCore(storage.Object, mailbox.Object);
            var initialization = core.InitializeApplicationAsync("password", CancellationToken.None);
            try
            {
                await entered.Task.WaitAsync(Timeout).ConfigureAwait(false);
                await Task.Run(core.Dispose).WaitAsync(Timeout).ConfigureAwait(false);
            }
            finally
            {
                accounts.TrySetResult(new List<Account> { CreateAccount() });
            }
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await initialization.WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);
            mailbox.Verify(x => x.GetMessagesAsync(It.IsAny<Folder>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            storage.Verify(x => x.Dispose(), Times.Once);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Core disposal is scheduled and awaited in finally with a timeout.")]
        [TestCase(false, false, false, false)]
        [TestCase(true, true, false, false)]
        [TestCase(false, false, true, false)]
        [TestCase(false, true, true, false)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, true, false)]
        [TestCase(false, true, true, true)]
        [TestCase(true, false, true, true)]
        public async Task CancellationCallbackRejectsShutdownOfItsCore(bool outerReset, bool innerReset, bool finishActionFirst, bool dispatchCancellation)
        {
            var account = CreateAccount();
            var storage = new Mock<IDataStorage>();
            var entered = Signal();
            var cancellationEntered = Signal();
            var rejected = Signal();
            var releaseCheck = Signal();
            using var tryShutdown = new ManualResetEventSlim(false);
            using var releaseCancellation = new ManualResetEventSlim(false);
            CancellationTokenRegistration registration = default;
            var context = new CancellationDispatchContext();
            TuviMail core = null;
            core = CreateCore(storage, account, async token =>
            {
                var previous = SynchronizationContext.Current;
                try
                {
                    if (dispatchCancellation) SynchronizationContext.SetSynchronizationContext(context);
                    registration = token.Register(() =>
                    {
                        cancellationEntered.TrySetResult(true);
                        if (!tryShutdown.Wait(Timeout)) throw new TimeoutException("The cancellation callback was not resumed.");
                        if (innerReset) Assert.Throws<InvalidOperationException>(() => core.ResetApplicationAsync());
                        else Assert.Throws<InvalidOperationException>(core.Dispose);
                        rejected.TrySetResult(true);
                        if (!releaseCancellation.Wait(Timeout)) throw new TimeoutException("The cancellation callback was not released.");
                    }, useSynchronizationContext: dispatchCancellation);
                }
                finally { SynchronizationContext.SetSynchronizationContext(previous); }
                entered.TrySetResult(true);
                await releaseCheck.Task.ConfigureAwait(false);
                return Array.Empty<Message>();
            });
            var credentials = Mock.Get(core.CredentialsManager);
            var security = Mock.Get(core.GetSecurityManager());
            Task operation = Task.CompletedTask;
            Task check = Task.CompletedTask;
            try
            {
                await core.InitializeApplicationAsync("password", default).WaitAsync(Timeout).ConfigureAwait(false);
                await entered.Task.WaitAsync(Timeout).ConfigureAwait(false);
                check = core.GetAccountScheduler(account.Email).GetActionTask();
                operation = outerReset ? core.ResetApplicationAsync() : Task.Run(core.Dispose);
                await cancellationEntered.Task.WaitAsync(Timeout).ConfigureAwait(false);
                if (finishActionFirst)
                {
                    releaseCheck.TrySetResult(true);
                    await check.WaitAsync(Timeout).ConfigureAwait(false);
                }
                tryShutdown.Set();
                await rejected.Task.WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(operation.IsCompleted, Is.False);
                storage.Verify(x => x.Dispose(), Times.Never);
                security.Verify(x => x.ResetAsync(), Times.Never);
                releaseCheck.TrySetResult(true);
                releaseCancellation.Set();
                await Task.WhenAll(check, operation).WaitAsync(Timeout).ConfigureAwait(false);
                security.Verify(x => x.ResetAsync(), outerReset ? Times.Once() : Times.Never());
                Assert.That(context.Sends, Is.EqualTo(dispatchCancellation ? 1 : 0));
                await Task.Run(core.Dispose).WaitAsync(Timeout).ConfigureAwait(false);
                storage.Verify(x => x.Dispose(), Times.Once);
                credentials.Verify(x => x.Dispose(), Times.Once);
            }
            finally
            {
                tryShutdown.Set();
                releaseCheck.TrySetResult(true);
                releaseCancellation.Set();
                await Task.WhenAll(check, operation, Task.Run(core.Dispose)).WaitAsync(Timeout).ConfigureAwait(false);
                registration.Dispose();
            }
        }

        private sealed class CancellationDispatchContext : SynchronizationContext
        {
            public int Sends;
            public override void Send(SendOrPostCallback callback, object state)
            {
                Interlocked.Increment(ref Sends);
                var completion = Signal();
                var thread = new Thread(() =>
                {
                    try { callback(state); completion.TrySetResult(true); }
#pragma warning disable CA1031 // Propagate callback failures to the thread performing synchronous cancellation.
                    catch (Exception ex) { completion.TrySetException(ex); }
#pragma warning restore CA1031
                })
                { IsBackground = true };
                thread.Start();
                completion.Task.WaitAsync(Timeout).GetAwaiter().GetResult();
            }
        }

        private sealed class NonPumpingContext : SynchronizationContext
        {
            public int Posts;
            public override void Post(SendOrPostCallback callback, object state)
            {
                Interlocked.Increment(ref Posts);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Core disposal is scheduled and awaited in finally with a timeout.")]
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public async Task UiDisposeWaitsForAcceptedResetAndRetainsOnlyItsOwnCleanupFailure(bool failReset, bool failClose)
        {
            var storage = new Mock<IDataStorage>();
            var core = CoreTestBase.CreateCore(storage.Object);
            var credentials = Mock.Get(core.CredentialsManager);
            var security = Mock.Get(core.GetSecurityManager());
            var entered = Signal();
            var release = Signal();
            var disposing = Signal();
            var finished = Signal();
            var resetFailure = new IOException("Reset deletion failed");
            var closeFailure = new IOException("Storage close failed");
            var context = new NonPumpingContext();
            security.Setup(x => x.ResetAsync()).Returns(async () =>
            {
                entered.TrySetResult(true);
                await release.Task.ConfigureAwait(true);
                if (failReset) throw resetFailure;
            });
            if (failClose) storage.Setup(x => x.Dispose()).Throws(closeFailure);
            Task reset = Task.CompletedTask;
            var ui = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(context);
                try
                {
                    reset = core.ResetApplicationAsync();
                    disposing.TrySetResult(true);
                    core.Dispose();
                    finished.TrySetResult(true);
                }
#pragma warning disable CA1031 // Report every worker-thread failure through the task observed by the test.
                catch (Exception ex) { finished.TrySetException(ex); }
#pragma warning restore CA1031
                finally { SynchronizationContext.SetSynchronizationContext(null); }
            })
            { IsBackground = true };
            try
            {
                ui.Start();
                await Task.WhenAll(entered.Task, disposing.Task).WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(finished.Task.IsCompleted, Is.False);
                storage.Verify(x => x.Dispose(), Times.Never);
                release.TrySetResult(true);
                if (failClose)
                {
                    var actual = await Assert.ThrowsAsync<IOException>(async () => await finished.Task.WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);
                    Assert.That(actual, Is.SameAs(closeFailure));
                }
                else await finished.Task.WaitAsync(Timeout).ConfigureAwait(false);
                if (failReset)
                {
                    var actual = await Assert.ThrowsAsync<IOException>(async () => await reset.WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);
                    Assert.That(actual, Is.SameAs(resetFailure));
                }
                else await reset.WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(context.Posts, Is.Zero, "Internal shutdown must not depend on a blocked caller's context.");
                Assert.That(ui.Join(Timeout), Is.True);
                storage.Verify(x => x.Dispose(), Times.Once);
                credentials.Verify(x => x.Dispose(), Times.Once);
            }
            finally
            {
                release.TrySetResult(true);
                try { await finished.Task.WaitAsync(Timeout).ConfigureAwait(false); }
                catch (IOException ex) when (ReferenceEquals(ex, closeFailure)) { }
                try { await Task.Run(core.Dispose).WaitAsync(Timeout).ConfigureAwait(false); }
                catch (IOException ex) when (ReferenceEquals(ex, closeFailure)) { }
            }
        }

        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        public async Task ResetAndDisposeOrderingProtectsTheNextSqlCipherOwner(bool resetFirst, bool failReset)
        {
            using var fixture = new CoreMailboxFixture();
            var account = CreateAccount();
            var entered = Signal();
            var cancelled = Signal();
            var releaseCheck = Signal();
            var resettingStorage = Signal();
            var releaseReset = Signal();
            fixture.Receive = async (_, token) =>
            {
                using (token.Register(() => cancelled.TrySetResult(true)))
                {
                    entered.TrySetResult(true);
                    await releaseCheck.Task.ConfigureAwait(false);
                }
                return Array.Empty<Message>();
            };
            var security = fixture.Security;
            var failure = new IOException("Reset failed after closing storage");
            security.Setup(x => x.ResetAsync()).Returns(async () =>
            {
                resettingStorage.TrySetResult(true);
                await releaseReset.Task.ConfigureAwait(false);
                await fixture.Native.ResetAsync().ConfigureAwait(false);
                if (failReset) throw failure;
            });
            Task reset = Task.CompletedTask;
            Task disposal = Task.CompletedTask;
            Task external = Task.CompletedTask;
            fixture.Storage.Setup(x => x.Dispose()).Callback(() =>
            {
                if (resetFirst) Assert.That(reset.IsCompleted, Is.True, "Storage cleanup must follow the accepted reset result.");
                fixture.Native.Dispose();
            });
            try
            {
                await fixture.InitializeAsync(account).ConfigureAwait(false);
                await entered.Task.WaitAsync(Timeout).ConfigureAwait(false);
                if (resetFirst) reset = fixture.Core.ResetApplicationAsync();
                disposal = fixture.Core.BeginDispose();
                external = Task.Run(fixture.Core.Dispose);
                await cancelled.Task.WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(disposal.IsCompleted, Is.False);
                security.Verify(x => x.ResetAsync(), Times.Never, "The old check has not finished yet.");
                fixture.Storage.Verify(x => x.Dispose(), Times.Never);
                if (!resetFirst) Assert.Throws<ObjectDisposedException>(() => fixture.Core.ResetApplicationAsync());
                releaseCheck.TrySetResult(true);
                if (resetFirst)
                {
                    await resettingStorage.Task.WaitAsync(Timeout).ConfigureAwait(false);
                    Assert.That(disposal.IsCompleted, Is.False, "Dispose must also wait before reset is accepted in storage.");
                    fixture.Storage.Verify(x => x.Dispose(), Times.Never);
                    releaseReset.TrySetResult(true);
                    if (failReset)
                    {
                        var actual = await Assert.ThrowsAsync<IOException>(async () => await reset.WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);
                        Assert.That(actual, Is.SameAs(failure));
                    }
                    else await reset.WaitAsync(Timeout).ConfigureAwait(false);
                }
                await Task.WhenAll(disposal, external).WaitAsync(Timeout).ConfigureAwait(false);
                fixture.Storage.Verify(x => x.Dispose(), Times.Once);
                fixture.Credentials.Verify(x => x.Dispose(), Times.Once);
                Assert.That(File.Exists(fixture.FilePath), Is.EqualTo(!resetFirst));
                if (!resetFirst) File.Delete(fixture.FilePath);
                using var next = DataStorageProvider.GetDataStorage(fixture.FilePath);
                await next.CreateAsync("next-owner").WaitAsync(Timeout).ConfigureAwait(false);
                var replacement = CreateAccount();
                await next.AddAccountAsync(replacement).WaitAsync(Timeout).ConfigureAwait(false);
                var message = new Message { Id = 91, Folder = replacement.DefaultInboxFolder, Subject = "New owner", TextBody = "Preserve the replacement database" };
                await next.AddMessageAsync(replacement.Email, message).WaitAsync(Timeout).ConfigureAwait(false);
                next.SavePgpPublicKeys(new PgpPublicKeyBundle { Data = new byte[] { 1, 4, 9 } });
                for (int i = 0; i < 2; ++i)
                {
                    if (resetFirst)
                    {
                        var repeated = fixture.Core.ResetApplicationAsync();
                        Assert.That(repeated, Is.SameAs(reset));
                        if (failReset)
                        {
                            var actual = await Assert.ThrowsAsync<IOException>(async () => await repeated.WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);
                            Assert.That(actual, Is.SameAs(failure));
                        }
                        else await repeated.WaitAsync(Timeout).ConfigureAwait(false);
                    }
                    else Assert.Throws<ObjectDisposedException>(() => fixture.Core.ResetApplicationAsync());
                    await Task.Run(fixture.Core.Dispose).WaitAsync(Timeout).ConfigureAwait(false);
                }
                var stored = await next.GetMessageAsync(replacement.Email, message.Folder.FullName, message.Id).WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(stored.Subject, Is.EqualTo(message.Subject));
                Assert.That(stored.TextBody, Is.EqualTo(message.TextBody));
                Assert.That((await next.GetPgpPublicKeysAsync().WaitAsync(Timeout).ConfigureAwait(false)).Data, Is.EqualTo(new byte[] { 1, 4, 9 }));
                security.Verify(x => x.ResetAsync(), resetFirst ? Times.Once() : Times.Never());
            }
            finally
            {
                releaseCheck.TrySetResult(true);
                releaseReset.TrySetResult(true);
                try { await reset.WaitAsync(Timeout).ConfigureAwait(false); }
                catch (IOException ex) when (ReferenceEquals(ex, failure)) { }
                await Task.WhenAll(disposal, external, Task.Run(fixture.Core.Dispose)).WaitAsync(Timeout).ConfigureAwait(false);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ClosingRejectsNewProfileServiceRequestsBeforeStorageReset(bool dispose)
        {
            using var fixture = new CoreMailboxFixture();
            var account = CreateAccount();
            var entered = Signal();
            var release = Signal();
            var security = Mock.Get(fixture.Core.GetSecurityManager());
            var backup = fixture.Core.GetBackupManager();
            var ai = fixture.Core.GetAIAgentsStorage();
            var credentials = Mock.Get(fixture.Core.CredentialsManager);
            fixture.Receive = async (_, _) =>
            {
                entered.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                return Array.Empty<Message>();
            };
            Task closing = Task.CompletedTask;
            try
            {
                await fixture.InitializeAsync(account).ConfigureAwait(false);
                await entered.Task.WaitAsync(Timeout).ConfigureAwait(false);
                closing = dispose ? fixture.Core.BeginDispose() : fixture.Core.ResetApplicationAsync();
                Assert.Throws<ObjectDisposedException>(() => fixture.Core.GetSecurityManager());
                Assert.Throws<ObjectDisposedException>(() => fixture.Core.GetBackupManager());
                Assert.Throws<ObjectDisposedException>(() => fixture.Core.GetAIAgentsStorage());
                Assert.Throws<ObjectDisposedException>(() => _ = fixture.Core.CredentialsManager);
                Assert.That(closing.IsCompleted, Is.False);
                security.Verify(x => x.ResetAsync(), Times.Never);
                credentials.Verify(x => x.Dispose(), Times.Never);
                Assert.That(backup, Is.Not.Null);
                Assert.That(ai, Is.Not.Null);
                release.TrySetResult(true);
                await closing.WaitAsync(Timeout).ConfigureAwait(false);
                await Task.Run(fixture.Core.Dispose).WaitAsync(Timeout).ConfigureAwait(false);
                security.Verify(x => x.ResetAsync(), dispose ? Times.Never() : Times.Once());
                credentials.Verify(x => x.Dispose(), Times.Once);
            }
            finally
            {
                release.TrySetResult(true);
                await closing.WaitAsync(Timeout).ConfigureAwait(false);
            }
        }

        private static TuviMail CreateCore(Mock<IDataStorage> storage, Account account,
            Func<CancellationToken, Task<IReadOnlyList<Message>>> receive)
        {
            account.Id = 1;
            storage.Setup(x => x.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Account> { account });
            storage.Setup(x => x.GetAccountAsync(account.Email, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            var mailbox = new Mock<IMailBox>();
            mailbox.Setup(x => x.GetMessagesAsync(It.IsAny<Folder>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns((Folder folder, int count, CancellationToken token) => receive(token));
            return CoreTestBase.CreateCore(storage.Object, mailbox.Object);
        }

        private static Mock<IDataStorage> ForwardStorage(IDataStorage storage)
        {
            var forwarded = new Mock<IDataStorage>();
            forwarded.Setup(x => x.GetAccountsAsync(It.IsAny<CancellationToken>())).Returns(storage.GetAccountsAsync);
            forwarded.Setup(x => x.GetLatestMessageAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(storage.GetLatestMessageAsync);
            forwarded.Setup(x => x.GetEarliestMessageAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((EmailAddress email, string folder, CancellationToken token) => storage.GetEarliestMessageAsync(email, folder, token));
            forwarded.Setup(x => x.IsMessageExistAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<CancellationToken>()))
                .Returns(storage.IsMessageExistAsync);
            forwarded.Setup(x => x.AddMessageListAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<Message>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(storage.AddMessageListAsync);
            forwarded.Setup(x => x.Dispose()).Callback(storage.Dispose);
            return forwarded;
        }
    }
}
