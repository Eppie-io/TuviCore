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
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;
using Tuvi.Core.Mail;

namespace Tuvi.Core.Tests
{
    public class StorageResetHandlingTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

        private static IEnumerable<TestCaseData> StorageFailures()
        {
            yield return new TestCaseData(new DataBaseResetException("Storage was reset"), false);
            yield return new TestCaseData(new DataBaseException("Storage failed"), true);
            yield return new TestCaseData(new IOException("File access failed"), true);
        }

        [Test]
        public async Task SuccessfulStartThenResetDuringAccountLoadingPropagatesReset()
        {
            var storage = new Mock<IDataStorage>();
            using var core = CoreTestBase.CreateCore(storage.Object);
            var security = Mock.Get(core.GetSecurityManager());
            security.Setup(x => x.StartAsync("password", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var failure = new DataBaseResetException("Reset after successful StartAsync");
            storage.Setup(x => x.GetAccountsAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);

            var actual = await Assert.CatchAsync<DataBaseResetException>(async () =>
                await core.InitializeApplicationAsync("password", default).WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);

            Assert.That(actual, Is.SameAs(failure));
            security.Verify(x => x.StartAsync("password", It.IsAny<CancellationToken>()), Times.Once);
            storage.Verify(x => x.GetAccountsAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ResetBeforeFirstStartPreservesPreparedSeed()
        {
            var storage = new Mock<IDataStorage>();
            using var core = CoreTestBase.CreateCore(storage.Object);
            var security = Mock.Get(core.GetSecurityManager());
            var preparedSeed = new Mock<ISeedQuiz>().Object;
            security.Setup(x => x.IsNeverStartedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            security.Setup(x => x.GetSeedQuiz()).Returns(preparedSeed);
            bool wipeRequested = false;
            core.WipeAllDataNeeded += (_, _) => wipeRequested = true;
            int contacts = 0;
            core.ContactAdded += (_, _) => ++contacts;
            core.ContactChanged += (_, _) => ++contacts;

            await core.ResetApplicationAsync().WaitAsync(Timeout).ConfigureAwait(false);

            var contact = new Contact("Initial setup", new EmailAddress("setup@example.test"));
            storage.Raise(x => x.ContactAdded += null, new ContactAddedEventArgs(contact));
            storage.Raise(x => x.ContactChanged += null, new ContactChangedEventArgs(contact));

            Assert.That(core.GetSecurityManager().GetSeedQuiz(), Is.SameAs(preparedSeed));
            Assert.That(contacts, Is.EqualTo(2), "The initial no-file reset must keep contact admission open.");
            Assert.That(wipeRequested, Is.False);
            security.Verify(x => x.ResetAsync(), Times.Never, "Initial setup must not clear the prepared seed.");
            storage.Verify(x => x.ResetAsync(), Times.Never, "The high-level no-file reset must remain a no-op.");
        }

        [TestCaseSource(nameof(StorageFailures))]
        public async Task SchedulerReportsOnlyUnexpectedStorageFailuresAndCanRunAgain(Exception failure, bool reported)
        {
            int calls = 0;
            using var scheduler = new SchedulerWithTimer(_ => ++calls == 1
                ? Task.FromException(failure)
                : Task.CompletedTask, 0);
            var exceptions = new List<Exception>();
            scheduler.ExceptionOccurred += (_, args) => exceptions.Add(args.Exception);

            await scheduler.ExecuteActionForceAsync().WaitAsync(Timeout).ConfigureAwait(false);
            await scheduler.ExecuteActionForceAsync().WaitAsync(Timeout).ConfigureAwait(false);

            Assert.That(calls, Is.EqualTo(2), "Handling a failed action must not leave the scheduler busy.");
            Assert.That(exceptions, reported ? Is.EqualTo(new[] { failure }) : Is.Empty);
        }

        [TestCaseSource(nameof(StorageFailures))]
        public async Task BackgroundMessageCheckReportsOnlyUnexpectedStorageFailures(Exception failure, bool reported)
        {
            var account = CoreTestBase.GetTestEmailAccount();
            account.SynchronizationInterval = 0;
            var storage = new Mock<IDataStorage>();
            storage.Setup(x => x.GetAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Account> { account });
            var update = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            storage.Setup(x => x.UpdateAccountFolderStructureAsync(account, It.IsAny<CancellationToken>()))
                .Returns(update.Task);
            var mailbox = new Mock<IMailBox>();
            mailbox.Setup(x => x.HasFolderCounters).Returns(true);
            mailbox.Setup(x => x.GetFoldersStructureAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(account.FoldersStructure);
            mailbox.Setup(x => x.GetDefaultInboxFolderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(account.DefaultInboxFolder);
            using var core = CoreTestBase.CreateCore(storage.Object, mailbox.Object);
            var exceptions = new List<Exception>();
            core.ExceptionOccurred += (_, args) => exceptions.Add(args.Exception);

            try
            {
                await core.InitializeApplicationAsync("password", CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
                var forcedCheck = core.CheckForNewInboxMessagesAsync();
                update.SetException(failure);
                await forcedCheck.WaitAsync(Timeout).ConfigureAwait(false);
            }
            finally
            {
                update.TrySetException(failure);
            }

            storage.Verify(x => x.UpdateAccountFolderStructureAsync(account, It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.That(exceptions, reported ? Is.EqualTo(new[] { failure, failure }) : Is.Empty,
                "The initial and forced checks must classify storage failures the same way.");
        }

        [Test]
        public async Task ResetStopsSynchronizationBeforeNextFolder()
        {
            var account = CoreTestBase.GetTestEmailAccount();
            foreach (var folder in account.FoldersStructure)
            {
                folder.Account = account;
            }
            var failure = new DataBaseResetException("Storage was reset");
            var storage = new Mock<IDataStorage>();
            storage.Setup(x => x.GetLatestMessageAsync(account.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(failure);
            var service = new AccountService(account, storage.Object, new Mock<IMailBox>().Object, new Mock<IMessageProtector>().Object);

            var actual = await Assert.CatchAsync<DataBaseResetException>(async () =>
                await service.SynchronizeAsync(false, CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);

            Assert.That(actual, Is.SameAs(failure));
            Assert.That(account.FoldersStructure.Count, Is.GreaterThan(1));
            storage.Verify(x => x.GetLatestMessageAsync(account.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once,
                "Reset must interrupt the entire synchronization, even without caller cancellation.");
            storage.Verify(x => x.GetEarliestMessageAsync(account.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestCaseSource(nameof(StorageFailures))]
        public async Task EarlierMessagesKeepLoadedResultsOnlyWhenStorageWasReset(Exception failure, bool reported)
        {
            var folder = new Folder("Inbox", FolderAttributes.Inbox);
            var first = new Message { Id = 7, Subject = "Already loaded" };
            var storage = new Mock<IDataStorage>();
            storage.SetupSequence(x => x.GetEarlierMessagesInFoldersAsync(It.IsAny<IEnumerable<Folder>>(),
                    It.IsAny<int>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Message> { first })
                .ThrowsAsync(failure);
            var service = new Mock<IAccountService>();
            service.Setup(x => x.ReceiveEarlierMessagesAsync(folder, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Message>());
            var composite = new CompositeFolder(new[] { folder }, _ => service.Object);
            using var core = CoreTestBase.CreateCore(storage.Object);

            var loading = core.GetFolderEarlierMessagesAsync(composite, 2, null);
            await AssertStorageFailureAsync(loading, failure, reported).ConfigureAwait(false);

            if (!reported)
            {
                Assert.That(await loading.ConfigureAwait(false), Is.EqualTo(new[] { first }));
            }
            service.Verify(x => x.ReceiveEarlierMessagesAsync(folder, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
            storage.Verify(x => x.GetEarlierMessagesInFoldersAsync(It.IsAny<IEnumerable<Folder>>(),
                It.IsAny<int>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        private static async Task AssertStorageFailureAsync(Task operation, Exception failure, bool reported)
        {
            if (reported)
            {
                var actual = await Assert.CatchAsync<Exception>(async () => await operation.WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);
                Assert.That(actual, Is.SameAs(failure));
            }
            else
            {
                await operation.WaitAsync(Timeout).ConfigureAwait(false);
            }
        }
    }
}
