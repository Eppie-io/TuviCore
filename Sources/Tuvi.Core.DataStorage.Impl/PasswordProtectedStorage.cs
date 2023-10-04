using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Konscious.Security.Cryptography;
using SQLite;
using Tuvi.Core.Entities;

namespace Tuvi.Core.DataStorage.Impl
{
    internal abstract class PasswordProtectedStorage : IPasswordProtectedStorage, IDisposable
    {
        private CancellationTokenSource _rootCTS;
        protected bool _isDisposed;

        private SemaphoreSlim _cancellableTasksSemaphore = new SemaphoreSlim(1);
        private int _cancellableTasksCount;
        private object _databaseLock = new object();
        protected SQLite.SQLiteAsyncConnection _database;
        protected SQLite.SQLiteAsyncConnection Database
        {
            get
            {
                lock (_databaseLock)
                    return _database;
            }
            set
            {
                lock (_databaseLock)
                    _database = value;
            }
        }
        private readonly string Path;

        public PasswordProtectedStorage(string filePath)
        {
            Path = filePath;
            _rootCTS = new CancellationTokenSource();
        }

        public Task<bool> IsStorageExistAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                return File.Exists(Path);
            });
        }

        public Task OpenAsync(string password, CancellationToken cancellationToken)
        {
            return DoCancellableTaskAsync(async (ct) =>
            {
                if (await IsAlreadyOpenedAsync().ConfigureAwait(false))
                {
                    return;
                }

                await OpenImplAsync(password).ConfigureAwait(false);
            }, cancellationToken);

        }

        public Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken)
        {
            return DoCancellableTaskAsync(async (ct) =>
            {
                await OpenImplAsync(currentPassword).ConfigureAwait(false);

                try
                {
                    if (!IsWasm())
                    {
                        // unfortunately Konscious.Security.Cryptography.Argon2 lib doesn't support WebAssembly for some unknown reason,
                        // so we temporary isolate the password hashing part
                        // TVM-508
                        newPassword = await GetPasswordHash(newPassword).ConfigureAwait(false);
                    }

                    await Database.ExecuteAsync($"PRAGMA rekey = \"{newPassword}\";").ConfigureAwait(false);
                }
                catch (SQLite.SQLiteException exp)
                {
                    throw new DataBaseException(exp.Message, exp);
                }
            }, cancellationToken);
        }

        public async Task ResetAsync()
        {
            try
            {
                await ResetDataStorageAsync().ConfigureAwait(false);
            }
            catch (SQLite.SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }

        private async Task<bool> IsAlreadyOpenedAsync()
        {
            return (Database != null &&
                await IsDataStorageOpenedCorrectlyAsync().ConfigureAwait(false));
        }

        private static bool IsWasm()
        {
            return System.Runtime.InteropServices.RuntimeInformation.OSDescription == "Browser";
        }

        private static async Task<string> GetPasswordHash(string password)
        {
            using (var argon2 = new Argon2i(Encoding.UTF8.GetBytes(password))
            {
                DegreeOfParallelism = 16,
                Iterations = 40,
                MemorySize = 8192,
            })
            {
                var hash = await argon2.GetBytesAsync(32).ConfigureAwait(false);

                // convert bytes to hex string
                return $"x'{StringHelper.BytesToHex(hash)}'";
            }
        }

        private async Task OpenImplAsync(string password)
        {
            var db = Database;
            try
            {
                if (db != null)
                {
                    await db.CloseAsync().ConfigureAwait(false);
                }

                if (!IsWasm())
                {
                    // unfortunately Konscious.Security.Cryptography.Argon2 lib doesn't support WebAssembly for some unknown reason,
                    // so we temporary isolate the password hashing part
                    // TVM-508
                    password = await GetPasswordHash(password).ConfigureAwait(false);
                }

                var options = new SQLite.SQLiteConnectionString(Path, true, key: password);

                db = new SQLite.SQLiteAsyncConnection(options);
                //db.Trace = true;
                //db.Tracer = (s) => { Debug.WriteLine(s); };

                await db.RunInTransactionAsync(t =>
                {
                    t.CreateTable<Files>();
                    t.CreateTables<Contact, ImageInfo, LastMessageData>();
                    t.CreateTables<Account, EmailAddressData, BasicAuthData, OAuth2Data, Folder>();
                    t.CreateTables<Entities.Message, ProtectionInfo, SignatureInfo, Attachment, MessageEmailAddress>();
                    t.CreateTable<MessageContact>();
                    t.CreateTable<DecMessage>();
                    t.CreateTable<SettingsTable>();
                    t.CreateTable<AccountGroup>();
                    t.CreateTable<Proton.Message>();
                    t.CreateTable<ProtonMessageId>();
                    t.CreateTable<ProtonLabel>();
                    t.CreateTable<ProtonMessageLabel>();
                    t.CreateIndex(nameof(Message), new[] { nameof(Message.Path), nameof(Message.Id) }, unique: true); // A hack, SQLite.Net still doesn't allow several [Intexed] attributes per property
                }).ConfigureAwait(false);
                Database = db;
                await IsDataStorageOpenedCorrectlyAsync().ConfigureAwait(false);
            }
            catch (SQLite.SQLiteException exp)
            {
                await db.CloseAsync().ConfigureAwait(false);
                Database = null;
                throw new DataBasePasswordException(exp.Message, exp);
            }
        }

        private async Task<bool> IsDataStorageOpenedCorrectlyAsync()
        {
            try
            {
                var db = Database;
                if (db == null)
                {
                    return false;
                }
                var list = await db.Table<Files>().ToListAsync().ConfigureAwait(false);

                return true;
            }
            catch (SQLite.SQLiteException)
            {
                return false;
            }
        }

        private async Task ResetDataStorageAsync()
        {
            if (Database != null)
            {
                _rootCTS?.Cancel();

                await _cancellableTasksSemaphore.WaitAsync().ConfigureAwait(true);
                try
                {
                    await Database.CloseAsync().ConfigureAwait(false);
                    Database = null;
                }
                finally
                {
                    _cancellableTasksSemaphore.Release();
                }
            }

            await Task.Run(() => File.Delete(Path)).ConfigureAwait(false);
        }

        protected async Task DoCancellableTaskAsync(Func<CancellationToken, Task> task, CancellationToken cancellationToken)
        {
            _rootCTS.Token.ThrowIfCancellationRequested();
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_rootCTS.Token, cancellationToken))
            {
                var t = task(cts.Token);
                if (Interlocked.Increment(ref _cancellableTasksCount) == 1)
                {
                    await _cancellableTasksSemaphore.WaitAsync(cancellationToken).ConfigureAwait(true);
                }
                try
                {
                    await t.ConfigureAwait(true);
                }
                finally
                {
                    if (Interlocked.Decrement(ref _cancellableTasksCount) == 0)
                    {
                        _cancellableTasksSemaphore.Release();
                    }
                }
            }
        }

        protected async Task<TResult> DoCancellableTaskAsync<TResult>(Func<CancellationToken, Task<TResult>> task, CancellationToken cancellationToken)
        {
            _rootCTS.Token.ThrowIfCancellationRequested();
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_rootCTS.Token, cancellationToken))
            {
                var t = task(cts.Token);
                if (Interlocked.Increment(ref _cancellableTasksCount) == 1)
                {
                    await _cancellableTasksSemaphore.WaitAsync(cancellationToken).ConfigureAwait(true);
                }
                try
                {
                    return await t.ConfigureAwait(true);
                }
                finally
                {
                    if (Interlocked.Decrement(ref _cancellableTasksCount) == 0)
                    {
                        _cancellableTasksSemaphore.Release();
                    }
                }
            }
        }

        protected void CheckDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(DataStorage));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }
            if (disposing)
            {
                // release managed resource(actually it holds unmanaged resources internally)
                if (Database != null)
                {
                    SQLiteAsyncConnection.ResetPool();
                }
                _rootCTS?.Dispose();
                _cancellableTasksSemaphore.Dispose();
            }
            _isDisposed = true;
        }
    }
}
