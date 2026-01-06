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
using System.Linq;
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
                return IsStorageExist();
            });
        }

        public Task CreateAsync(string password, CancellationToken cancellationToken)
        {
            return DoCancellableTaskAsync(async (ct) =>
            {
                if (IsStorageExist())
                {
                    throw new DataBaseAlreadyExistsException();
                }

                SQLiteAsyncConnection db = await CreateDBConnectionAsync(password, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create).ConfigureAwait(false); ;
                try
                {
                    await CreateOrMigrateTablesAsync(db).ConfigureAwait(false);
                    Database = db;
                }
                catch (SQLite.SQLiteException ex)
                {
                    await db.CloseAsync().ConfigureAwait(false);
                    File.Delete(Path);
                    throw new DataBaseException(ex.Message, ex);
                }
            }, cancellationToken);
        }

        private static Task CreateOrMigrateTablesAsync(SQLiteAsyncConnection db)
        {
            return db.RunInTransactionAsync(t =>
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
                t.CreateTable<ProtonMessageIdV3>();
                t.CreateTable<ProtonLabelV3>();
                t.CreateTables<ProtonMessageLabelV2, ProtonAuthData>();
                t.CreateTable<LocalAIAgent>();

                t.CreateIndex(nameof(Message), new[] { nameof(Message.Path), nameof(Message.Id) }, unique: true); // A hack, SQLite.Net still doesn't allow several [Indexed] attributes per property
                t.CreateIndex(nameof(ProtonMessageIdV3), new[] { nameof(ProtonMessageIdV3.AccountId), nameof(ProtonMessageIdV3.MessageId) }, unique: true);
                t.CreateIndex(nameof(ProtonMessageLabelV2), new[] { nameof(ProtonMessageLabelV2.AccountId), nameof(ProtonMessageLabelV2.MessageId), nameof(ProtonMessageLabelV2.LabelId) }, unique: true);
                t.CreateIndex(nameof(ProtonLabelV3), new[] { nameof(ProtonLabelV3.AccountId), nameof(ProtonLabelV3.LabelId) }, unique: true);
            });
        }

        public Task OpenAsync(string password, CancellationToken cancellationToken)
        {
            return DoCancellableTaskAsync(async (ct) =>
            {
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
                    newPassword = await GetPasswordHash(newPassword).ConfigureAwait(false);
                    await Database.ReKeyAsync(newPassword).ConfigureAwait(false);
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
            return Database != null &&
                await IsDataStorageOpenedCorrectlyAsync().ConfigureAwait(false);
        }

        private static bool IsWasm()
        {
            return System.Runtime.InteropServices.RuntimeInformation.OSDescription == "Browser";
        }

        private static async Task<string> GetPasswordHash(string password)
        {
            if (IsWasm())
            {
                // unfortunately Konscious.Security.Cryptography.Argon2 lib doesn't support WebAssembly for some unknown reason,
                // so we temporary isolate the password hashing part
                // TVM-508
                return password;
            }
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
            if (!IsStorageExist())
            {
                throw new DataBaseNotCreatedException();
            }

            // close previous connection (if any) before creating a new one to avoid leaking file handles.
            var existing = Database;
            if (existing != null)
            {
                await existing.CloseAsync().ConfigureAwait(false);
                Database = null;
                SQLiteAsyncConnection.ResetPool();
            }

            var db = await CreateDBConnectionAsync(password, SQLiteOpenFlags.ReadWrite).ConfigureAwait(false);
            try
            {
                await MigrateAsync(db).ConfigureAwait(false);
                Database = db;
            }
            catch (SQLite.SQLiteException ex)
            {
                await db.CloseAsync().ConfigureAwait(false);
                if (ex.Result == SQLite3.Result.NonDBFile)
                {
                    throw new DataBasePasswordException(ex.Message, ex);
                }
                throw new DataBaseMigrationException(ex.Message, ex);
            }
        }

        private static async Task MigrateAsync(SQLiteAsyncConnection db)
        {
            // TODO: add version check and manual migration code if needed
            // var version = GetCurrentVersion()
            //switch(version)
            //{
            //case Version1:
            //    MigrateToVersion1();
            ////    ...
            //case VersionN: 
            //     MigrateToVersionN();
            //     SetCurrentVersion(LastVersion);

            //case LastVersion:
            //    break;
            //}

            // Automatic migration
            await CreateOrMigrateTablesAsync(db).ConfigureAwait(false);

            // --- Data migration (18.05.2025) ---
            // LastMessageData.AccountEmailId -> LastMessageData.AccountId
            // Account.EmailId -> Account.Email
            await DataMigration18052025(db).ConfigureAwait(false);
        }

        private static async Task DataMigration18052025(SQLiteAsyncConnection db)
        {
#pragma warning disable CS0618 // Only for migration purposes
            // --- Data migration (18.05.2025): LastMessageData.AccountEmailId -> LastMessageData.AccountId ---
            {
                var lastMessages = await db.Table<LastMessageData>().ToListAsync().ConfigureAwait(false);

                bool needLmdMigration = lastMessages.Any(lmd => lmd.AccountId == 0 && lmd.AccountEmailId != 0);

                if (needLmdMigration)
                {
                    var accounts = await db.Table<Account>().ToListAsync().ConfigureAwait(false);
                    foreach (var lmd in lastMessages)
                    {
                        // If AccountId is not set and AccountEmailId is present, migrate the data
                        if (lmd.AccountId == 0 && lmd.AccountEmailId != 0)
                        {
                            var account = accounts.FirstOrDefault(a => a.EmailId == lmd.AccountEmailId);
                            if (account != null)
                            {
                                lmd.AccountId = account.Id;
                                lmd.AccountEmailId = 0; // Reset AccountEmailId to avoid future migrations
                                await db.UpdateAsync(lmd).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }

            // --- Data migration (18.05.2025): Account.EmailId -> Account.Email ---            
            {
                var accounts = await db.Table<Account>().ToListAsync().ConfigureAwait(false);
                bool needMigration = accounts.Any(a => string.IsNullOrEmpty(a.EmailAddress) && a.EmailId != 0);

                if (needMigration)
                {
                    foreach (var account in accounts)
                    {
                        // If EmailAddress is not set and EmailId is present, migrate the data
                        if (string.IsNullOrEmpty(account.EmailAddress) && account.EmailId != 0)
                        {
                            var emailData = await db.FindAsync<EmailAddressData>(account.EmailId).ConfigureAwait(false);
                            if (emailData != null)
                            {
                                account.Email = new EmailAddress(emailData.Address, emailData.Name);
                                account.EmailId = 0; // Reset EmailId to avoid future migrations
                                await db.UpdateAsync(account).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
#pragma warning restore CS0618 // Only for migration purposes
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

        private async Task<SQLiteAsyncConnection> CreateDBConnectionAsync(string password, SQLiteOpenFlags flags)
        {
            var hashedPassword = await GetPasswordHash(password).ConfigureAwait(false);
            var options = new SQLite.SQLiteConnectionString(Path, flags, storeDateTimeAsTicks: true, key: hashedPassword);

            var db = new SQLite.SQLiteAsyncConnection(options);
            //db.Trace = true;
            //db.Tracer = (s) => { Debug.WriteLine(s); };
            return db;
        }

        private bool IsStorageExist()
        {
            return File.Exists(Path);
        }
    }
}
