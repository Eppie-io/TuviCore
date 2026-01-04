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

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core.Backup
{
    public interface IBackupProtector : IBackupDataLocker, IBackupDataUnlocker
    {
        /// <summary>
        /// Mandatory to be set before class instance usage.
        /// </summary>
        /// <param name="backupPgpKeyIdentity">Identity (dummy email) of PGP key used for data protection.</param>
        /// <exception cref="ArgumentNullException"/>
        void SetPgpKeyIdentity(string backupPgpKeyIdentity);

        /// <summary>
        /// Return backup public key fingerprint.
        /// </summary>
        /// <returns>Backup public key fingerprint.</returns>
        string GetBackupKeyFingerprint();
    }

    public interface IBackupDataLocker : IDataProtectionFormatProvider
    {
        /// <summary>
        /// Protect data. For example: encrypt and sign.
        /// </summary>
        /// <param name="dataToProtect">Stream with data to be protected.</param>
        /// <param name="protectedData">Output protected data stream.</param>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="BackupDataProtectionException"/>
        Task LockDataAsync(Stream dataToProtect, Stream protectedData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sign data and create detached signature data.
        /// </summary>
        /// <param name="dataToSign">Stream with data to be signed.</param>
        /// <param name="detachedSignatureData">Output detached signature data stream.</param>
        /// <param name="publicKeyData">Output public key data stream.</param>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="BackupDataProtectionException"/>
        Task CreateDetachedSignatureDataAsync(Stream dataToSign, Stream detachedSignatureData, Stream publicKeyData, CancellationToken cancellationToken = default);
    }

    public interface IBackupDataUnlocker : IDataProtectionFormatProvider
    {
        /// <summary>
        /// Unprotect data. For example: decrypt and validate signature.
        /// </summary>
        /// <param name="protectedData">Stream with locked (protected) data.</param>
        /// <param name="unprotectedData">Stream unlocked data will be put on.</param>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="BackupDataProtectionException"/>
        Task UnlockDataAsync(Stream protectedData, Stream unprotectedData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verify detached signature of data.
        /// </summary>
        /// <param name="data">Stream with signed data.</param>
        /// <param name="detachedSignatureData">Stream with detached signature data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        Task<bool> VerifySignatureAsync(Stream data, Stream detachedSignatureData, CancellationToken cancellationToken = default);
    }

    public interface IDataProtectionFormatProvider
    {
        /// <returns>Data protection format this class implements.</returns>
        DataProtectionFormat GetSupportedDataProtectionFormat();
    }
}
