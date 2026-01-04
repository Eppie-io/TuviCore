// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2025 Eppie (https://eppie.io)                                    //
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BackupServiceClientLibrary;
using Microsoft.Extensions.Logging;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Dec;
using Tuvi.Core.Dec.Names;
using Tuvi.Core.Entities;
using Tuvi.Core.Logging;
using Tuvi.Core.Mail;
using Tuvi.Core.Utils;
using Tuvi.Core.Web.BackupService;

namespace Tuvi.Core.Impl
{
    static class ConcurrentDictionaryExtension
    {
        public static TValue AddOrReplace<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            return dictionary.AddOrUpdate(key, value, (k, v) => value);
        }
    }

    class FolderEqualityComparer : IEqualityComparer<string>
    {
        public bool Equals(string x, string y)
        {
            return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(string obj)
        {
            return obj.GetHashCode();
        }
    }

    public sealed class TuviMail : ITuviMail, IDisposable
    {
        private bool _isDisposed;
        private readonly IMailBoxFactory MailBoxFactory;
        private readonly IMailServerTester MailServerTester;
        private readonly IDataStorage DataStorage;
        private readonly ISecurityManager SecurityManager;
        private readonly IBackupManager BackupManager;
        private readonly IDecStorageClient DecStorageClient;
        public ICredentialsManager CredentialsManager { get; private set; }

        private ConcurrentDictionary<int, AccountGroup> AccountGroupCache = new ConcurrentDictionary<int, AccountGroup>();
        private ConcurrentDictionary<int, Account> AccountCache = new ConcurrentDictionary<int, Account>();
        private ConcurrentDictionary<EmailAddress, IAccountService> AccountServiceCache = new ConcurrentDictionary<EmailAddress, IAccountService>();
        private ConcurrentDictionary<EmailAddress, SchedulerWithTimer> AccountSchedulers = new ConcurrentDictionary<EmailAddress, SchedulerWithTimer>();
        private ConcurrentDictionary<Folder, IAccountService> FolderToAccountMapping = new ConcurrentDictionary<Folder, IAccountService>();

        public event EventHandler<MessagesReceivedEventArgs> MessagesReceived;
        public event EventHandler<UnreadMessagesReceivedEventArgs> UnreadMessagesReceived;
        public event EventHandler<MessageDeletedEventArgs> MessageDeleted;
        public event EventHandler<MessagesAttributeChangedEventArgs> MessagesIsReadChanged;
        public event EventHandler<MessagesAttributeChangedEventArgs> MessagesIsFlaggedChanged;
        public event EventHandler<AccountEventArgs> AccountAdded;
        public event EventHandler<AccountEventArgs> AccountUpdated;
        public event EventHandler<AccountEventArgs> AccountDeleted;
        public event EventHandler<ExceptionEventArgs> ExceptionOccurred;
        public event EventHandler<ContactAddedEventArgs> ContactAdded;
        public event EventHandler<ContactChangedEventArgs> ContactChanged;
        public event EventHandler<ContactDeletedEventArgs> ContactDeleted;
        public event EventHandler<EventArgs> WipeAllDataNeeded;

        public TuviMail(
            IMailBoxFactory mailBoxFactory,
            IMailServerTester mailServerTester,
            IDataStorage dataStorage,
            ISecurityManager securityManager,
            IBackupManager backupManager,
            ICredentialsManager credentialsManager,
            ImplementationDetailsProvider implementationDetailsProvider,
            IDecStorageClient decStorageClient)
        {
            if (mailBoxFactory is null)
            {
                throw new ArgumentNullException(nameof(mailBoxFactory));
            }
            if (mailServerTester is null)
            {
                throw new ArgumentNullException(nameof(mailServerTester));
            }
            if (dataStorage is null)
            {
                throw new ArgumentNullException(nameof(dataStorage));
            }
            if (securityManager is null)
            {
                throw new ArgumentNullException(nameof(securityManager));
            }
            if (backupManager is null)
            {
                throw new ArgumentNullException(nameof(backupManager));
            }
            if (credentialsManager is null)
            {
                throw new ArgumentNullException(nameof(credentialsManager));
            }
            if (decStorageClient is null)
            {
                throw new ArgumentNullException(nameof(decStorageClient));
            }

#if NET461_OR_GREATER || NETSTANDARD || NET5_0_OR_GREATER
            // Note: The CodePagesEncodingProvider was introduced in .NET Framework v4.6.1
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif

            MailBoxFactory = mailBoxFactory;
            MailServerTester = mailServerTester;
            DataStorage = dataStorage;
            SecurityManager = securityManager;
            BackupManager = backupManager;
            CredentialsManager = credentialsManager;
            DecStorageClient = decStorageClient;

            BackupManager.AccountRestoredAsync += RestoreAccountFromBackupAsync;
            BackupManager.MessagesRestoredAsync += RestoreMessagesFromBackupAsync;

            BackupManager.SetBackupDetails(implementationDetailsProvider);
            SecurityManager.SetKeyDerivationDetails(implementationDetailsProvider);

            DataStorage.ContactAdded += (sender, args) => ContactAdded?.Invoke(sender, args);
            DataStorage.ContactChanged += (sender, args) => ContactChanged?.Invoke(sender, args);
        }

        public ISecurityManager GetSecurityManager()
        {
            return SecurityManager;
        }

        public IBackupManager GetBackupManager()
        {
            return BackupManager;
        }

        public ITextUtils GetTextUtils()
        {
            return new TextUtils();
        }

        public async Task TestMailServerAsync(string serverAddress, int serverPort, MailProtocol protocol, ICredentialsProvider credentialsProvider, CancellationToken cancellationToken)
        {
            await MailServerTester.TestAsync(serverAddress,
                                             serverPort,
                                             protocol,
                                             credentialsProvider,
                                             cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> InitializeApplicationAsync(string password, CancellationToken cancellationToken)
        {
            try
            {
                await SecurityManager.StartAsync(password, cancellationToken).ConfigureAwait(false);
                await InitializeAndStartAllAccountSchedulersAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (DataBasePasswordException)
            {
                return false;
            }
        }

        private async Task InitializeAndStartAllAccountSchedulersAsync(CancellationToken cancellationToken)
        {
            try
            {
                AccountSchedulers.Clear();

                var accounts = await GetAccountsAsync(cancellationToken).ConfigureAwait(false);
                foreach (var account in accounts)
                {
                    AddAndStartAccountScheduler(account);
                }
            }
            catch (OperationCanceledException)
            {
                // Do nothing. Operation was cancelled.
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                this.Log().LogError(ex, "An error occurred while initializing and starting all account scheduler");
            }
        }

        private void AddAndStartAccountScheduler(Account account)
        {
            if (AccountSchedulers.ContainsKey(account.Email))
            {
                return;
            }

            SchedulerWithTimer scheduler = new SchedulerWithTimer(
                (schedulerCancellation) =>
                {
                    if (_isDisposed)
                    {
                        return Task.CompletedTask;
                    }
                    return CheckForNewMessagesAsync(account, schedulerCancellation);
                },
                TimeSpan.FromMinutes(account.SynchronizationInterval).TotalMilliseconds);
            scheduler.ExceptionOccurred += OnSchedulerExceptionOccurred;

            AccountSchedulers[account.Email] = scheduler;
            scheduler.Start();
        }

        private void OnSchedulerExceptionOccurred(object sender, ExceptionEventArgs args)
        {
            ExceptionOccurred?.Invoke(sender, args);
        }

        private async Task CheckForNewMessagesForceAsync(Account account, CancellationToken cancellationToken, bool silent = false)
        {
            if (AccountSchedulers.TryGetValue(account.Email, out SchedulerWithTimer scheduler))
            {
                await scheduler.ExecuteActionForceAsync().ConfigureAwait(false);
            }
        }

        private void CheckDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TuviMail));
            }
        }

        private async Task CheckForNewMessagesAsync(Account account, CancellationToken cancellationToken)
        {
            List<EmailFolderError> failedEmailFolders = new List<EmailFolderError>();

            try
            {
                CheckDisposed();
                var accountService = GetAccountService(account);
                await accountService.UpdateFolderStructureAsync(cancellationToken).ConfigureAwait(true);
                foreach (var folder in account.FoldersStructure)
                {
                    CheckDisposed();
                    await LoadNewMessagesAsync(folder, accountService, cancellationToken).ConfigureAwait(true);
                }
                if (!cancellationToken.IsCancellationRequested)
                {
                    CheckDisposed();
                    await accountService.SynchronizeAsync(full: false, cancellationToken).ConfigureAwait(true);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                RegisterException(ex, "An error occurred while checking for new messages");
            }
        }

        public async Task ResetApplicationAsync()
        {
            CheckDisposed();
            if (await SecurityManager.IsNeverStartedAsync().ConfigureAwait(true))
            {
                return;
            }
            // reset caches
            AccountGroupCache.Clear();
            AccountCache.Clear();
            AccountServiceCache.Clear();
            foreach (var s in AccountSchedulers)
            {
                s.Value.Cancel();
            }
            AccountSchedulers.Clear();
            FolderToAccountMapping.Clear();

            // TODO: TVM-181 clear application cache like mailboxes and mails
            await SecurityManager.ResetAsync().ConfigureAwait(true);

            WipeAllDataNeeded?.Invoke(this, null);
        }

        public Task<bool> IsFirstApplicationStartAsync(CancellationToken cancellationToken)
        {
            return SecurityManager.IsNeverStartedAsync(cancellationToken);
        }

        public async Task<bool> ChangeApplicationPasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            try
            {
                await SecurityManager.ChangePasswordAsync(currentPassword, newPassword, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (DataBasePasswordException)
            {
                return false;
            }
        }

        public Task<bool> ExistsAccountWithEmailAddressAsync(EmailAddress email, CancellationToken cancellationToken)
        {
            CheckDisposed();
            return DataStorage.ExistsAccountWithEmailAddressAsync(email, cancellationToken);
        }

        public Task<Account> GetAccountAsync(EmailAddress email, CancellationToken cancellationToken)
        {
            CheckDisposed();
            return DataStorage.GetAccountAsync(email, cancellationToken);
        }

        public Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken)
        {
            CheckDisposed();
            return DataStorage.GetAccountsAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<CompositeAccount>> GetCompositeAccountsAsync(CancellationToken cancellationToken)
        {
            CheckDisposed();
            await LoadAccountsAsync(cancellationToken).ConfigureAwait(false);

            // wrap accounts with CompositeAccount, but preserve previous behavior
            var accountGroups = AccountCache.Values.GroupBy(x => x.GroupId);
            var res = new List<CompositeAccount>();
            var comparer = new FolderEqualityComparer();
            foreach (var accountGroup in accountGroups)
            {
                var addresses = new List<EmailAddress>();
                var defaultInboxFolders = new List<Folder>();
                var mergedFolders = new Dictionary<string, List<Folder>>(comparer);
                foreach (var account in accountGroup)
                {
                    AccountGroup group = null;
                    if (accountGroup.Key > 0)
                    {
                        if (!AccountGroupCache.TryGetValue(accountGroup.Key, out group))
                        {
                            // TODO: Need to create groups after backup restoring
                            account.GroupId = 0; // HACK: Unknown group, reset group id to default.
                            //continue; // skip, unknown group
                        }
                    }
                    var accountService = GetAccountService(account);

                    addresses.Add(account.Email);

                    foreach (var folder in account.FoldersStructure)
                    {
                        FolderToAccountMapping.AddOrReplace(folder, accountService);
                        List<Folder> folders = null;
                        if (!mergedFolders.TryGetValue(folder.FullName, out folders))
                        {
                            folders = new List<Folder>();
                            mergedFolders.Add(folder.FullName, folders);
                        }
                        folders.Add(folder);

                        if (folder.Id == account.DefaultInboxFolderId)
                        {
                            defaultInboxFolders.Add(folder);
                        }
                    }
                    if (accountGroup.Key == 0)
                    {
                        AddCompositeFolder(addresses, mergedFolders.Values, defaultInboxFolders);
                        addresses.Clear();
                        mergedFolders.Clear();
                        defaultInboxFolders.Clear();
                    }
                }

                if (accountGroup.Key > 0)
                {
                    AddCompositeFolder(addresses, mergedFolders.Values, defaultInboxFolders);
                }
            }
            return res;

            void AddCompositeFolder(List<EmailAddress> addresses,
                                    IEnumerable<List<Folder>> folders,
                                    List<Folder> defaultInboxFolders)
            {
                var compositeFolders = folders.Select(x => new CompositeFolder(x, GetAccountService)).ToList();
                res.Add(new CompositeAccount(compositeFolders, addresses, new CompositeFolder(defaultInboxFolders, GetAccountService)));
            }
        }

        private async Task LoadAccountsAsync(CancellationToken cancellationToken)
        {
            var groups = await DataStorage.GetAccountGroupsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var group in groups)
            {
                AccountGroupCache.AddOrReplace(group.Id, group);
            }

            var accounts = await DataStorage.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var account in accounts)
            {
                AccountCache.AddOrReplace(account.Id, account);
            }
        }

        private async Task<CompositeFolder> GetAllAccountsInboxAsync(CancellationToken cancellationToken)
        {
            // wrap accounts with CompositeAccount, but preserve previous behavior
            var accounts = await DataStorage.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
            var folders = new List<Folder>();
            foreach (var account in accounts)
            {
                var accountService = GetAccountService(account);
                foreach (var folder in account.FoldersStructure.Where(x => x.IsInbox))
                {
                    folders.Add(folder);
                    FolderToAccountMapping.AddOrReplace(folder, accountService);
                }
            }
            return new CompositeFolder(folders, GetAccountService);
        }

        public async Task<IAccountService> GetAccountServiceAsync(EmailAddress email, CancellationToken cancellationToken)
        {
            CheckDisposed();
            var account = await GetAccountAsync(email, cancellationToken).ConfigureAwait(false);
            return GetAccountService(account);
        }

        private IAccountService GetAccountService(Account account)
        {
            if (!AccountServiceCache.TryGetValue(account.Email, out IAccountService accountService))
            {
                accountService = CreateAccountService(account);
                accountService = AccountServiceCache.GetOrAdd(account.Email, accountService);
            }
            return accountService;
        }

        private void RemoveAccountServiceByKey(EmailAddress key)
        {
            AccountServiceCache.TryRemove(key, out IAccountService _);
        }

        private IAccountService CreateAccountService(Account account)
        {
            IMessageProtector messageProtector = SecurityManager.GetMessageProtector();
            var mailBox = CreateMailBox(account);
            var accountService = new AccountService(account, DataStorage, mailBox, messageProtector);

            SubscribeOnServiceEvents(accountService);

            return accountService;
        }

        private void SubscribeOnServiceEvents(IAccountService accountService)
        {
            accountService.MessageDeleted += OnMessageDeleted;
            accountService.MessagesIsReadChanged += (sender, args) => MessagesIsReadChanged?.Invoke(sender, args);
            accountService.MessagesIsFlaggedChanged += (sender, args) => MessagesIsFlaggedChanged?.Invoke(sender, args);
            accountService.UnreadMessagesReceived += (sender, args) => UnreadMessagesReceived?.Invoke(sender, args);
        }

        private IMailBox CreateMailBox(Account account)
        {
            return MailBoxFactory.CreateMailBox(account);
        }

        public async Task AddAccountAsync(Account account, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            if (await ExistsAccountWithEmailAddressAsync(account.Email, cancellationToken).ConfigureAwait(false))
            {
                throw new AccountAlreadyExistInDatabaseException();
            }

            using (var mailBox = CreateMailBox(account))
            {
                await IncreaseDecentralizedAccountIndexIfNeededAsync(account, cancellationToken).ConfigureAwait(true);
                await AccountService.TryToAccountUpdateFolderStructureAsync(account, mailBox, cancellationToken).ConfigureAwait(false);
                await AddAccountToStorageAsync(account, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task IncreaseDecentralizedAccountIndexIfNeededAsync(Account account, CancellationToken cancellationToken)
        {
            if (account.Type == MailBoxType.Dec)
            {
                switch (account.Email.Network)
                {
                    case NetworkType.Eppie:
                        await IncreaseEppieAccountIndexAsync(account, cancellationToken).ConfigureAwait(false);
                        break;
                    case NetworkType.Bitcoin:
                        await IncreaseBitcoinAccountIndexAsync(account, cancellationToken).ConfigureAwait(false);
                        break;
                    case NetworkType.Ethereum:
                        await IncreaseEthereumAccountIndexAsync(account, cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported network type: {account.Email.Network}");
                }
            }
        }

        private async Task IncreaseEppieAccountIndexAsync(Account account, CancellationToken cancellationToken)
        {
            var settings = await DataStorage.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            var index = settings.EppieAccountCounter;

            if (index != account.DecentralizedAccountIndex)
            {
                throw new InvalidOperationException($"Decentralized account index mismatch. Expected: {account.DecentralizedAccountIndex}, Actual: {index}");
            }

            index++;
            settings.EppieAccountCounter = index;
            await DataStorage.SetSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        private async Task IncreaseBitcoinAccountIndexAsync(Account account, CancellationToken cancellationToken)
        {
            var settings = await DataStorage.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            var index = settings.BitcoinAccountCounter;

            if (index != account.DecentralizedAccountIndex)
            {
                throw new InvalidOperationException($"Decentralized account index mismatch. Expected: {account.DecentralizedAccountIndex}, Actual: {index}");
            }

            index++;
            settings.BitcoinAccountCounter = index;
            await DataStorage.SetSettingsAsync(settings, cancellationToken).ConfigureAwait(false);

        }

        private async Task IncreaseEthereumAccountIndexAsync(Account account, CancellationToken cancellationToken)
        {
            var settings = await DataStorage.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            var index = settings.EthereumAccountCounter;

            if (index != account.DecentralizedAccountIndex)
            {
                throw new InvalidOperationException($"Decentralized account index mismatch. Expected: {account.DecentralizedAccountIndex}, Actual: {index}");
            }
            index++;
            settings.EthereumAccountCounter = index;
            await DataStorage.SetSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        private async Task AddAccountToStorageAsync(Account account, CancellationToken cancellationToken = default)
        {
            await DataStorage.AddAccountAsync(account, cancellationToken).ConfigureAwait(false);
            await SecurityManager.CreateDefaultPgpKeysAsync(account).ConfigureAwait(false);

            AddAndStartAccountScheduler(account);
            AccountCache.AddOrReplace(account.Id, account);

            AccountAdded?.Invoke(this, new AccountEventArgs(account));
        }

        public async Task DeleteAccountAsync(Account account, CancellationToken cancellationToken)
        {
            CheckDisposed();
            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }
            foreach (var folder in account.FoldersStructure)
            {
                await DataStorage.DeleteFolderAsync(account.Email, folder.FullName, cancellationToken).ConfigureAwait(false);
            }

            SecurityManager.RemovePgpKeys(account);

            RemoveAccountServiceByKey(account.Email);
            RemoveAccountSchedulerByKey(account.Email);
            AccountCache.TryRemove(account.Id, out Account _);

            await DataStorage.DeleteAccountAsync(account, cancellationToken).ConfigureAwait(false);

            AccountDeleted?.Invoke(this, new AccountEventArgs(account));
        }

        private void RemoveAccountSchedulerByKey(EmailAddress key)
        {
            if (AccountSchedulers.TryRemove(key, out SchedulerWithTimer scheduler))
            {
                scheduler.Cancel();
                scheduler.ExceptionOccurred -= OnSchedulerExceptionOccurred;
                scheduler.Dispose();
            }
        }

        public async Task CheckForNewMessagesInFolderAsync(CompositeFolder folder, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            Debug.Assert(folder != null);

            try
            {
                var uniqueAccountEmails = folder.Folders
                    .Select(f => f.AccountEmail)
                    .Distinct();

                var tasks = uniqueAccountEmails.Select(async email =>
                {
                    var account = await GetAccountAsync(email, cancellationToken).ConfigureAwait(false);
                    await CheckForNewMessagesForceAsync(account, cancellationToken).ConfigureAwait(false);
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        private async Task LoadNewMessagesAsync(Folder folder, IAccountService accountService, CancellationToken cancellationToken)
        {
            var messages = await accountService.ReceiveNewMessagesInFolderAsync(folder, cancellationToken)
                                               .ConfigureAwait(true);
            var receivedMessages = new List<ReceivedMessageInfo>(messages.Select(m => new ReceivedMessageInfo(folder.AccountEmail, m)));

            if (receivedMessages.Count > 0)
            {
                MessagesReceived?.Invoke(this, new MessagesReceivedEventArgs(receivedMessages));
            }
        }

        public async Task CheckForNewInboxMessagesAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();

            var accounts = await GetAccountsAsync(cancellationToken).ConfigureAwait(true);
            var tasks = accounts.Select(x => CheckForNewMessagesForceAsync(x, cancellationToken)).ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(true);
        }

        private void OnMessageDeleted(object sender, MessageDeletedEventArgs args)
        {
            try
            {
                this.Log().LogDebug("OnMessageDeleted {Folder}, uid = {MessageID}", args.Folder.FullName, args.MessageID);
                MessageDeleted?.Invoke(sender, args);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                RegisterException(ex, "An error occurred while deleting the message");
            }
        }

        private void RegisterExceptions(IEnumerable<Exception> exceptions, string message)
        {
            foreach (var ex in exceptions)
            {
                RegisterException(ex, message);
            }
        }

        private void RegisterException(Exception ex, string message, bool silent = false)
        {
            if (ex is OperationCanceledException ||
                ex is ObjectDisposedException)
            {
                return;
            }

            this.Log().LogError(ex, "Error details: {Details}", message);

            if (!silent)
            {
                ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex));
            }
        }

        private async Task RestoreAccountFromBackupAsync(Account account)
        {
            var isAccountExist = await DataStorage
                        .ExistsAccountWithEmailAddressAsync(account.Email)
                        .ConfigureAwait(false);

            if (isAccountExist)
            {
                await UpdateAccountAsync(account).ConfigureAwait(false);
            }
            else
            {
                await AddAccountToStorageAsync(account).ConfigureAwait(false);
            }
        }

        public async Task CreateHybridAccountAsync(Account account, CancellationToken cancellationToken)
        {
            CheckDisposed();

            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            var email = account.Email;

            var deckey = await SecurityManager.GetEmailPublicKeyStringAsync(email, cancellationToken).ConfigureAwait(false);
            var hybridAddress = email.MakeHybrid(deckey);

            var decAccountService = GetAccountService(hybridAddress);
            if (decAccountService != null)
            {
                // we already have created hybrid account
                return;
            }

            if (account.GroupId == 0)
            {
                var group = new AccountGroup() { Name = email.Name };
                await DataStorage.AddAccountGroupAsync(group, cancellationToken).ConfigureAwait(false);
                AccountGroupCache.AddOrReplace(group.Id, group);
                account.GroupId = group.Id;
                await DataStorage.UpdateAccountAsync(account, cancellationToken).ConfigureAwait(false);
            }

            var decAccount = new Account()
            {
                Email = hybridAddress,
                IsBackupAccountSettingsEnabled = true,
                IsBackupAccountMessagesEnabled = true,
                GroupId = account.GroupId,
                Type = MailBoxType.Hybrid
            };

            await AddAccountAsync(decAccount, cancellationToken).ConfigureAwait(false);
        }

        public async Task UpdateAccountAsync(Account account, CancellationToken cancellationToken = default)
        {
            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            CheckDisposed();
            await DataStorage.UpdateAccountAuthAsync(account, cancellationToken).ConfigureAwait(false);

            // Update account in cache
            AccountCache.AddOrReplace(account.Id, account);
            AccountGroupCache.TryRemove(account.GroupId, out AccountGroup _);
            RemoveAccountServiceByKey(account.Email);

            UpdateSchedulerInterval(account);

            AccountUpdated?.Invoke(this, new AccountEventArgs(account));
        }

        private void UpdateSchedulerInterval(Account account)
        {
            if (account is null)
            {
                return;
            }
            var interval = TimeSpan.FromMinutes(account.SynchronizationInterval).TotalMilliseconds;
            if (AccountSchedulers.TryGetValue(account.Email, out SchedulerWithTimer scheduler))
            {
                scheduler.Interval = interval;
            }
            else
            {
                AddAndStartAccountScheduler(account);
            }
        }

        private async Task RestoreMessagesFromBackupAsync(EmailAddress email, IReadOnlyList<FolderMessagesBackupContainer> messages)
        {
            var isAccountExist = await DataStorage
                        .ExistsAccountWithEmailAddressAsync(email)
                        .ConfigureAwait(false);

            if (isAccountExist)
            {
                await AddMessagesAsync(email, messages).ConfigureAwait(false);
            }
        }

        private async Task AddMessagesAsync(EmailAddress email, IReadOnlyList<FolderMessagesBackupContainer> messagesHolder, CancellationToken cancellationToken = default)
        {
            var account = await GetAccountAsync(email, cancellationToken).ConfigureAwait(false);
            var accountService = GetAccountService(account);
            foreach (var messages in messagesHolder)
            {
                var folder = account.FoldersStructure.Where(x => x.HasSameName(messages.FolderFullName)).FirstOrDefault();
                if (folder is null)
                {
                    // TODO: discuss what should we do in this case
                    continue; // skip unknown folder
                }
                await accountService.AddMessagesToDataStorageAsync(folder, messages.Messages, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            foreach (var accountScheduler in AccountSchedulers)
            {
                accountScheduler.Value.Cancel();
            }
            var tasks = AccountSchedulers.Select(x => x.Value.GetActionTask()).ToList();
            Task.WhenAll(tasks).Wait();
            foreach (var accountScheduler in AccountSchedulers)
            {
                accountScheduler.Value.Dispose();
            }
            CredentialsManager.Dispose();
            DataStorage.Dispose();
            _isDisposed = true;
        }

        public Task<IEnumerable<Contact>> GetContactsAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            return DataStorage.GetContactsAsync(cancellationToken);
        }

        public async Task SetContactNameAsync(EmailAddress contactEmail, string newName, CancellationToken cancellationToken = default)
        {
            CheckDisposed();

            if (contactEmail is null)
            {
                throw new ArgumentNullException(nameof(contactEmail));
            }

            if (await DataStorage.ExistsContactWithEmailAddressAsync(contactEmail, cancellationToken).ConfigureAwait(false))
            {
                var contact = await DataStorage.GetContactAsync(contactEmail, cancellationToken).ConfigureAwait(false);

                contact.FullName = newName;
                contact.Email = new EmailAddress(contact.Email.Address, newName);
                await DataStorage.UpdateContactAsync(contact, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task SetContactAvatarAsync(EmailAddress contactEmail, byte[] avatarBytes, int avatarWidth, int avatarHeight, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (await DataStorage.ExistsContactWithEmailAddressAsync(contactEmail, cancellationToken).ConfigureAwait(false))
            {
                await DataStorage.SetContactAvatarAsync(contactEmail, avatarBytes, avatarWidth, avatarHeight, cancellationToken).ConfigureAwait(false);
                var contact = await DataStorage.GetContactAsync(contactEmail, cancellationToken).ConfigureAwait(false);
                ContactChanged?.Invoke(this, new ContactChangedEventArgs(contact));
            }
        }

        public async Task RemoveContactAsync(EmailAddress contactEmail, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (await DataStorage.ExistsContactWithEmailAddressAsync(contactEmail, cancellationToken).ConfigureAwait(false))
            {
                await DataStorage.RemoveContactAsync(contactEmail, cancellationToken).ConfigureAwait(false);
                ContactDeleted?.Invoke(this, new ContactDeletedEventArgs(contactEmail));
            }
        }

        public async Task<IReadOnlyList<Message>> GetContactEarlierMessagesAsync(EmailAddress contactEmail, int count, Message lastMessage, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            var allFolder = await GetAllAccountsInboxAsync(cancellationToken).ConfigureAwait(false);
            return await GetFolderEarlierMessagesAsync(allFolder, count, lastMessage, (c, l, ct) => { return DataStorage.GetEarlierContactMessagesAsync(contactEmail, c, l, ct); }, cancellationToken).ConfigureAwait(false);
        }

        int _receiveBatchSize = 100;

        public async Task<IReadOnlyList<Message>> GetAllEarlierMessagesAsync(int count, Message lastMessage, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            var allFolder = await GetAllAccountsInboxAsync(cancellationToken).ConfigureAwait(true);
            return await GetFolderEarlierMessagesAsync(allFolder, count, lastMessage, cancellationToken).ConfigureAwait(true);
        }

        public async Task<IReadOnlyList<Message>> GetFolderEarlierMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (folder is null)
            {
                throw new ArgumentNullException(nameof(folder));
            }
            // ensure that we cashed account service
            await GetAccountServiceAsync(folder.AccountEmail, cancellationToken).ConfigureAwait(false);
            var compositeFolder = new CompositeFolder(new List<Folder>() { folder }, GetAccountService);
            return await GetFolderEarlierMessagesAsync(compositeFolder, count, lastMessage, cancellationToken).ConfigureAwait(false);
        }

        public Task<IReadOnlyList<Message>> GetFolderEarlierMessagesAsync(CompositeFolder folder, int count, Message lastMessage, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (folder is null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            return GetFolderEarlierMessagesAsync(folder, count, lastMessage, (c, l, ct) => { return DataStorage.GetEarlierMessagesInFoldersAsync(folder.Folders, c, l, ct); }, cancellationToken);
        }

        delegate Task<IReadOnlyList<Message>> GetEarlierLocalMessages(int count, Message lastMessage, CancellationToken cancellationToken);

        private async Task<IReadOnlyList<Message>> GetFolderEarlierMessagesAsync(CompositeFolder folder, int count, Message lastMessage, GetEarlierLocalMessages func, CancellationToken cancellationToken)
        {
            var messages = new List<Message>(count);
            var storedMessages = await func(count, lastMessage, cancellationToken).ConfigureAwait(true);
            messages.AddRange(storedMessages);

            var storedMessagesCount = storedMessages.Count;
            if (storedMessagesCount == count)
            {
                return messages;
            }
            try
            {
                var loadedItems = await folder.ReceiveEarlierMessagesAsync(_receiveBatchSize, cancellationToken).ConfigureAwait(false);
                RegisterExceptions(folder.Exceptions, "An error occurred while retrieving earlier messages");
                var lastStoredMessage = storedMessagesCount == 0 ? lastMessage : storedMessages[storedMessagesCount - 1];
                messages.AddRange(await func(count - messages.Count, lastStoredMessage, cancellationToken).ConfigureAwait(true));

                if (loadedItems.Count > 0 && messages.Count == 0)
                {
                    // HACK this will allow us to detect that there are more items to load
                    // this is the most suitable and easy way to let know that there are no more items, without breaking tests and changing a bunch of interfaces
                    messages.Add(null);
                }
            }
            catch (ObjectDisposedException)
            {
                //ignore
            }
            catch (OperationCanceledException)
            {
                //ignore
            }

            return messages;
        }

        public async Task<int> GetUnreadCountForAllAccountsInboxAsync(CancellationToken cancellationToken)
        {
            CheckDisposed();
            var unreadCount = 0;

            var accounts = await GetCompositeAccountsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var account in accounts)
            {
                unreadCount += await account.DefaultInboxFolder.GetUnreadMessagesCountAsync(cancellationToken).ConfigureAwait(false);
            }

            Debug.Assert(unreadCount >= 0);
            return unreadCount;
        }

        public Task<IReadOnlyDictionary<EmailAddress, int>> GetUnreadMessagesCountByContactAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            return DataStorage.GetUnreadMessagesCountByContactAsync(cancellationToken);
        }

        public async Task DeleteMessagesAsync(IReadOnlyList<Message> messages, CancellationToken cancellationToken)
        {
            CheckDisposed();
            // group messages by Folder
            var messageByFolder = messages.GroupBy(x => x.FolderId);

            foreach (var group in messageByFolder)
            {
                var message = group.First();
                var folder = message.Folder;
                var accountService = await GetAccountServiceAsync(folder.AccountEmail, cancellationToken).ConfigureAwait(true);
                await accountService.DeleteMessagesAsync(folder, group.ToList(), cancellationToken).ConfigureAwait(true);
            }
        }

        public async Task RestoreFromBackupIfNeededAsync(Uri downloadUri)
        {
            CheckDisposed();
            var accounts = await GetAccountsAsync(default).ConfigureAwait(true);
            var isSeedInitialized = await GetSecurityManager().IsSeedPhraseInitializedAsync().ConfigureAwait(true);
            if (accounts.Count == 0 && isSeedInitialized)
            {
                var backupFileName = GetBackupManager().GetBackupKeyFingerprint() + DataIdentificators.BackupExtension;

                using (var backup = await BackupServiceClient.DownloadAsync(downloadUri, backupFileName).ConfigureAwait(true))
                {
                    if (backup != null)
                    {
                        await GetBackupManager().RestoreBackupAsync(backup).ConfigureAwait(true);
                    }
                    else
                    {
                        // TODO: TVM-278
                        // we need to figure out what to do in case of failure to obtain or restore data from a backup.
                    }
                }
            }
        }
        public Task MarkMessagesAsReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            return ApplyMessageCommandAsync(messages, (accountService, m, ct) =>
            {
                return accountService.MarkMessagesAsReadAsync(m, ct);
            }, cancellationToken);
        }

        public Task MarkMessagesAsUnReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            return ApplyMessageCommandAsync(messages, (accountService, m, ct) =>
            {
                return accountService.MarkMessagesAsUnReadAsync(m, ct);
            }, cancellationToken);
        }

        public Task FlagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            return ApplyMessageCommandAsync(messages, (accountService, m, ct) =>
            {
                return accountService.FlagMessagesAsync(m, ct);
            }, cancellationToken);
        }

        public Task UnflagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            return ApplyMessageCommandAsync(messages, (accountService, m, ct) =>
            {
                return accountService.UnflagMessagesAsync(m, ct);
            }, cancellationToken);
        }

        public Task<Message> GetMessageBodyHighPriorityAsync(Message message, CancellationToken cancellationToken)
        {
            CheckDisposed();
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            var accountService = GetAccountService(message.Folder);
            return accountService.GetMessageBodyHighPriorityAsync(message, cancellationToken);
        }

        public Task<Message> GetMessageBodyAsync(Message message, CancellationToken cancellationToken)
        {
            CheckDisposed();
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            var accountService = GetAccountService(message.Folder);
            return accountService.GetMessageBodyAsync(message, cancellationToken);
        }

        public async Task SendMessageAsync(Message message, bool encrypt, bool sign, CancellationToken cancellationToken)
        {
            CheckDisposed();
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            if (message.From.Count == 0)
            {
                throw new ArgumentException($"{nameof(message.From)} property should contain at least on address", nameof(message));
            }

            var subAddresses = SplitAddress(message);
            var tasks = subAddresses.Select(x => GetAccountService(x).SendMessageAsync(message, encrypt, sign, cancellationToken));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private static IReadOnlyList<EmailAddress> SplitAddress(Message message)
        {
            var address = message.From.First();
            var res = new List<EmailAddress>();

            if (address.IsHybrid)
            {
                var decentralized = message.AllRecipients.Where(x => x.IsDecentralized).Any();
                if (decentralized)
                {
                    // if we have decentralized email recipients
                    res.Add(address);
                }

                var traditional = message.AllRecipients.Where(x => !x.IsDecentralized).Any();
                if (traditional)
                {
                    // if we have traditional email recipients
                    res.Add(address.OriginalAddress);
                }
            }
            else
            {
                res.Add(address);
            }

            return res;
        }

        public async Task<Message> CreateDraftMessageAsync(Message message, CancellationToken cancellationToken)
        {
            CheckDisposed();
            Debug.Assert(message != null);
            Debug.Assert(message.From.Count > 0);
            var accountService = await GetAccountServiceAsync(message.From.First(), cancellationToken).ConfigureAwait(false);
            return await accountService.CreateDraftMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Message> UpdateDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken)
        {
            CheckDisposed();
            Debug.Assert(message != null);
            Debug.Assert(message.From.Count > 0);
            var accountService = await GetAccountServiceAsync(message.From.First(), cancellationToken).ConfigureAwait(false);
            return await accountService.UpdateDraftMessageAsync(id, message, cancellationToken).ConfigureAwait(false);
        }

        delegate Task MessageCommandAsync(IAccountService accountService, IEnumerable<Message> messages, CancellationToken cancellationToken);
        private async Task ApplyMessageCommandAsync(IEnumerable<Message> messages, MessageCommandAsync command, CancellationToken cancellationToken)
        {
            CheckDisposed();
            foreach (var groupMessages in messages.GroupBy(message => GetAccountService(message.Folder)))
            {
                if (groupMessages.Key is null)
                {
                    // folders were deleted, skip them
                    continue;
                }
                var accountService = groupMessages.Key;
                await command(accountService, groupMessages, cancellationToken).ConfigureAwait(true);
            }
        }

        private IAccountService GetAccountService(Folder folder)
        {
            IAccountService accountService;
            if (FolderToAccountMapping.TryGetValue(folder, out accountService))
            {
                return accountService;
            }
            accountService = GetAccountService(folder.AccountEmail);
            FolderToAccountMapping.AddOrReplace(folder, accountService);
            return accountService;
        }
        private IAccountService GetAccountService(EmailAddress email)
        {
            if (AccountServiceCache.TryGetValue(email, out var accountService))
            {
                return accountService;
            }
            Debug.Assert(email != null);
            return null;
        }

        public async Task MoveMessagesAsync(IReadOnlyList<Message> messages, CompositeFolder targetFolder, CancellationToken cancellationToken)
        {
            CheckDisposed();
            // group messages by Folder
            var messageByFolder = messages.GroupBy(x => x.FolderId);

            foreach (var group in messageByFolder)
            {
                var message = group.First();
                var folder = message.Folder;
                var accountService = await GetAccountServiceAsync(folder.AccountEmail, cancellationToken).ConfigureAwait(true);

                var target = targetFolder?.Folders.FirstOrDefault(x => x.AccountEmail == folder.AccountEmail);
                await accountService.MoveMessagesAsync(folder, target, group.ToList(), cancellationToken).ConfigureAwait(true);
            }
        }

        public IAIAgentsStorage GetAIAgentsStorage()
        {
            return DataStorage;
        }

        public Task UpdateMessageProcessingResultAsync(Message message, string result, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            return DataStorage.UpdateMessageProcessingResultAsync(message, result, cancellationToken);
        }

        public async Task<string> ClaimDecentralizedNameAsync(string name, Account account, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name is empty", nameof(name));
            }

            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            var address = account.Email;
            if (address.Network != NetworkType.Eppie)
            {
                throw new NotSupportedException($"Unsupported network type: {address.Network}");
            }

            var canonicalName = NameClaim.CanonicalizeName(name);
            var publicKey = await GetSecurityManager().GetEmailPublicKeyStringAsync(address, cancellationToken).ConfigureAwait(false);
            var signature = await GetSecurityManager().SignNameClaimAsync(canonicalName, account, cancellationToken).ConfigureAwait(false);
            var response = await DecStorageClient.ClaimNameAsync(canonicalName, publicKey, signature, cancellationToken).ConfigureAwait(false);

            if (response.Equals(publicKey, StringComparison.OrdinalIgnoreCase))
            {
                return canonicalName;
            }

            return string.Empty;
        }
    }
}
