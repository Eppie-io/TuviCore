using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Backup
{
    public interface IBackupParser
    {
        /// <summary>
        /// Parse backup data. After this possible to get specific content from backup.
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="BackupParsingException"/>
        Task ParseBackupAsync(Stream backupData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get backup protocol version it was created on.
        /// </summary>
        /// <exception cref="BackupDeserializationException"/>
        Task<BackupProtocolVersion> GetVersionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get accounts from backup.
        /// </summary>
        /// <exception cref="BackupDeserializationException"/>
        Task<IList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get messages from backup.
        /// </summary>
        /// <exception cref="BackupDeserializationException"/>
        Task<IReadOnlyList<EmailAccountBackupContainer>> GetMessagesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get imported public keys from backup.
        /// </summary>
        /// <exception cref="BackupDeserializationException"/>
        Task<IList<byte[]>> GetImportedPublicKeysAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get settings from backup.
        /// </summary>
        /// <exception cref="BackupDeserializationException"/>
        Task<Settings> GetSettingsAsync(CancellationToken cancellationToken = default);
    }
}
