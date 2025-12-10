using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Backup;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using TuviPgpLib.Entities;
using Tuvi.Core.Entities.Exceptions;

namespace Tuvi.Core.Impl.BackupManagement
{
    public static class BackupManagerCreator
    {
        public static IBackupManager GetBackupManager(IDataStorage storage, IBackupProtector backupProtector, IBackupSerializationFactory backupFactory, ISecurityManager security)
        {
            return new BackupManager(storage, backupProtector, backupFactory, security);
        }
    }

    /// <summary>
    /// Handles backup operations.
    /// </summary>
    internal class BackupManager : IBackupManager
    {
        public event Func<Account, Task> AccountRestoredAsync;
        public event Func<EmailAddress, IReadOnlyList<FolderMessagesBackupContainer>, Task> MessagesRestoredAsync;

        private readonly IDataStorage DataStorage;
        private readonly ISecurityManager SecurityManager;
        private readonly IBackupProtector BackupProtector;
        private readonly BackupProtocolVersion BackupVersion = new BackupProtocolVersion
        {
            Major = 1,
            Minor = 0,
            Build = 0,
            Revision = 0
        };
        private IBackupSerializationFactory BackupFactory;
        private string BackupPackagesIdentifier;

        /// <param name="storage">Used to get data for backup.</param>
        /// <param name="backupProtector">Handles backup protection.</param>
        public BackupManager(IDataStorage storage, IBackupProtector backupProtector, IBackupSerializationFactory backupFactory, ISecurityManager security)
        {
            if (storage is null)
            {
                throw new ArgumentNullException(nameof(storage));
            }
            if (backupProtector is null)
            {
                throw new ArgumentNullException(nameof(backupProtector));
            }

            DataStorage = storage;
            BackupProtector = backupProtector;
            BackupFactory = backupFactory;
            SecurityManager = security;
        }

        public void SetBackupDetails(IBackupDetailsProvider backupDetails)
        {
            if (backupDetails is null)
            {
                throw new ArgumentNullException(nameof(backupDetails));
            }

            BackupPackagesIdentifier = backupDetails.GetPackageIdentifier();
            SetupBackupFactory();
        }

        public async Task CreateBackupAsync(Stream outputStream, CancellationToken cancellationToken)
        {
            if (BackupFactory is null)
            {
                throw new InvalidOperationException($"{nameof(BackupFactory)} is not set.");
            }

            var backup = BackupFactory.CreateBackupBuilder();

            var accounts = await DataStorage.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
            var accountsToBackup = accounts.Where(a => a.IsBackupAccountSettingsEnabled);

            await backup.SetVersionAsync(BackupVersion, cancellationToken).ConfigureAwait(false);
            await backup.SetAccountsAsync(accountsToBackup, cancellationToken).ConfigureAwait(false);

            var messagesHolder = new List<EmailAccountBackupContainer>();
            foreach (var account in accountsToBackup.Where(a => a.IsBackupAccountMessagesEnabled))
            {
                var folderMessages = new List<FolderMessagesBackupContainer>();
                foreach (var folder in account.FoldersStructure)
                {
                    var messages = await DataStorage.GetMessageListAsync(account.Email, folder.FullName, 0, cancellationToken).ConfigureAwait(false);
                    folderMessages.Add(new FolderMessagesBackupContainer(folder.FullName, messages));
                }
                messagesHolder.Add(new EmailAccountBackupContainer(account.Email.Address, folderMessages));
            }
            await backup.SetMessagesAsync(messagesHolder, cancellationToken).ConfigureAwait(false);

            var importedPublicKeysInfo = SecurityManager.GetPublicPgpKeysInfo()
                .Where(x => accounts.Find(y => StringHelper.AreEmailsEqual(x.UserIdentity, y.Email.Address)) == null);

            var importedKeys = new List<byte[]>();
            foreach (var key in importedPublicKeysInfo)
            {
                using (var stream = new MemoryStream())
                {
                    await SecurityManager.ExportPgpKeyRingAsync(key.KeyId, stream, cancellationToken).ConfigureAwait(false);
                    importedKeys.Add(stream.ToArray());
                }
            }

            await backup.SetImportedPublicKeysAsync(importedKeys, cancellationToken).ConfigureAwait(false);

            var settings = await DataStorage.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            await backup.SetSettingsAsync(settings, cancellationToken).ConfigureAwait(false);

            await backup.BuildBackupAsync(outputStream, cancellationToken).ConfigureAwait(false);
        }

        public async Task RestoreBackupAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            if (BackupFactory is null)
            {
                throw new InvalidOperationException($"{nameof(BackupFactory)} is not set.");
            }

            var backup = BackupFactory.CreateBackupParser();

            await backup.ParseBackupAsync(inputStream, cancellationToken).ConfigureAwait(false);
            var version = await backup.GetVersionAsync(cancellationToken).ConfigureAwait(false);

            if (version == BackupVersion)
            {
                var backupAccounts = await backup.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
                await RestoreAccounts(backupAccounts).ConfigureAwait(false);

                var backupMessages = await backup.GetMessagesAsync(cancellationToken).ConfigureAwait(false);
                foreach (var messagesHolder in backupMessages)
                {
                    await RestoreMessages(messagesHolder.EmailAccount, messagesHolder.Folders).ConfigureAwait(false);
                }

                var importedPublicKeys = await backup.GetImportedPublicKeysAsync(cancellationToken).ConfigureAwait(false);

                foreach (var key in importedPublicKeys)
                {
                    try
                    {
                        SecurityManager.ImportPublicPgpKey(key);
                    }
                    catch (PublicKeyAlreadyExistException)
                    {
                        // TVM-511: we can skip public keys which already exist
                    }
                }

                var settings = await backup.GetSettingsAsync(cancellationToken).ConfigureAwait(false);

                await DataStorage.SetSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new BackupVersionMismatchException($"Expected backup version {BackupVersion}, but got {version}.");
            }
        }

        public string GetBackupKeyFingerprint()
        {
            return BackupProtector.GetBackupKeyFingerprint();
        }

        public Task CreateDetachedSignatureDataAsync(Stream dataToSign, Stream detachedSignatureData, Stream publicKeyData, CancellationToken cancellationToken)
        {
            return BackupProtector.CreateDetachedSignatureDataAsync(dataToSign, detachedSignatureData, publicKeyData, cancellationToken);
        }

        private void SetupBackupFactory()
        {
            BackupFactory.SetPackageIdentifier(BackupPackagesIdentifier);
        }

        private async Task RestoreAccounts(IEnumerable<Account> accountsFromBackup)
        {
            foreach (var backupAccount in accountsFromBackup)
            {
                await (AccountRestoredAsync?.Invoke(backupAccount)).ConfigureAwait(false);
            }
        }

        private async Task RestoreMessages(string email, IReadOnlyList<FolderMessagesBackupContainer> messages)
        {
            await (MessagesRestoredAsync?.Invoke(new EmailAddress(email), messages)).ConfigureAwait(false);
        }
    }
}
