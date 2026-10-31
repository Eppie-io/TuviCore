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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using KeyDerivation.Keys;

using NUnit.Framework;

using SQLite;

using Tuvi.Core.DataStorage.Impl;
using Tuvi.Core.Entities;

using TuviPgpLib.Entities;

namespace Tuvi.Core.DataStorage.Tests
{
    [NonParallelizable]
    public class StorageLifecycleTests
    {
        private const string Password = "storage-lifecycle-old";
        private const string NewPassword = "storage-lifecycle-new";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
        private static readonly EmailAddress Email = new EmailAddress("lifecycle@eppie");
        private static readonly Folder Inbox = new Folder("Inbox", FolderAttributes.Inbox);

        [TestCase(false)]
        [TestCase(true)]
        public async Task ReplacementWaitsForActiveOperationAndPreservesDecReads(bool rekey)
        {
            using var files = new StorageFiles();
            var storage = files.Storage;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            var message = await storage.AddDecMessageAsync(Email, CreateMessage("original")).ConfigureAwait(false);
            using var held = await HoldOperationAsync(storage).ConfigureAwait(false);
            var replacement = ReplaceAsync(storage, rekey);
            var exists = storage.IsDecMessageExistsAsync(Email, Inbox.FullName, message.Hash);
            var list = storage.GetDecMessagesAsync(Email, Inbox, 10);
            var single = storage.GetDecMessageAsync(Email, Inbox, message.Id);
            try
            {
                Assert.That(await Task.WhenAny(replacement, Task.Delay(500)).ConfigureAwait(false), Is.Not.SameAs(replacement),
                    "Replacing the connection must wait for the active operation to release it.");
                Assert.That(exists.IsCompleted, Is.False, "New DEC operations must wait behind replacement.");
                Assert.That(list.IsCompleted, Is.False);
                Assert.That(single.IsCompleted, Is.False);
            }
            finally
            {
                held.Release();
                await Task.WhenAll(held.Completion, replacement, exists, list, single).WaitAsync(Timeout).ConfigureAwait(false);
            }

            Assert.That(await exists.ConfigureAwait(false), Is.True);
            AssertMessage((await list.ConfigureAwait(false)).Single(), "original");
            AssertMessage(await single.ConfigureAwait(false), "original");
            Assert.That(held.Subject, Is.EqualTo("subject-original"), "The active operation must retain a usable connection.");
        }

        [Test]
        public async Task RekeyPreservesQueuedWritesAndAllKeyValues()
        {
            using var files = new StorageFiles();
            var storage = files.Storage;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await storage.AddDecMessageAsync(Email, CreateMessage("original")).ConfigureAwait(false);
            var updatedMessage = await storage.AddDecMessageAsync(Email, CreateMessage("updated")).ConfigureAwait(false);
            var deletedMessage = await storage.AddDecMessageAsync(Email, CreateMessage("deleted")).ConfigureAwait(false);
            var account = TestData.CreateAccountWithFolder(Email);
            await storage.AddAccountAsync(account).ConfigureAwait(false);
            var ordinaryMessage = CreateMessage("ordinary").ToMessage();
            ordinaryMessage.Id = 17;
            ordinaryMessage.Folder = account.FoldersStructure.Single();
            await storage.AddMessageAsync(Email, ordinaryMessage, updateUnreadAndTotal: false).ConfigureAwait(false);
            var initialSettings = new Settings { EppieAccountCounter = 11, BitcoinAccountCounter = 22, EthereumAccountCounter = 33 };
            var newSettings = new Settings { EppieAccountCounter = 44, BitcoinAccountCounter = 55, EthereumAccountCounter = 66 };
            await storage.SetSettingsAsync(initialSettings).ConfigureAwait(false);
            using var masterKey = new MasterKey(Enumerable.Repeat((byte)1, 32).ToArray(), Enumerable.Repeat((byte)2, 32).ToArray());
            await storage.InitializeMasterKeyAsync(masterKey).ConfigureAwait(false);
            var initialPublicKeys = new PgpPublicKeyBundle { Data = new byte[] { 1, 3, 5, 7 } };
            var newPublicKeys = new PgpPublicKeyBundle { Data = new byte[] { 9, 11, 13, 15 } };
            var initialSecretKeys = new PgpSecretKeyBundle { Data = new byte[] { 2, 4, 6, 8 } };
            var newSecretKeys = new PgpSecretKeyBundle { Data = new byte[] { 10, 12, 14, 16 } };
            storage.SavePgpPublicKeys(initialPublicKeys);
            storage.SavePgpSecretKeys(initialSecretKeys);

            using var held = await HoldOperationAsync(storage).ConfigureAwait(false);
            var rekey = storage.ChangePasswordAsync(Password, NewPassword);
            var writes = Enumerable.Range(0, 8).Select(i => storage.AddDecMessageAsync(Email, CreateMessage("queued-" + i))).ToArray();
            updatedMessage.IsFlagged = true;
            var update = storage.UpdateDecMessageAsync(Email, updatedMessage);
            var delete = storage.DeleteDecMessageAsync(Email, Inbox, deletedMessage.Id);
            var ordinaryRead = storage.GetMessageAsync(Email, ordinaryMessage.Folder.FullName, ordinaryMessage.Id);
            var ordinaryCount = storage.GetMessagesCountAsync(Email, ordinaryMessage.Folder.FullName);
            var settingsRead = storage.GetSettingsAsync();
            var settingsWrite = storage.SetSettingsAsync(newSettings);
            var masterRead = storage.GetMasterKeyAsync();
            var publicRead = storage.GetPgpPublicKeysAsync();
            var secretRead = storage.GetPgpSecretKeysAsync();
            var saveStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var synchronousSave = Task.Run(() =>
            {
                saveStarted.SetResult(true);
                storage.SavePgpPublicKeys(newPublicKeys);
                storage.SavePgpSecretKeys(newSecretKeys);
            });
            try
            {
                await saveStarted.Task.WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(await Task.WhenAny(rekey, Task.Delay(500)).ConfigureAwait(false), Is.Not.SameAs(rekey));
                Assert.That(masterRead.IsCompleted, Is.False);
                Assert.That(publicRead.IsCompleted, Is.False);
                Assert.That(secretRead.IsCompleted, Is.False);
                Assert.That(update.IsCompleted, Is.False);
                Assert.That(delete.IsCompleted, Is.False);
                Assert.That(ordinaryRead.IsCompleted, Is.False);
                Assert.That(ordinaryCount.IsCompleted, Is.False);
                Assert.That(settingsRead.IsCompleted, Is.False);
                Assert.That(settingsWrite.IsCompleted, Is.False);
                Assert.That(synchronousSave.IsCompleted, Is.False);
            }
            finally
            {
                held.Release();
                await Task.WhenAll(writes.Cast<Task>().Concat(new Task[]
                {
                    held.Completion, rekey, masterRead, publicRead, secretRead,
                    update, delete, ordinaryRead, ordinaryCount, settingsRead, settingsWrite, synchronousSave
                }))
                    .WaitAsync(Timeout).ConfigureAwait(false);
            }

            using var loadedMaster = await masterRead.ConfigureAwait(false);
            Assert.That(loadedMaster, Is.EqualTo(masterKey));
            Assert.That((await publicRead.ConfigureAwait(false)).Data, Is.EqualTo(initialPublicKeys.Data).Or.EqualTo(newPublicKeys.Data));
            Assert.That((await secretRead.ConfigureAwait(false)).Data, Is.EqualTo(initialSecretKeys.Data).Or.EqualTo(newSecretKeys.Data));
            Assert.That((await ordinaryRead.ConfigureAwait(false)).Subject, Is.EqualTo(ordinaryMessage.Subject));
            Assert.That((await ordinaryRead.ConfigureAwait(false)).TextBody, Is.EqualTo(ordinaryMessage.TextBody));
            Assert.That(await ordinaryCount.ConfigureAwait(false), Is.EqualTo(1));
            var loadedSettings = await settingsRead.ConfigureAwait(false);
            Assert.That((loadedSettings.EppieAccountCounter, loadedSettings.BitcoinAccountCounter, loadedSettings.EthereumAccountCounter),
                Is.EqualTo((initialSettings.EppieAccountCounter, initialSettings.BitcoinAccountCounter, initialSettings.EthereumAccountCounter))
                  .Or.EqualTo((newSettings.EppieAccountCounter, newSettings.BitcoinAccountCounter, newSettings.EthereumAccountCounter)));
            await storage.OpenAsync(NewPassword).WaitAsync(Timeout).ConfigureAwait(false);
            var reopenedSettings = await storage.GetSettingsAsync().WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(reopenedSettings.EppieAccountCounter, Is.EqualTo(newSettings.EppieAccountCounter));
            Assert.That(reopenedSettings.BitcoinAccountCounter, Is.EqualTo(newSettings.BitcoinAccountCounter));
            Assert.That(reopenedSettings.EthereumAccountCounter, Is.EqualTo(newSettings.EthereumAccountCounter));
            Assert.That((await storage.GetPgpPublicKeysAsync().WaitAsync(Timeout).ConfigureAwait(false)).Data, Is.EqualTo(newPublicKeys.Data));
            Assert.That((await storage.GetPgpSecretKeysAsync().WaitAsync(Timeout).ConfigureAwait(false)).Data, Is.EqualTo(newSecretKeys.Data));
            var messages = await storage.GetDecMessagesAsync(Email, Inbox, 0).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(messages.Count, Is.EqualTo(10));
            Assert.That(messages.Select(x => x.Hash), Is.EquivalentTo(new[] { "original", "updated" }.Concat(Enumerable.Range(0, 8).Select(i => "queued-" + i))));
            Assert.That(messages.Single(x => x.Hash == "updated").IsFlagged, Is.True);
            foreach (var message in messages)
            {
                AssertMessage(message, message.Hash);
            }
            using var reopenedMaster = await storage.GetMasterKeyAsync().WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(reopenedMaster, Is.EqualTo(masterKey));
        }

        [Test]
        public async Task FailedBatchRollsBackDataCachesAndContactEvents()
        {
            using var files = new StorageFiles();
            var storage = files.Storage;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            var account = TestData.CreateAccountWithFolder(Email);
            await storage.AddAccountAsync(account).WaitAsync(Timeout).ConfigureAwait(false);
            var existingSender = new EmailAddress("preserved@rollback.example", "Preserved sender");
            var pendingSender = new EmailAddress("pending@rollback.example", "Rolled back sender");
            var preserved = CreateStoredMessage(account.FoldersStructure.Single(), 1, "preserved");
            preserved.From.Add(existingSender);
            preserved.To.Add(Email);
            preserved.Attachments.Add(new Attachment { FileName = "preserved.bin", Data = new byte[] { 1, 2, 3 } });
            preserved.Protection = new ProtectionInfo { Type = MessageProtectionType.Signature };
            preserved.Protection.SignaturesInfo.Add(new SignatureInfo { SignerFingerprint = "preserved-signature" });
            await storage.AddMessageAsync(Email, preserved, true).WaitAsync(Timeout).ConfigureAwait(false);
            Dictionary<string, int> before = await ReadAffectedRowCountsAsync(storage).WaitAsync(Timeout).ConfigureAwait(false);
            Contact existingContact = await storage.GetContactAsync(existingSender, CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
            Folder baselineFolder = (await storage.GetAccountAsync(Email).WaitAsync(Timeout).ConfigureAwait(false)).FoldersStructure.Single();
            var events = new List<string>();
            storage.ContactAdded += (_, args) => events.Add("added:" + args.Contact.Email.Address);
            storage.ContactChanged += (_, args) => events.Add("changed:" + args.Contact.Email.Address);
            storage.ContactDeleted += (_, args) => events.Add("deleted:" + args.ContactEmail.Address);

            var rejected = CreateStoredMessage(account.FoldersStructure.Single(), 2, "rolled-back");
            rejected.From.Add(new EmailAddress(existingSender.Address, "Transaction-local name"));
            rejected.From.Add(pendingSender);
            rejected.To.Add(Email);
            rejected.IsMarkedAsRead = false;
            rejected.Attachments.Add(new Attachment { FileName = "rolled-back.bin", Data = new byte[] { 4, 5, 6 } });
            rejected.Protection = new ProtectionInfo { Type = MessageProtectionType.SignatureAndEncryption };
            rejected.Protection.SignaturesInfo.Add(new SignatureInfo { SignerFingerprint = "rolled-back-signature" });
            bool reachedFailure = false;
            var failure = new InvalidOperationException("Stop after the first transactional message");
            var batch = new FailingMessageBatch(rejected, () =>
            {
                Assert.That(rejected.Pk, Is.GreaterThan(preserved.Pk), "A real insert must precede the failure.");
                Assert.That(ReadCachedEmailName(storage, "_emailAddressDataCache", existingSender.Address), Is.EqualTo("Transaction-local name"));
                Assert.That(ReadCachedEmailName(storage, "_emailAddressesCache2", existingSender.Address), Is.EqualTo("Transaction-local name"));
                Assert.That(events, Is.Empty, "Contact events must wait for commit and release of the gate.");
                reachedFailure = true;
            }, failure);
            await Assert.ThatAsync(async () => await storage.AddMessageListAsync(Email, baselineFolder.FullName, batch, true)
                .WaitAsync(Timeout).ConfigureAwait(false),
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo(failure.Message)).ConfigureAwait(false);
            Assert.That(reachedFailure, Is.True);
            Assert.That(events, Is.Empty, "A rolled-back transaction must publish no contact events.");
            Assert.That(await ReadAffectedRowCountsAsync(storage).WaitAsync(Timeout).ConfigureAwait(false), Is.EqualTo(before));
            Assert.That(await storage.IsMessageExistAsync(Email, baselineFolder.FullName, rejected.Id).WaitAsync(Timeout).ConfigureAwait(false), Is.False);
            Assert.That(await storage.ExistsContactWithEmailAddressAsync(pendingSender, CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false), Is.False);
            Message restored = await storage.GetMessageAsync(Email, baselineFolder.FullName, preserved.Id).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(restored.Subject, Is.EqualTo(preserved.Subject));
            Assert.That(restored.TextBody, Is.EqualTo(preserved.TextBody));
            Assert.That(restored.From.Single().Address, Is.EqualTo(existingSender.Address));
            Assert.That(restored.From.Single().Name, Is.EqualTo(existingSender.Name));
            Assert.That(restored.To.Single(), Is.EqualTo(Email));
            Assert.That(restored.Attachments.Single(), Is.EqualTo(preserved.Attachments.Single()));
            Assert.That(restored.Protection.Type, Is.EqualTo(preserved.Protection.Type));
            Assert.That(restored.Protection.SignaturesInfo.Single().SignerFingerprint, Is.EqualTo("preserved-signature"));
            Contact restoredContact = await storage.GetContactAsync(existingSender, CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(restoredContact.Id, Is.EqualTo(existingContact.Id));
            Assert.That(restoredContact.Email.Name, Is.EqualTo(existingSender.Name));
            Assert.That(restoredContact.UnreadCount, Is.EqualTo(existingContact.UnreadCount));
            Assert.That(restoredContact.LastMessageData.MessageId, Is.EqualTo(preserved.Id));
            Folder restoredFolder = (await storage.GetAccountAsync(Email).WaitAsync(Timeout).ConfigureAwait(false)).FoldersStructure.Single();
            Assert.That((restoredFolder.LocalCount, restoredFolder.TotalCount, restoredFolder.UnreadCount),
                Is.EqualTo((baselineFolder.LocalCount, baselineFolder.TotalCount, baselineFolder.UnreadCount)));

            var committedSender = new EmailAddress(pendingSender.Address, "Committed sender");
            var committed = CreateStoredMessage(restoredFolder, 3, "committed");
            committed.From.Add(committedSender);
            committed.To.Add(Email);
            committed.IsMarkedAsRead = false;
            await storage.AddMessageAsync(Email, committed, true).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(committed.Pk, Is.EqualTo(rejected.Pk), "SQLite should naturally reuse the rolled-back primary key.");
            Assert.That(events, Is.EqualTo(new[] { "added:" + committedSender.Address }));
            Message loaded = await storage.GetMessageAsync(Email, restoredFolder.FullName, committed.Id).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(loaded.Subject, Is.EqualTo(committed.Subject));
            Assert.That(loaded.TextBody, Is.EqualTo(committed.TextBody));
            Assert.That(loaded.From.Single().Address, Is.EqualTo(committedSender.Address));
            Assert.That(loaded.From.Single().Name, Is.EqualTo(committedSender.Name));
            Assert.That(loaded.To.Single(), Is.EqualTo(Email));
            Contact committedContact = await storage.GetContactAsync(committedSender, CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(committedContact.Email.Name, Is.EqualTo(committedSender.Name));
            Assert.That(committedContact.FullName, Is.EqualTo(committedSender.Name));
            Assert.That(committedContact.UnreadCount, Is.EqualTo(1));
            Assert.That(committedContact.LastMessageData.MessageId, Is.EqualTo(committed.Id));
            Assert.That(await storage.GetMessagesCountAsync(Email, restoredFolder.FullName).WaitAsync(Timeout).ConfigureAwait(false), Is.EqualTo(2));
        }

        private static string ReadCachedEmailName(IDataStorage storage, string fieldName, string address)
        {
            var cache = (System.Collections.IDictionary)storage.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(storage);
            object email = cache.Values.Cast<object>().Single(value =>
                (string)value.GetType().GetProperty(nameof(EmailAddress.Address)).GetValue(value) == address);
            return (string)email.GetType().GetProperty(nameof(EmailAddress.Name)).GetValue(email);
        }

        private static Task<Dictionary<string, int>> ReadAffectedRowCountsAsync(IDataStorage storage)
        {
            Type type = storage.GetType();
            MethodInfo method = null;
            while (type != null && method == null)
            {
                method = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .SingleOrDefault(candidate => candidate.Name == "ExecuteDatabaseAsync" && candidate.IsGenericMethodDefinition);
                type = type.BaseType;
            }
            if (method == null)
            {
                throw new MissingMethodException("Storage operation boundary not found.");
            }
            Func<SQLiteConnection, CancellationToken, Dictionary<string, int>> read = (connection, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                string[] tables = { "Message", "Contact", "EmailAddressData", "MessageEmailAddress", "MessageContact",
                    "LastMessageData", "Attachment", "ProtectionInfo", "SignatureInfo", "Folder", "ImageInfo" };
                return tables.ToDictionary(table => table, table => connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM [{table}]"));
            };
            return (Task<Dictionary<string, int>>)method.MakeGenericMethod(typeof(Dictionary<string, int>))
                .Invoke(storage, new object[] { read, CancellationToken.None, null });
        }

        private sealed class FailingMessageBatch(Message first, Action afterFirst, InvalidOperationException failure) : IReadOnlyList<Message>
        {
            public int Count => 2;

            public Message this[int index] => GetItem(index);

            private Message GetItem(int index)
            {
                if (index == 0)
                {
                    return first;
                }
                if (index != 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                afterFirst();
                throw failure;
            }

            public IEnumerator<Message> GetEnumerator()
            {
                yield return this[0];
                yield return this[1];
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        [Test]
        public async Task CancelledWaitAndFailedOpenAllowSubsequentUse()
        {
            using var files = new StorageFiles();
            var storage = files.Storage;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await storage.AddDecMessageAsync(Email, CreateMessage("original")).ConfigureAwait(false);
            using (var held = await HoldOperationAsync(storage).ConfigureAwait(false))
            {
                using var cancellation = new CancellationTokenSource();
                var cancelledRead = storage.GetDecMessagesAsync(Email, Inbox, 10, cancellation.Token);
                var cancelledOpen = storage.OpenAsync(Password, cancellation.Token);
                cancellation.Cancel();
                bool readCancelled;
                bool openCancelled;
                try
                {
                    readCancelled = await IsCancelledAsync(cancelledRead).ConfigureAwait(false);
                    openCancelled = await IsCancelledAsync(cancelledOpen).ConfigureAwait(false);
                }
                finally
                {
                    held.Release();
                    await held.Completion.WaitAsync(Timeout).ConfigureAwait(false);
                }
                Assert.That(readCancelled, Is.True, "A waiting DEC read must honor caller cancellation.");
                Assert.That(openCancelled, Is.True, "A waiting open must honor caller cancellation.");
            }
            Assert.That(ReadGateUsers(storage), Is.Zero);

            await Assert.ThatAsync(async () => await storage.OpenAsync("incorrect-password").WaitAsync(Timeout).ConfigureAwait(false),
                Throws.TypeOf<DataBasePasswordException>()).ConfigureAwait(false);
            Assert.That(ReadGateUsers(storage), Is.Zero);
            await storage.OpenAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            AssertMessage((await storage.GetDecMessagesAsync(Email, Inbox, 10).ConfigureAwait(false)).Single(), "original");
        }

        [Test]
        public async Task DisposeReleasesOwnedSemaphore()
        {
            using var gate = new TrackingSemaphore();
            using var files = new StorageFiles();
            var storage = files.Storage;
            TrackGate(storage, gate);
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            var handle = gate.AvailableWaitHandle.SafeWaitHandle;
            await Task.Run(storage.Dispose).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(handle.IsClosed, Is.True, "Storage must dispose its owned semaphore.");
            Assert.That(ReadGateUsers(storage), Is.Zero);
            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(storage.Dispose))).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(gate.DisposeCount, Is.EqualTo(1), "Repeated disposal must not dispose the gate again.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ClosingWaitsForActiveOperationAndRejectsNewWork(bool dispose)
        {
            using var files = new StorageFiles();
            var storage = files.Storage;
            var gate = (SemaphoreSlim)GetStorageField(storage, "_operationGate").GetValue(storage);
            var handle = gate.AvailableWaitHandle.SafeWaitHandle;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await storage.AddDecMessageAsync(Email, CreateMessage("original")).ConfigureAwait(false);
            using var held = await HoldOperationAsync(storage).ConfigureAwait(false);
            var queuedRead = storage.GetDecMessagesAsync(Email, Inbox, 10);
            var queuedWrite = storage.AddDecMessageAsync(Email, CreateMessage("rejected-write"));
            var queuedOpen = storage.OpenAsync(Password);
            var saveStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var queuedSave = Task.Run(() =>
            {
                saveStarted.SetResult(true);
                storage.SavePgpPublicKeys(new PgpPublicKeyBundle { Data = new byte[] { 1 } });
            });
            var rejected = Task.WhenAll(new Task[] { queuedRead, queuedWrite, queuedOpen, queuedSave }.Select(ObserveClosingFailureAsync));
            Task closing = Task.CompletedTask;
            try
            {
                await saveStarted.Task.WaitAsync(Timeout).ConfigureAwait(false);
                await WaitForGateUsersAsync(storage, 5).ConfigureAwait(false);
                closing = dispose ? Task.Run(storage.Dispose) : storage.ResetAsync();
                if (dispose)
                {
                    await WaitForDisposalAsync(storage).ConfigureAwait(false);
                }
                Assert.That(ReadGateUsers(storage), Is.EqualTo(6), "The closer also owns a registration.");
                Assert.That(handle.IsClosed, Is.False);
                Assert.That(closing.IsCompleted, Is.False, "Closing must wait for the admitted operation.");
                if (dispose)
                {
                    await Assert.ThatAsync(async () => await storage.GetDecMessagesAsync(Email, Inbox, 10).WaitAsync(Timeout).ConfigureAwait(false),
                        Throws.TypeOf<ObjectDisposedException>()).ConfigureAwait(false);
                    Assert.That(() => storage.SavePgpPublicKeys(new PgpPublicKeyBundle { Data = new byte[] { 1 } }), Throws.TypeOf<ObjectDisposedException>());
                }
                else
                {
                    await Assert.ThatAsync(async () => await storage.GetDecMessagesAsync(Email, Inbox, 10).WaitAsync(Timeout).ConfigureAwait(false),
                        Throws.TypeOf<DataBaseException>()).ConfigureAwait(false);
                    Assert.That(() => storage.SavePgpPublicKeys(new PgpPublicKeyBundle { Data = new byte[] { 1 } }), Throws.TypeOf<DataBaseException>());
                }
            }
            finally
            {
                held.Release();
                await Task.WhenAll(held.Completion, closing, rejected).WaitAsync(Timeout).ConfigureAwait(false);
            }

            Assert.That(held.Subject, Is.EqualTo("subject-original"));
            foreach (var failure in await rejected.ConfigureAwait(false))
            {
                Assert.That(failure, dispose ? Is.TypeOf<ObjectDisposedException>() : Is.InstanceOf<DataBaseException>(),
                    "Already queued operations must reject closing without using a destroyed gate or connection.");
                if (dispose)
                {
                    Assert.That(((ObjectDisposedException)failure).ObjectName, Is.EqualTo("DataStorage"));
                }
            }
            Assert.That(ReadGateUsers(storage), Is.Zero);
            Assert.That(handle.IsClosed, Is.EqualTo(dispose));
            if (dispose)
            {
                await Assert.ThatAsync(async () => await storage.GetDecMessagesAsync(Email, Inbox, 10).WaitAsync(Timeout).ConfigureAwait(false),
                    Throws.TypeOf<ObjectDisposedException>()).ConfigureAwait(false);
                using var reopened = DataStorageProvider.GetDataStorage(files.Path);
                await reopened.OpenAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
                AssertMessage((await reopened.GetDecMessagesAsync(Email, Inbox, 10).ConfigureAwait(false)).Single(), "original");
            }
            else
            {
                await Assert.ThatAsync(async () => await storage.GetDecMessagesAsync(Email, Inbox, 10).WaitAsync(Timeout).ConfigureAwait(false),
                    Throws.TypeOf<DataBaseException>()).ConfigureAwait(false);
                Assert.That(File.Exists(files.Path), Is.False);
                await storage.CreateAsync(NewPassword).WaitAsync(Timeout).ConfigureAwait(false);
                await storage.AddDecMessageAsync(Email, CreateMessage("recreated")).WaitAsync(Timeout).ConfigureAwait(false);
                await storage.OpenAsync(NewPassword).WaitAsync(Timeout).ConfigureAwait(false);
                AssertMessage((await storage.GetDecMessagesAsync(Email, Inbox, 10).WaitAsync(Timeout).ConfigureAwait(false)).Single(), "recreated");
            }
        }

        [Test]
        public async Task ConcurrentResetAndRepeatedDisposeDrainQueuedOperations()
        {
            using var gate = new TrackingSemaphore();
            using var files = new StorageFiles();
            var storage = files.Storage;
            TrackGate(storage, gate);
            var handle = gate.AvailableWaitHandle.SafeWaitHandle;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await storage.AddDecMessageAsync(Email, CreateMessage("original")).ConfigureAwait(false);
            using var held = await HoldOperationAsync(storage).ConfigureAwait(false);
            var queuedRead = storage.GetDecMessagesAsync(Email, Inbox, 10);
            var queuedOpen = storage.OpenAsync(Password);
            var rejected = Task.WhenAll(new Task[] { queuedRead, queuedOpen }.Select(ObserveClosingFailureAsync));
            var reset = storage.ResetAsync();
            var repeatedReset = storage.ResetAsync();
            Task firstDispose = Task.CompletedTask;
            Task secondDispose = Task.CompletedTask;
            try
            {
                Assert.That(ReadGateUsers(storage), Is.EqualTo(4), "Repeated reset must not register a second closer.");
                firstDispose = Task.Run(storage.Dispose);
                secondDispose = Task.Run(storage.Dispose);
                await WaitForDisposalAsync(storage).ConfigureAwait(false);
                Assert.That(ReadGateUsers(storage), Is.EqualTo(5), "Only the first dispose registers a closer.");
                Assert.That(handle.IsClosed, Is.False);
                Assert.That(repeatedReset, Is.SameAs(reset), "Concurrent reset callers share completion of the same reset.");
                Assert.That(reset.IsCompleted, Is.False);
                Assert.That(firstDispose.IsCompleted, Is.False);
                Assert.That(secondDispose.IsCompleted, Is.False);
                await Assert.ThatAsync(async () => await storage.ResetAsync().WaitAsync(Timeout).ConfigureAwait(false),
                    Throws.TypeOf<ObjectDisposedException>()).ConfigureAwait(false);
            }
            finally
            {
                held.Release();
                await Task.WhenAll(held.Completion, reset, repeatedReset, firstDispose, secondDispose, rejected).WaitAsync(Timeout).ConfigureAwait(false);
            }

            Assert.That(held.Subject, Is.EqualTo("subject-original"));
            Assert.That(File.Exists(files.Path), Is.False);
            foreach (var failure in await rejected.ConfigureAwait(false))
            {
                Assert.That(failure, Is.TypeOf<ObjectDisposedException>());
                Assert.That(((ObjectDisposedException)failure).ObjectName, Is.EqualTo("DataStorage"));
            }
            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(storage.Dispose))).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(ReadGateUsers(storage), Is.Zero);
            Assert.That(handle.IsClosed, Is.True);
            Assert.That(gate.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task CancellationDuringDisposeReleasesGateRegistration()
        {
            using var files = new StorageFiles();
            var storage = files.Storage;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await storage.AddDecMessageAsync(Email, CreateMessage("original")).WaitAsync(Timeout).ConfigureAwait(false);
            var gate = (SemaphoreSlim)GetStorageField(storage, "_operationGate").GetValue(storage);
            var handle = gate.AvailableWaitHandle.SafeWaitHandle;
            using var held = await HoldOperationAsync(storage).ConfigureAwait(false);
            using var cancellation = new CancellationTokenSource();
            var cancelled = IsCancelledAsync(storage.GetDecMessagesAsync(Email, Inbox, 10, cancellation.Token));
            Task closing = Task.CompletedTask;
            try
            {
                Assert.That(ReadGateUsers(storage), Is.EqualTo(2));
                closing = Task.Run(storage.Dispose);
                await WaitForDisposalAsync(storage).ConfigureAwait(false);
                Assert.That(ReadGateUsers(storage), Is.EqualTo(3));
                cancellation.Cancel();
                Assert.That(await cancelled.WaitAsync(Timeout).ConfigureAwait(false), Is.True);
                Assert.That(ReadGateUsers(storage), Is.EqualTo(2), "Cancellation must unregister without releasing an unacquired gate.");
                Assert.That(handle.IsClosed, Is.False);
                Assert.That(closing.IsCompleted, Is.False);
            }
            finally
            {
                cancellation.Cancel();
                held.Release();
                await Task.WhenAll(held.Completion, closing, cancelled).WaitAsync(Timeout).ConfigureAwait(false);
            }
            Assert.That(held.Subject, Is.EqualTo("subject-original"));
            Assert.That(ReadGateUsers(storage), Is.Zero);
            Assert.That(handle.IsClosed, Is.True);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        public async Task AdmittedCallCanResumeAfterDisposeBeforeFirstWait(bool synchronous, bool cancelBeforeWait)
        {
            using var gate = new TrackingSemaphore();
            using var files = new StorageFiles();
            var storage = files.Storage;
            TrackGate(storage, gate);
            var handle = gate.AvailableWaitHandle.SafeWaitHandle;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await storage.AddDecMessageAsync(Email, CreateMessage("original")).WaitAsync(Timeout).ConfigureAwait(false);
            var type = GetStorageField(storage, "_operationGate").DeclaringType;
            var register = type.GetMethod("RegisterGateUser", BindingFlags.Instance | BindingFlags.NonPublic);
            var unregister = type.GetMethod("UnregisterGateUser", BindingFlags.Instance | BindingFlags.NonPublic);
            long generation = (long)register.Invoke(storage, new object[] { null });
            Task<Exception> resumed = null;
            Task closing = Task.CompletedTask;
            using var cancellation = new CancellationTokenSource();
            bool callbackExecuted = false;
            try
            {
                closing = Task.Run(storage.Dispose);
                await closing.WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(ReadGateUsers(storage), Is.EqualTo(1));
                Assert.That(handle.IsClosed, Is.False, "Dispose must return without destroying the delayed caller's gate.");
                Assert.That(gate.DisposeCount, Is.Zero);
                using (var file = new FileStream(files.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Assert.That(file.Length, Is.GreaterThan(0), "The physical SQLite connection must already be closed.");
                }
                await Assert.ThatAsync(async () => await storage.GetDecMessagesAsync(Email, Inbox, 10).WaitAsync(Timeout).ConfigureAwait(false),
                    Throws.TypeOf<ObjectDisposedException>().With.Property(nameof(ObjectDisposedException.ObjectName)).EqualTo("DataStorage")).ConfigureAwait(false);
                Assert.That(ReadGateUsers(storage), Is.EqualTo(1), "Rejected admission must not register a new user.");

                if (synchronous)
                {
                    var execute = type.GetMethod("ExecuteRegisteredDatabase", BindingFlags.Instance | BindingFlags.NonPublic);
                    Action<SQLiteConnection> operation = connection =>
                    {
                        callbackExecuted = true;
                        connection.ExecuteScalar<int>("SELECT count(*) FROM DecMessage");
                    };
                    resumed = Task.Run(() =>
                    {
                        try
                        {
                            execute.Invoke(storage, new object[] { operation, generation });
                            return null;
                        }
                        catch (TargetInvocationException exception)
                        {
                            return exception.InnerException;
                        }
                    });
                }
                else
                {
                    if (cancelBeforeWait)
                    {
                        cancellation.Cancel();
                    }
                    var execute = type.GetMethod("RunRegisteredOperationAsync", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(string));
                    Func<CancellationToken, Task<string>> operation = _ =>
                    {
                        callbackExecuted = true;
                        return Task.FromResult("must not execute");
                    };
                    var task = (Task)execute.Invoke(storage, new object[] { operation, generation, cancellation.Token });
                    resumed = ObserveClosingFailureAsync(task);
                }
                Exception failure = await resumed.WaitAsync(Timeout).ConfigureAwait(false);
                if (cancelBeforeWait)
                {
                    Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
                    Assert.That(((OperationCanceledException)failure).CancellationToken, Is.EqualTo(cancellation.Token));
                }
                else
                {
                    Assert.That(failure, Is.TypeOf<ObjectDisposedException>());
                    Assert.That(((ObjectDisposedException)failure).ObjectName, Is.EqualTo("DataStorage"));
                }
                Assert.That(callbackExecuted, Is.False);
                Assert.That(ReadGateUsers(storage), Is.Zero);
                Assert.That(handle.IsClosed, Is.True);
                await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(storage.Dispose))).WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(gate.DisposeCount, Is.EqualTo(1), "The last registration disposes the gate exactly once.");
            }
            finally
            {
                if (resumed == null)
                {
                    unregister.Invoke(storage, new object[] { false });
                }
                else
                {
                    await resumed.WaitAsync(Timeout).ConfigureAwait(false);
                }
                await closing.WaitAsync(Timeout).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task FailedResetAllowsReopenAndRetry()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Ignore("This scenario requires Windows file-sharing semantics.");
            }

            using var files = new StorageFiles();
            var storage = files.Storage;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await storage.AddDecMessageAsync(Email, CreateMessage("original")).WaitAsync(Timeout).ConfigureAwait(false);

            using (var heldFile = new FileStream(files.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                await Assert.ThatAsync(async () => await storage.ResetAsync().WaitAsync(Timeout).ConfigureAwait(false),
                    Throws.InstanceOf<IOException>()).ConfigureAwait(false);
                Assert.That(await storage.IsStorageExistAsync().WaitAsync(Timeout).ConfigureAwait(false), Is.True);
                Assert.That(ReadGateUsers(storage), Is.Zero);
            }

            await storage.OpenAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            AssertMessage((await storage.GetDecMessagesAsync(Email, Inbox, 10).WaitAsync(Timeout).ConfigureAwait(false)).Single(), "original");
            await storage.ResetAsync().WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(File.Exists(files.Path), Is.False);
            await storage.CreateAsync(NewPassword).WaitAsync(Timeout).ConfigureAwait(false);
        }

        [Test]
        public async Task ContactEventCanSynchronouslyReadStorage()
        {
            using var files = new StorageFiles();
            var storage = files.Storage;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            var contact = new Contact("Nested storage read", new EmailAddress("nested@example.com"));
            Contact loaded = null;
            storage.ContactAdded += (_, _) => loaded = storage.GetContactAsync(contact.Email, CancellationToken.None).WaitAsync(Timeout).GetAwaiter().GetResult();
            await storage.AddContactAsync(contact, CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(loaded.FullName, Is.EqualTo(contact.FullName));
            Assert.That(loaded.Email, Is.EqualTo(contact.Email));
        }

        private static async Task<bool> IsCancelledAsync(Task task)
        {
            try
            {
                await task.WaitAsync(Timeout).ConfigureAwait(false);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }

        private static async Task<Exception> ObserveClosingFailureAsync(Task task)
        {
            try
            {
                await task.WaitAsync(Timeout).ConfigureAwait(false);
                return null;
            }
            catch (DataBaseException exception)
            {
                return exception;
            }
            catch (ObjectDisposedException exception)
            {
                return exception;
            }
            catch (OperationCanceledException exception)
            {
                return exception;
            }
        }

        private static FieldInfo GetStorageField(IDataStorage storage, string name)
        {
            var type = storage.GetType();
            while (type != null)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
                type = type.BaseType;
            }
            throw new MissingFieldException(name);
        }

        private static void TrackGate(IDataStorage storage, TrackingSemaphore gate)
        {
            var field = GetStorageField(storage, "_operationGate");
            var original = (SemaphoreSlim)field.GetValue(storage);
            field.SetValue(storage, gate);
            original.Dispose();
        }

        private static int ReadGateUsers(IDataStorage storage)
        {
            var stateLock = GetStorageField(storage, "_stateLock").GetValue(storage);
            lock (stateLock)
            {
                return (int)GetStorageField(storage, "_gateUsers").GetValue(storage);
            }
        }

        private static async Task WaitForGateUsersAsync(IDataStorage storage, int count)
        {
            var elapsed = Stopwatch.StartNew();
            while (ReadGateUsers(storage) != count)
            {
                if (elapsed.Elapsed >= Timeout)
                {
                    throw new TimeoutException("Storage calls did not reach the expected registration count.");
                }
                await Task.Delay(1).ConfigureAwait(false);
            }
        }

        private sealed class TrackingSemaphore : SemaphoreSlim
        {
            private int _disposeCount;

            public TrackingSemaphore() : base(1, 1)
            {
            }

            public int DisposeCount => Volatile.Read(ref _disposeCount);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Interlocked.Increment(ref _disposeCount);
                }
                base.Dispose(disposing);
            }
        }

        private static async Task WaitForDisposalAsync(IDataStorage storage)
        {
            var checkDisposed = storage.GetType().GetMethod("CheckDisposed", BindingFlags.Instance | BindingFlags.NonPublic);
            var elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < Timeout)
            {
                try
                {
                    checkDisposed.Invoke(storage, Array.Empty<object>());
                }
                catch (TargetInvocationException exception) when (exception.InnerException is ObjectDisposedException)
                {
                    return;
                }
                await Task.Delay(1).ConfigureAwait(false);
            }
            throw new TimeoutException("Disposal did not reject admission within the deadline.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ReplacementDoesNotCloseAnIndependentDatabase(bool rekey)
        {
            using var first = new StorageFiles();
            using var second = new StorageFiles();
            await first.Storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await second.Storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await second.Storage.AddDecMessageAsync(Email, CreateMessage("original")).ConfigureAwait(false);
            using var held = await HoldOperationAsync(second.Storage).ConfigureAwait(false);
            try
            {
                await ReplaceAsync(first.Storage, rekey).WaitAsync(Timeout).ConfigureAwait(false);
            }
            finally
            {
                held.Release();
                await held.Completion.WaitAsync(Timeout).ConfigureAwait(false);
            }
            Assert.That(held.Subject, Is.EqualTo("subject-original"));
            await second.Storage.AddDecMessageAsync(Email, CreateMessage("independent-write")).WaitAsync(Timeout).ConfigureAwait(false);
            var messages = await second.Storage.GetDecMessagesAsync(Email, Inbox, 0).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(messages.Count, Is.EqualTo(2));
            foreach (var message in messages)
            {
                AssertMessage(message, message.Hash);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task WrongPasswordPreservesDataAndAllowsCorrectReopen(bool rekey)
        {
            using var files = new StorageFiles();
            var storage = files.Storage;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await storage.AddDecMessageAsync(Email, CreateMessage("original")).ConfigureAwait(false);
            using var master = new MasterKey(Enumerable.Repeat((byte)3, 32).ToArray(), Enumerable.Repeat((byte)4, 32).ToArray());
            await storage.InitializeMasterKeyAsync(master).ConfigureAwait(false);

            await Assert.ThatAsync(async () => await (rekey
                ? storage.ChangePasswordAsync("incorrect-password", NewPassword)
                : storage.OpenAsync("incorrect-password")).WaitAsync(Timeout).ConfigureAwait(false),
                Throws.TypeOf<DataBasePasswordException>()).ConfigureAwait(false);

            await storage.OpenAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            AssertMessage((await storage.GetDecMessagesAsync(Email, Inbox, 10).WaitAsync(Timeout).ConfigureAwait(false)).Single(), "original");
            using var reopenedMaster = await storage.GetMasterKeyAsync().WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(reopenedMaster, Is.EqualTo(master));
            storage.Dispose();

            using var verifier = DataStorageProvider.GetDataStorage(files.Path);
            await verifier.OpenAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            AssertMessage((await verifier.GetDecMessagesAsync(Email, Inbox, 10).ConfigureAwait(false)).Single(), "original");
            using var storedMaster = await verifier.GetMasterKeyAsync().ConfigureAwait(false);
            Assert.That(storedMaster, Is.EqualTo(master));
            await Assert.ThatAsync(async () => await verifier.OpenAsync(NewPassword).WaitAsync(Timeout).ConfigureAwait(false),
                Throws.TypeOf<DataBasePasswordException>()).ConfigureAwait(false);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RecreatedDatabaseDoesNotReuseCachedContactAddresses(bool reopen)
        {
            using var files = new StorageFiles();
            var storage = files.Storage;
            var oldAddress = new EmailAddress("old@lifecycle.example");
            var newAddress = new EmailAddress("new@lifecycle.example");
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await storage.AddContactAsync(new Contact("Old contact", oldAddress), default).ConfigureAwait(false);
            Assert.That((await storage.GetContactAsync(oldAddress, default).ConfigureAwait(false)).Email, Is.EqualTo(oldAddress));
            await storage.ResetAsync().WaitAsync(Timeout).ConfigureAwait(false);

            await storage.CreateAsync(NewPassword).WaitAsync(Timeout).ConfigureAwait(false);
            await storage.AddContactAsync(new Contact("New contact", newAddress), default).ConfigureAwait(false);
            if (reopen)
            {
                await storage.OpenAsync(NewPassword).WaitAsync(Timeout).ConfigureAwait(false);
            }

            var contact = await storage.GetContactAsync(newAddress, default).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(contact.FullName, Is.EqualTo("New contact"));
            Assert.That(contact.Email, Is.EqualTo(newAddress));
            Assert.That(await storage.ExistsContactWithEmailAddressAsync(oldAddress, default).ConfigureAwait(false), Is.False);
            await Assert.ThatAsync(async () => await storage.GetContactAsync(oldAddress, default).WaitAsync(Timeout).ConfigureAwait(false),
                Throws.TypeOf<DataBaseException>()).ConfigureAwait(false);
            await storage.RemoveContactAsync(newAddress, default).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(await storage.ExistsContactWithEmailAddressAsync(newAddress, default).ConfigureAwait(false), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        public async Task PausedBatchCannotCrossResetGeneration(int pauseBeforeIndex)
        {
            using var files = new StorageFiles();
            var storage = files.Storage;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            var oldAccount = CreateMessageAccount();
            await storage.AddAccountAsync(oldAccount).WaitAsync(Timeout).ConfigureAwait(false);
            var oldMessages = new[]
            {
                CreateStoredMessage(oldAccount.FoldersStructure.Single(), 1, "old-first"),
                CreateStoredMessage(oldAccount.FoldersStructure.Single(), 2, "old-second")
            };
            foreach (var message in oldMessages)
            {
                await storage.AddMessageAsync(Email, message, false).WaitAsync(Timeout).ConfigureAwait(false);
                message.Subject = "STALE-" + message.Subject;
                message.TextBody = "STALE-" + message.TextBody;
            }
            var paused = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var resume = new ManualResetEventSlim();
            var staleUpdate = Task.Run(() => storage.UpdateMessagesAsync(Email,
                PauseEnumeration(oldMessages, pauseBeforeIndex, paused, resume), false));
            Exception failure;
            try
            {
                await paused.Task.WaitAsync(Timeout).ConfigureAwait(false);
                await storage.ResetAsync().WaitAsync(Timeout).ConfigureAwait(false);
                await storage.CreateAsync(NewPassword).WaitAsync(Timeout).ConfigureAwait(false);
                var newAccount = CreateMessageAccount();
                await storage.AddAccountAsync(newAccount).WaitAsync(Timeout).ConfigureAwait(false);
                foreach (var message in new[]
                {
                    CreateStoredMessage(newAccount.FoldersStructure.Single(), 1, "new-first"),
                    CreateStoredMessage(newAccount.FoldersStructure.Single(), 2, "new-second")
                })
                {
                    await storage.AddMessageAsync(Email, message, false).WaitAsync(Timeout).ConfigureAwait(false);
                    Assert.That(message.Pk, Is.EqualTo(oldMessages.Single(x => x.Id == message.Id).Pk),
                        "The new database naturally reuses IDs; the test never assigns primary keys.");
                }
            }
            finally
            {
                resume.Set();
                failure = await ObserveClosingFailureAsync(staleUpdate).ConfigureAwait(false);
            }

            var actual = await storage.GetMessageListAsync(Email, "Archive", 0).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.Multiple(() =>
            {
                Assert.That(failure, Is.InstanceOf<DataBaseException>(), "An old batch must reject the replacement database.");
                Assert.That(actual.Select(x => x.Subject), Is.EquivalentTo(new[] { "new-first", "new-second" }));
                Assert.That(actual.Select(x => x.TextBody), Is.EquivalentTo(new[] { "body-new-first", "body-new-second" }));
            });
        }

        [Test]
        public async Task PausedFolderReadCannotCrossResetGeneration()
        {
            using var files = new StorageFiles();
            var storage = files.Storage;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            var oldAccount = CreateMessageAccount();
            await storage.AddAccountAsync(oldAccount).WaitAsync(Timeout).ConfigureAwait(false);
            await storage.AddMessageAsync(Email, CreateStoredMessage(oldAccount.FoldersStructure.Single(), 1, "old-message"), false)
                .WaitAsync(Timeout).ConfigureAwait(false);
            var paused = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var resume = new ManualResetEventSlim();
            var staleRead = Task.Run(() => storage.GetEarlierMessagesInFoldersAsync(
                PauseEnumeration(oldAccount.FoldersStructure, 0, paused, resume), 10, null, CancellationToken.None));
            Exception failure;
            try
            {
                await paused.Task.WaitAsync(Timeout).ConfigureAwait(false);
                await storage.ResetAsync().WaitAsync(Timeout).ConfigureAwait(false);
                await storage.CreateAsync(NewPassword).WaitAsync(Timeout).ConfigureAwait(false);
                var newAccount = CreateMessageAccount();
                await storage.AddAccountAsync(newAccount).WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(newAccount.FoldersStructure.Single().Id, Is.EqualTo(oldAccount.FoldersStructure.Single().Id));
                await storage.AddMessageAsync(Email, CreateStoredMessage(newAccount.FoldersStructure.Single(), 1, "new-message"), false)
                    .WaitAsync(Timeout).ConfigureAwait(false);
            }
            finally
            {
                resume.Set();
                failure = await ObserveClosingFailureAsync(staleRead).ConfigureAwait(false);
            }
            Assert.That(failure, Is.InstanceOf<DataBaseException>(), "An old read must not return messages from the new database.");
            var actual = await storage.GetEarlierMessagesAsync(null, 10, null, CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(actual, Is.Empty, "The recreated account has no Inbox folders.");
            var message = await storage.GetMessageAsync(Email, "Archive", 1).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(message.Subject, Is.EqualTo("new-message"));
            Assert.That(message.TextBody, Is.EqualTo("body-new-message"));
        }

        [Test]
        public async Task ContactEventCanResetRecreateAndUseNewGeneration()
        {
            using var files = new StorageFiles();
            var storage = files.Storage;
            await storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            Settings loaded = null;
            storage.ContactAdded += (_, _) =>
            {
                storage.ResetAsync().WaitAsync(Timeout).GetAwaiter().GetResult();
                storage.CreateAsync(NewPassword).WaitAsync(Timeout).GetAwaiter().GetResult();
                storage.SetSettingsAsync(new Settings { EppieAccountCounter = 73 }).WaitAsync(Timeout).GetAwaiter().GetResult();
                loaded = storage.GetSettingsAsync().WaitAsync(Timeout).GetAwaiter().GetResult();
            };
            var address = new EmailAddress("before-reset@lifecycle.example");
            await storage.AddContactAsync(new Contact("Old contact", address), CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That(loaded.EppieAccountCounter, Is.EqualTo(73));
            Assert.That(await storage.ExistsContactWithEmailAddressAsync(address, CancellationToken.None).WaitAsync(Timeout).ConfigureAwait(false), Is.False);
            await storage.OpenAsync(NewPassword).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That((await storage.GetSettingsAsync().WaitAsync(Timeout).ConfigureAwait(false)).EppieAccountCounter, Is.EqualTo(73));
        }

        private static IEnumerable<T> PauseEnumeration<T>(IEnumerable<T> values, int pauseBeforeIndex,
            TaskCompletionSource<bool> paused, ManualResetEventSlim resume)
        {
            int index = 0;
            foreach (var value in values)
            {
                if (index++ == pauseBeforeIndex)
                {
                    paused.SetResult(true);
                    if (!resume.Wait(Timeout))
                    {
                        throw new TimeoutException("Reset/create did not finish before resuming the old operation.");
                    }
                }
                yield return value;
            }
        }

        private static Account CreateMessageAccount()
        {
            var account = new Account { Email = Email, AuthData = new BasicAuthData { Password = "synthetic" } };
            account.FoldersStructure.Add(new Folder("Archive", FolderAttributes.None));
            return account;
        }

        private static Message CreateStoredMessage(Folder folder, uint id, string subject)
        {
            return new Message
            {
                Id = id,
                Folder = folder,
                Subject = subject,
                TextBody = "body-" + subject,
                IsMarkedAsRead = true,
                Date = DateTimeOffset.Parse("2026-10-29T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        private static Task ReplaceAsync(IDataStorage storage, bool rekey)
        {
            return rekey ? storage.ChangePasswordAsync(Password, NewPassword) : storage.OpenAsync(Password);
        }

        private static DecMessage CreateMessage(string hash)
        {
            return new DecMessage
            {
                Hash = hash,
                From = "sender@eppie",
                To = Email.Address,
                Date = DateTimeOffset.Parse("2026-01-02T03:04:05+00:00", System.Globalization.CultureInfo.InvariantCulture),
                Subject = "subject-" + hash,
                TextBody = "body-" + hash,
                MimeBody = new byte[] { 11, 22, 33 },
                FolderName = Inbox.FullName,
                FolderAttributes = Inbox.Attributes
            };
        }

        private static void AssertMessage(DecMessage message, string hash)
        {
            Assert.That(message.Hash, Is.EqualTo(hash));
            Assert.That(message.Subject, Is.EqualTo("subject-" + hash));
            Assert.That(message.TextBody, Is.EqualTo("body-" + hash));
            Assert.That(message.MimeBody, Is.EqualTo(new byte[] { 11, 22, 33 }));
        }

        private static async Task<HeldOperation> HoldOperationAsync(IDataStorage storage)
        {
            var held = new HeldOperation();
            var type = storage.GetType();
            MethodInfo method = null;
            while (type != null)
            {
                method = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .SingleOrDefault(x => x.Name == "ExecuteDatabaseAsync" && x.IsGenericMethodDefinition);
                if (method != null)
                {
                    break;
                }
                type = type.BaseType;
            }
            if (method == null)
            {
                throw new MissingMethodException("Storage operation boundary not found.");
            }
            Func<SQLiteConnection, CancellationToken, string> operation = (connection, ct) =>
            {
                held.Entered.SetResult(true);
                if (!held.Continue.Task.Wait(Timeout, ct))
                {
                    throw new TimeoutException("The active SQL callback was not released.");
                }
                held.Subject = connection.ExecuteScalar<string>("SELECT Subject FROM DecMessage WHERE Hash = ?", "original");
                return held.Subject;
            };
            held.Completion = (Task)method.MakeGenericMethod(typeof(string)).Invoke(storage, new object[] { operation, CancellationToken.None, null });
            await held.Entered.Task.WaitAsync(Timeout).ConfigureAwait(false);
            return held;
        }

        private sealed class HeldOperation : IDisposable
        {
            public TaskCompletionSource<bool> Entered { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> Continue { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task Completion { get; set; }
            public string Subject { get; set; }
            public void Release() => Continue.TrySetResult(true);
            public void Dispose() => Release();
        }

        private sealed class StorageFiles : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TuviCore-lifecycle-" + Guid.NewGuid().ToString("N") + ".db");
            public IDataStorage Storage { get; }

            public StorageFiles()
            {
                Storage = DataStorageProvider.GetDataStorage(Path);
            }

            public void Dispose()
            {
                Task.Run(Storage.Dispose).WaitAsync(Timeout).GetAwaiter().GetResult();
                foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
                {
                    File.Delete(Path + suffix);
                }
            }
        }
    }
}
