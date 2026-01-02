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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

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
