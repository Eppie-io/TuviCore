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
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SQLite;
using Tuvi.Core.DataStorage.Impl;
using Tuvi.Core.Entities;

namespace Tuvi.Core.DataStorage.Tests
{
    [NonParallelizable]
    public class StoragePreparationTests
    {
        private const string Password = "preparation-old";
        private const string NewPassword = "preparation-new";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

        [TestCase(false)]
        [TestCase(true)]
        public async Task LateWorkerAfterStartTimeoutAndCleanupRemainsSafeAndObserved(bool failOperation)
        {
            using var pause = new WorkerPause();
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var failure = new InvalidOperationException("Late operation failure");
            var timeout = Assert.Throws<TimeoutException>(() => pause.Start(() => completion.Task, TimeSpan.Zero));
            Assert.That(timeout.Message, Is.EqualTo("The preparation worker did not enter."));
            Assert.That(pause.Operation, Is.SameAs(completion.Task));
            pause.Dispose();
            pause.Dispose();
            try
            {
                await Task.Run(pause.EnterWorker).WaitAsync(Timeout).ConfigureAwait(false);
            }
            finally
            {
                if (failOperation)
                {
                    completion.TrySetException(failure);
                }
                else
                {
                    completion.TrySetResult(true);
                }
                var observed = await pause.Observation.WaitAsync(Timeout).ConfigureAwait(false);
                Assert.That(observed?.InnerException, failOperation ? Is.SameAs(failure) : Is.Null);
            }
            Assert.That(pause.TimedOut, Is.True, "A late callback must not erase the original entry timeout.");
        }

        [Test]
        public async Task CleanupReleasesAnAlreadyWaitingWorker()
        {
            using var pause = new WorkerPause();
            var operation = pause.Start(() => Task.Run(() => 42));
            try
            {
                Assert.That(operation.IsCompleted, Is.False);
            }
            finally
            {
                pause.Dispose();
                await operation.WaitAsync(Timeout).ConfigureAwait(false);
                await pause.Observation.WaitAsync(Timeout).ConfigureAwait(false);
            }
            Assert.That(pause.TimedOut, Is.False);
            Assert.That(((Task<int>)operation).Result, Is.EqualTo(42));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CreatePreparationHasNoFileSideEffects(bool invalidPassword)
        {
            using var files = new StorageFiles();
            using var cancellation = new CancellationTokenSource();
            using var pause = new WorkerPause();
            var operation = pause.Start(() => files.Storage.CreateAsync(invalidPassword ? string.Empty : Password, cancellation.Token));
            try
            {
                Assert.That(Directory.GetFileSystemEntries(files.DirectoryPath), Is.Empty,
                    "Key preparation must finish before the final file or any sidecar is created.");
                if (!invalidPassword)
                {
                    cancellation.Cancel();
                }
            }
            finally
            {
                pause.Release();
                if (invalidPassword)
                {
                    await Assert.ThatAsync(async () => await operation.WaitAsync(Timeout).ConfigureAwait(false),
                        Throws.TypeOf<ArgumentException>()).ConfigureAwait(false);
                }
                else
                {
                    cancellation.Cancel();
                    await AssertCancelledAsync(operation, cancellation.Token).ConfigureAwait(false);
                }
            }
            Assert.That(pause.TimedOut, Is.False);
            Assert.That(Directory.GetFileSystemEntries(files.DirectoryPath), Is.Empty);
            await files.Storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await files.Storage.SetSettingsAsync(new Settings { EppieAccountCounter = 42 }).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That((await files.Storage.GetSettingsAsync().WaitAsync(Timeout).ConfigureAwait(false)).EppieAccountCounter, Is.EqualTo(42));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CancellationDuringPreparationPreservesWorkingConnection(bool rekey)
        {
            using var files = new StorageFiles();
            await files.Storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await files.Storage.SetSettingsAsync(new Settings { EppieAccountCounter = 42 }).WaitAsync(Timeout).ConfigureAwait(false);
            using var cancellation = new CancellationTokenSource();
            using var pause = new WorkerPause();
            var operation = pause.Start(() => rekey
                ? files.Storage.ChangePasswordAsync(Password, NewPassword, cancellation.Token)
                : files.Storage.OpenAsync(Password, cancellation.Token));
            try
            {
                cancellation.Cancel();
            }
            finally
            {
                pause.Release();
                await AssertCancelledAsync(operation, cancellation.Token).ConfigureAwait(false);
            }
            Assert.That(pause.TimedOut, Is.False);
            Assert.That((await files.Storage.GetSettingsAsync().WaitAsync(Timeout).ConfigureAwait(false)).EppieAccountCounter, Is.EqualTo(42),
                "Cancellation during preparation must not require a recovery OpenAsync.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CancellationAfterClosingBeginsCompletesReplacementAndStopsBeforeRekey(bool rekey)
        {
            using var files = new StorageFiles();
            await files.Storage.CreateAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            await files.Storage.SetSettingsAsync(new Settings { EppieAccountCounter = 42 }).WaitAsync(Timeout).ConfigureAwait(false);
            using var cancellation = new CancellationTokenSource();
            var storageType = files.Storage.GetType().BaseType.BaseType;
            var database = storageType.GetField("_database", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(files.Storage);
            var connectionField = database.GetType().GetField("_connection", BindingFlags.Instance | BindingFlags.NonPublic);
            var hashMethod = storageType.GetMethod("GetPasswordHash", BindingFlags.Static | BindingFlags.NonPublic);
            var hash = await ((Task<string>)hashMethod.Invoke(null, new object[] { Password })).WaitAsync(Timeout).ConfigureAwait(false);
            ((SQLiteConnectionWithLock)connectionField.GetValue(database)).Dispose();
            using var controlled = new CancelOnCloseConnection(new SQLiteConnectionString(files.Path, SQLiteOpenFlags.ReadWrite,
                storeDateTimeAsTicks: true, key: hash), cancellation.Cancel);
            connectionField.SetValue(database, controlled);
            var operation = rekey
                ? files.Storage.ChangePasswordAsync(Password, NewPassword, cancellation.Token)
                : files.Storage.OpenAsync(Password, cancellation.Token);
            if (rekey)
            {
                await AssertCancelledAsync(operation, cancellation.Token).ConfigureAwait(false);
            }
            else
            {
                await operation.WaitAsync(Timeout).ConfigureAwait(false);
            }
            Assert.That(cancellation.IsCancellationRequested, Is.True);
            Assert.That(controlled.CloseCount, Is.EqualTo(1));
            Assert.That((await files.Storage.GetSettingsAsync().WaitAsync(Timeout).ConfigureAwait(false)).EppieAccountCounter, Is.EqualTo(42));
            files.Storage.Dispose();
            using var reopened = DataStorageProvider.GetDataStorage(files.Path);
            await reopened.OpenAsync(Password).WaitAsync(Timeout).ConfigureAwait(false);
            Assert.That((await reopened.GetSettingsAsync().WaitAsync(Timeout).ConfigureAwait(false)).EppieAccountCounter, Is.EqualTo(42));
            await Assert.ThatAsync(async () => await reopened.OpenAsync(NewPassword).WaitAsync(Timeout).ConfigureAwait(false),
                Throws.TypeOf<DataBasePasswordException>()).ConfigureAwait(false);
        }

        private static async Task AssertCancelledAsync(Task operation, CancellationToken token)
        {
            var failure = await Assert.CatchAsync<OperationCanceledException>(async () => await operation.WaitAsync(Timeout).ConfigureAwait(false)).ConfigureAwait(false);
            Assert.That(failure.CancellationToken, Is.EqualTo(token));
        }

        private sealed class CancelOnCloseConnection(SQLiteConnectionString options, Action onClose) : SQLiteConnectionWithLock(options)
        {
            public int CloseCount { get; private set; }

            protected override void Dispose(bool disposing)
            {
                if (disposing && CloseCount++ == 0)
                {
                    onClose();
                }
                base.Dispose(disposing);
            }
        }

        private sealed class WorkerPause : IDisposable
        {
            private static readonly AsyncLocal<WorkerPause> Current = new AsyncLocal<WorkerPause>(OnContextChanged);
            private readonly TaskCompletionSource<bool> _entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _used;
            private int _callerThread;
            private int _timedOut;
            public bool TimedOut => Volatile.Read(ref _timedOut) != 0;
            public Task Operation { get; private set; }
            public Task<AggregateException> Observation { get; private set; }

            public Task Start(Func<Task> operation, TimeSpan? entryTimeout = null)
            {
                _callerThread = Environment.CurrentManagedThreadId;
                var previous = Current.Value;
                Current.Value = this;
                try
                {
                    Operation = operation();
                    Observation = Operation.ContinueWith(task => task.Exception, CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    if (!_entered.Task.Wait(entryTimeout ?? Timeout))
                    {
                        Interlocked.Exchange(ref _timedOut, 1);
                        Release();
                        throw new TimeoutException("The preparation worker did not enter.");
                    }
                    return Operation;
                }
                finally
                {
                    Current.Value = previous;
                }
            }

            private static void OnContextChanged(AsyncLocalValueChangedArgs<WorkerPause> args)
            {
                var pause = args.CurrentValue;
                if (args.ThreadContextChanged && pause != null && Environment.CurrentManagedThreadId != pause._callerThread)
                {
                    pause.EnterWorker();
                }
            }

            public void EnterWorker()
            {
                if (Interlocked.Exchange(ref _used, 1) == 0)
                {
                    _entered.TrySetResult(true);
                    if (!_resume.Task.Wait(Timeout))
                    {
                        Interlocked.Exchange(ref _timedOut, 1);
                    }
                }
            }

            public void Release() => _resume.TrySetResult(true);
            public void Dispose() => Release();
        }

        private sealed class StorageFiles : IDisposable
        {
            public string DirectoryPath { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TuviCore-preparation-" + Guid.NewGuid().ToString("N"));
            public string Path => System.IO.Path.Combine(DirectoryPath, "storage.db");
            public IDataStorage Storage { get; }

            public StorageFiles()
            {
                Directory.CreateDirectory(DirectoryPath);
                Storage = DataStorageProvider.GetDataStorage(Path);
            }

            public void Dispose()
            {
                Storage.Dispose();
                foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
                {
                    File.Delete(Path + suffix);
                }
                Directory.Delete(DirectoryPath);
            }
        }
    }
}
