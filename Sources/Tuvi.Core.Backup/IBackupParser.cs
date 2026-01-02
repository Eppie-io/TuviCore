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
