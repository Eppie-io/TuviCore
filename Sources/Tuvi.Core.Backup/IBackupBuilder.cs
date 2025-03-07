using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Backup
{
    public interface IBackupBuilder
    {
        /// <summary>
        /// Set backup protocol version
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="BackupSerializationException"/>
        Task SetVersionAsync(BackupProtocolVersion version, CancellationToken cancellationToken = default);

        /// <summary>
        /// Forward accounts for backup
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="BackupSerializationException"/>
        Task SetAccountsAsync(IEnumerable<Account> accounts, CancellationToken cancellationToken = default);

        /// <summary>
        /// Forward messages for backup
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="BackupSerializationException"/>
        Task SetMessagesAsync(IReadOnlyList<EmailAccountBackupContainer> messages, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create backup package with previously set content.
        /// </summary>
        /// <param name="backup">Backup package data will be written to this stream.</param>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="BackupBuildingException"/>
        Task BuildBackupAsync(Stream backup, CancellationToken cancellationToken = default);

        /// <summary>
        /// Forward imported public keys for backup
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="BackupSerializationException"/>
        Task SetImportedPublicKeysAsync(IEnumerable<byte[]> keys, CancellationToken cancellationToken = default);

        /// <summary>
        /// Forward settings for backup
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="BackupSerializationException"/>
        Task SetSettingsAsync(Settings settings, CancellationToken cancellationToken = default);
    }
}
