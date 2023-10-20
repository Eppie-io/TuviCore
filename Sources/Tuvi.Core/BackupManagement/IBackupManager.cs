using Tuvi.Core.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core
{
    public interface IBackupManager
    {
        /// <summary>
        /// Set backup details provider. Has to be setup before any backup operations.
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        void SetBackupDetails(IBackupDetailsProvider detailsProvider);

        /// <summary>
        /// Create backup to stream.
        /// </summary>
        /// <param name="outputStream">For backup data output.</param>
        /// <exception cref="InvalidOperationException"/>
        Task CreateBackupAsync(Stream outputStream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restore data from stream.
        /// </summary>
        /// <param name="inputStream">Backup data input.</param>
        /// <exception cref="InvalidOperationException"/>
        Task RestoreBackupAsync(Stream inputStream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Return backup public key fingerprint.
        /// </summary>
        /// <returns>Backup public key fingerprint.</returns>
        string GetBackupKeyFingerprint();

        /// <summary>
        /// Sign data and create deatached signature data.
        /// </summary>
        /// <param name="dataToSign">Stream with data to be signed.</param>
        /// <param name="deatachedSignatureData">Output deatached signature data stream.</param>
        /// <param name="publicKeyData">Output public key data stream.</param>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="BackupDataProtectionException"/>
        Task CreateDetachedSignatureDataAsync(Stream dataToSign, Stream deatachedSignatureData, Stream publicKeyData, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Raised when account restored from backup.
        /// </summary>
#pragma warning disable CA1003
        event Func<Account, Task> AccountRestoredAsync;
        event Func<EmailAddress, IReadOnlyList<FolderMessagesBackupContainer>, Task> MessagesRestoredAsync;
#pragma warning restore CA1003
    }
}
