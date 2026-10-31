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
        private readonly object _stateLock = new object();
        private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);
        private int _gateUsers;
        private long _databaseGeneration;
        private TaskCompletionSource<bool> _resetCompletion;
        private TaskCompletionSource<bool> _disposeCompletion;
        private StorageConnection _database;

        private StorageConnection Database => _database ?? throw new DataBaseException("Database is not opened");
        protected bool IsDisposed
        {
            get
            {
                lock (_stateLock)
                {
                    return _disposeCompletion != null;
                }
            }
        }
        private readonly string Path;

        public PasswordProtectedStorage(string filePath)
        {
            // One storage instance owns each database file. Its operation gate
            // protects the connection throughout every admitted operation.
            Path = System.IO.Path.GetFullPath(filePath);
        }

        public Task<bool> IsStorageExistAsync(CancellationToken cancellationToken)
        {
            return RunOperationAsync(ct => Task.FromResult(IsStorageExist()), cancellationToken);
        }

        public Task CreateAsync(string password, CancellationToken cancellationToken)
        {
            return RunOperationAsync(async (ct) =>
            {
                if (IsStorageExist())
                {
                    throw new DataBaseAlreadyExistsException();
                }

                await CloseDatabaseAsync().ConfigureAwait(false);
                StorageConnection db = null;
                bool created = false;
                try
                {
                    using (new FileStream(Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                    {
                    }
                    created = true;
                    db = await CreateDBConnectionAsync(password, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create, ct).ConfigureAwait(false);
                    await CreateOrMigrateTablesAsync(db).ConfigureAwait(false);
                    _database = db;
                }
                catch (Exception ex)
                {
                    if (db != null)
                    {
                        await db.CloseAsync().ConfigureAwait(false);
                    }
                    if (created)
                    {
                        File.Delete(Path);
                    }
                    if (ex is SQLiteException)
                    {
                        throw new DataBaseException(ex.Message, ex);
                    }
                    throw;
                }
            }, cancellationToken);
        }

        private static Task CreateOrMigrateTablesAsync(StorageConnection db)
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
            return RunOperationAsync(ct => OpenImplAsync(password, ct), cancellationToken);
        }

        public Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken)
        {
            return RunOperationAsync(async (ct) =>
            {
                await OpenImplAsync(currentPassword, ct).ConfigureAwait(false);

                try
                {
                    newPassword = await GetPasswordHash(newPassword).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    await Database.RunAsync(connection => connection.ReKey(newPassword)).ConfigureAwait(false);
                }
                catch (SQLite.SQLiteException exp)
                {
                    throw new DataBaseException(exp.Message, exp);
                }
            }, cancellationToken);
        }

        public Task ResetAsync()
        {
            TaskCompletionSource<bool> completion;
            lock (_stateLock)
            {
                CheckDisposed();
                if (_resetCompletion != null)
                {
                    return _resetCompletion.Task;
                }
                // Invalidate calls paused between database steps, including
                // waiters that resume after reset/create has already finished.
                ++_databaseGeneration;
                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _resetCompletion = completion;
                ++_gateUsers;
            }
            _ = CompleteCloseAsync(completion, reset: true);
            return completion.Task;
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

        private async Task OpenImplAsync(string password, CancellationToken cancellationToken)
        {
            if (!IsStorageExist())
            {
                throw new DataBaseNotCreatedException();
            }

            await CloseDatabaseAsync().ConfigureAwait(false);

            StorageConnection db = null;
            try
            {
                db = await CreateDBConnectionAsync(password, SQLiteOpenFlags.ReadWrite, cancellationToken).ConfigureAwait(false);
                // A freshly owned physical connection must actually read the
                // encrypted schema before the password can be accepted.
                await db.RunAsync(connection => connection.ExecuteScalar<int>("SELECT count(*) FROM sqlite_master")).ConfigureAwait(false);
                await MigrateAsync(db).ConfigureAwait(false);
                _database = db;
            }
            catch (Exception ex)
            {
                if (db != null)
                {
                    await db.CloseAsync().ConfigureAwait(false);
                }
                if (ex is SQLiteException sqliteException)
                {
                    if (sqliteException.Result == SQLite3.Result.NonDBFile)
                    {
                        throw new DataBasePasswordException(ex.Message, ex);
                    }
                    throw new DataBaseMigrationException(ex.Message, ex);
                }
                throw;
            }
        }

        private static async Task MigrateAsync(StorageConnection db)
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
            await DataMigration18052025Async(db).ConfigureAwait(false);

            // --- Data migration (09.03.2026) ---
            // legacy named decentralized Eppie accounts: Account.EmailName -> Account.DecentralizedName
            await DataMigration09032026Async(db).ConfigureAwait(false);
        }

        private static async Task DataMigration18052025Async(StorageConnection db)
        {
#pragma warning disable CS0618 // Only for migration purposes
            // --- Data migration (18.05.2025): LastMessageData.AccountEmailId -> LastMessageData.AccountId ---
            {
                var lastMessages = await db.RunAsync(connection => connection.Table<LastMessageData>().ToList()).ConfigureAwait(false);

                bool needLmdMigration = lastMessages.Any(lmd => lmd.AccountId == 0 && lmd.AccountEmailId != 0);

                if (needLmdMigration)
                {
                    var accounts = await db.RunAsync(connection => connection.Table<Account>().ToList()).ConfigureAwait(false);
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
                                await db.RunAsync(connection => connection.Update(lmd)).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }

            // --- Data migration (18.05.2025): Account.EmailId -> Account.Email ---            
            {
                var accounts = await db.RunAsync(connection => connection.Table<Account>().ToList()).ConfigureAwait(false);
                bool needMigration = accounts.Any(a => string.IsNullOrEmpty(a.EmailAddress) && a.EmailId != 0);

                if (needMigration)
                {
                    foreach (var account in accounts)
                    {
                        // If EmailAddress is not set and EmailId is present, migrate the data
                        if (string.IsNullOrEmpty(account.EmailAddress) && account.EmailId != 0)
                        {
                            var emailData = await db.RunAsync(connection => connection.Find<EmailAddressData>(account.EmailId)).ConfigureAwait(false);
                            if (emailData != null)
                            {
                                account.Email = new EmailAddress(emailData.Address, emailData.Name);
                                account.EmailId = 0; // Reset EmailId to avoid future migrations
                                await db.RunAsync(connection => connection.Update(account)).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
#pragma warning restore CS0618 // Only for migration purposes
        }

        private static async Task DataMigration09032026Async(StorageConnection db)
        {
#pragma warning disable CS0618 // Only for migration purposes
            // --- Data migration (09.03.2026): legacy named decentralized Eppie accounts ---
            {
                var accounts = await db.RunAsync(connection => connection.Table<Account>().ToList()).ConfigureAwait(false);
                bool needMigration = accounts.Any(NeedsLegacyDecentralizedNameMigration);

                if (needMigration)
                {
                    foreach (var account in accounts.Where(NeedsLegacyDecentralizedNameMigration))
                    {
                        account.DecentralizedName = account.EmailName;
                        account.EmailName = null;
                        await db.RunAsync(connection => connection.Update(account)).ConfigureAwait(false);
                    }
                }
            }
        }

        private static bool NeedsLegacyDecentralizedNameMigration(Account account)
        {
            if (account is null
                || string.IsNullOrWhiteSpace(account.EmailAddress)
                || !account.EmailAddress.EndsWith("@eppie", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(account.EmailName)
                || !string.IsNullOrWhiteSpace(account.DecentralizedName))
            {
                return false;
            }

            var localPartEnd = account.EmailAddress.IndexOf('@');
            if (localPartEnd <= 0)
            {
                return false;
            }

            var localPart = account.EmailAddress.Substring(0, localPartEnd);
            return !string.Equals(account.EmailName, localPart, StringComparison.Ordinal);
#pragma warning restore CS0618 // Only for migration purposes
        }

        // Admission runs on the caller before any wait or Task.Run. SQL callbacks
        // stay synchronous and materialize their result while the gate is held.
        protected Task<T> ExecuteDatabaseAsync<T>(Func<SQLiteConnection, CancellationToken, T> operation, CancellationToken cancellationToken, long? generation = null)
        {
            return RunOperationAsync(ct => Database.RunAsync(connection => operation(connection, ct)),
                cancellationToken, generation);
        }

        protected Task ExecuteDatabaseAsync(Action<SQLiteConnection, CancellationToken> operation, CancellationToken cancellationToken, long? generation = null)
        {
            return RunOperationAsync(ct => Database.RunAsync(connection => operation(connection, ct)),
                cancellationToken, generation);
        }

        private Task RunOperationAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken, long? generation = null)
        {
            return RunOperationAsync(async ct =>
            {
                await operation(ct).ConfigureAwait(false);
                return true;
            }, cancellationToken, generation);
        }

        private async Task<TResult> RunOperationAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken, long? generation = null)
        {
            long admittedGeneration = RegisterGateUser(generation);
            return await RunRegisteredOperationAsync(operation, admittedGeneration, cancellationToken).ConfigureAwait(false);
        }

        private async Task<TResult> RunRegisteredOperationAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, long generation, CancellationToken cancellationToken)
        {
            bool entered = false;
            try
            {
                await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                entered = true;
                CheckOperationAllowed(generation);
                cancellationToken.ThrowIfCancellationRequested();
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                UnregisterGateUser(entered);
            }
        }

        protected void ExecuteDatabase(Action<SQLiteConnection> operation)
        {
            long generation = RegisterGateUser();
            ExecuteRegisteredDatabase(operation, generation);
        }

        private void ExecuteRegisteredDatabase(Action<SQLiteConnection> operation, long generation)
        {
            bool entered = false;
            try
            {
                _operationGate.Wait();
                entered = true;
                CheckOperationAllowed(generation);
                Database.Run(operation);
            }
            finally
            {
                UnregisterGateUser(entered);
            }
        }

        private long RegisterGateUser(long? generation = null)
        {
            lock (_stateLock)
            {
                CheckOperationAllowed(generation);
                ++_gateUsers;
                return _databaseGeneration;
            }
        }

        private void UnregisterGateUser(bool entered)
        {
            try
            {
                if (entered)
                {
                    _operationGate.Release();
                }
            }
            finally
            {
                lock (_stateLock)
                {
                    // A registered caller can still be paused before Wait. Only
                    // the last user may dispose, after its own Release has returned.
                    if (--_gateUsers == 0 && _disposeCompletion != null)
                    {
                        _operationGate.Dispose();
                    }
                }
            }
        }

        // Composite methods retain this value while releasing the gate between
        // steps. Events get no ambient scope and can start fresh storage calls.
        protected long CaptureDatabaseGeneration(long? generation = null)
        {
            lock (_stateLock)
            {
                CheckOperationAllowed(generation);
                return _databaseGeneration;
            }
        }

        private void CheckOperationAllowed(long? generation = null)
        {
            lock (_stateLock)
            {
                CheckDisposed();
                if (_resetCompletion != null)
                {
                    throw new DataBaseException("Database is being reset");
                }
                if (generation.HasValue && generation.Value != _databaseGeneration)
                {
                    throw new DataBaseException("Database was reset during the operation");
                }
            }
        }

        protected void CheckDisposed()
        {
            lock (_stateLock)
            {
                if (_disposeCompletion != null)
                {
                    throw new ObjectDisposedException(nameof(DataStorage));
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }
            TaskCompletionSource<bool> completion;
            bool close;
            lock (_stateLock)
            {
                close = _disposeCompletion == null;
                if (close)
                {
                    _disposeCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    ++_gateUsers;
                }
                completion = _disposeCompletion;
            }
            if (close)
            {
                _ = CompleteCloseAsync(completion, reset: false);
            }
            completion.Task.GetAwaiter().GetResult();
        }

        // Closing rejects new work immediately, then drains the current holder.
        // Completion is published after releasing the gate and finishing state cleanup.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "All failures are propagated through the shared completion after releasing the gate.")]
        private async Task CompleteCloseAsync(TaskCompletionSource<bool> completion, bool reset)
        {
            Exception failure = null;
            bool entered = false;
            try
            {
                try
                {
                    await _operationGate.WaitAsync().ConfigureAwait(false);
                    entered = true;
                    await CloseDatabaseAsync().ConfigureAwait(false);
                    if (reset)
                    {
                        File.Delete(Path);
                    }
                }
                finally
                {
                    UnregisterGateUser(entered);
                }
            }
            catch (Exception ex)
            {
                failure = reset && ex is SQLiteException ? new DataBaseException(ex.Message, ex) : ex;
            }
            finally
            {
                if (reset)
                {
                    lock (_stateLock)
                    {
                        _resetCompletion = null;
                    }
                }
            }
            if (failure == null)
            {
                completion.SetResult(true);
            }
            else
            {
                completion.SetException(failure);
            }
        }

        private async Task CloseDatabaseAsync()
        {
            var database = _database;
            _database = null;
            InvalidateCaches();
            if (database != null)
            {
                await database.CloseAsync().ConfigureAwait(false);
            }
        }

        protected virtual void InvalidateCaches()
        {
        }

        private async Task<StorageConnection> CreateDBConnectionAsync(string password, SQLiteOpenFlags flags, CancellationToken cancellationToken)
        {
            var hashedPassword = await GetPasswordHash(password).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var options = new SQLite.SQLiteConnectionString(Path, flags, storeDateTimeAsTicks: true, key: hashedPassword);

            return new StorageConnection(options);
        }

        private bool IsStorageExist()
        {
            return File.Exists(Path);
        }
    }
}
