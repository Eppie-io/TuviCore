using Tuvi.Core.Backup;
using Tuvi.Core.Entities;
using Tuvi.Core.Entities.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core.Backup.Impl
{
    internal class JsonUtf8BackupBuilder : ProtectedPackageBuilderBase, IBackupBuilder
    {
        private readonly string PackageIdentifier;
        private readonly ContentSerializationFormat SerializationFormat = ContentSerializationFormat.JsonUtf8;
        protected BackupSectionsDictionary BackupSections = new BackupSectionsDictionary();

        public JsonUtf8BackupBuilder(string packageIdentifier, IBackupProtector dataLocker)
            : base(dataLocker)
        {
            if (packageIdentifier is null)
            {
                throw new ArgumentNullException(nameof(packageIdentifier));
            }

            PackageIdentifier = packageIdentifier;
        }

        public async Task BuildBackupAsync(Stream backupPackage, CancellationToken cancellationToken)
        {
            if (backupPackage is null)
            {
                throw new ArgumentNullException(nameof(backupPackage));
            }

            await base.BuildPackageAsync(backupPackage, cancellationToken).ConfigureAwait(false);
        }

        public Task SetAccountsAsync(IEnumerable<Account> accounts, CancellationToken cancellationToken)
        {
            if (accounts is null)
            {
                throw new ArgumentNullException(nameof(accounts));
            }

            return SetBackupSectionAsync(BackupSectionType.Accounts, accounts, cancellationToken);
        }

        public Task SetImportedPublicKeysAsync(IEnumerable<byte[]> keys, CancellationToken cancellationToken)
        {
            if (keys is null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            return SetBackupSectionAsync(BackupSectionType.PublicKeys, keys, cancellationToken);
        }

        public Task SetSettingsAsync(Settings settings, CancellationToken cancellationToken)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return SetBackupSectionAsync(BackupSectionType.Settings, settings, cancellationToken);
        }

        public Task SetMessagesAsync(IReadOnlyList<EmailAccountBackupContainer> messages, CancellationToken cancellationToken)
        {
            if (messages is null)
            {
                throw new ArgumentNullException(nameof(messages));
            }

            return SetBackupSectionAsync(BackupSectionType.Messages, messages, cancellationToken);
        }

        public Task SetVersionAsync(BackupProtocolVersion appCoreVersion, CancellationToken cancellationToken)
        {
            if (appCoreVersion is null)
            {
                throw new ArgumentNullException(nameof(appCoreVersion));
            }

            return SetBackupSectionAsync(BackupSectionType.Version, appCoreVersion, cancellationToken);
        }

        private async Task SetBackupSectionAsync<TObject>(BackupSectionType sectionType, TObject value, CancellationToken cancellationToken)
        {
            try
            {
                using (var jsonUtf8Stream = new MemoryStream())
                {
                    await JsonSerializer.SerializeAsync(jsonUtf8Stream, value, GetDefaultOptions(), cancellationToken: cancellationToken).ConfigureAwait(false);
                    BackupSections.Add(sectionType, jsonUtf8Stream.ToArray());
                }
            }
            catch (Exception exception)
            {
                throw new BackupSerializationException($"Object serialization to section '{sectionType}' error.", exception);
            }
        }

        protected sealed override string GetPackageIdentifier()
        {
            return PackageIdentifier;
        }

        protected sealed override async Task BuildProtectedPackageContentAsync(Stream protectedPackageContent, CancellationToken cancellationToken)
        {
            await BuildBackupBinaryHeaderAsync(protectedPackageContent, cancellationToken).ConfigureAwait(false);
            await BuildBackupJsonSerializedBodyAsync(protectedPackageContent, cancellationToken).ConfigureAwait(false);
        }

        private Task BuildBackupBinaryHeaderAsync(Stream backupData, CancellationToken cancellationToken)
        {
            var binaryHeader = Convert.ToInt32(SerializationFormat, CultureInfo.InvariantCulture).ToByteBuffer();
            return backupData.WriteAsync(binaryHeader, 0, binaryHeader.Length, cancellationToken);
        }

        private async Task BuildBackupJsonSerializedBodyAsync(Stream backupData, CancellationToken cancellationToken)
        {
            try
            {
                await JsonSerializer.SerializeAsync(backupData, BackupSections, GetDefaultOptions(), cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw new BackupSerializationException("Error in backup sections serialization.", exception);
            }
        }

        private static JsonSerializerOptions GetDefaultOptions()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonUtf8.Converters.JsonAuthenticationDataConverter());

            return options;
        }
    }
}
