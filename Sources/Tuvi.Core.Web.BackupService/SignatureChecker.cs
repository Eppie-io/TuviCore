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
using System.Threading.Tasks;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Tuvi.Core.Backup.Impl;
using TuviPgpLibImpl;

namespace Tuvi.Core.Web.BackupService
{
    public static class SignatureChecker
    {
        public static async Task<bool> IsValidSignatureAsync(Stream publicKeyStream, Stream signatureStream, Stream backupStream, string backupPgpKeyIdentity)
        {
            if (publicKeyStream is null)
            {
                throw new ArgumentNullException(nameof(publicKeyStream));
            }

            if (signatureStream is null)
            {
                throw new ArgumentNullException(nameof(signatureStream));
            }

            if (backupStream is null)
            {
                throw new ArgumentNullException(nameof(backupStream));
            }

            publicKeyStream.Position = 0;
            signatureStream.Position = 0;
            backupStream.Position = 0;

            using (var verificationContext = new TuviPgpContext(new PgpKeyStorage()))
            {
                await verificationContext.LoadContextAsync().ConfigureAwait(false);

                var ring = new PgpPublicKeyRing(publicKeyStream);
                var bundle = new PgpPublicKeyRingBundle(new PgpObject[] { ring });
                verificationContext.Import(bundle);

                var backupDataSignatureVerifier = BackupProtectorCreator.CreateBackupProtector(verificationContext);
                backupDataSignatureVerifier.SetPgpKeyIdentity(backupPgpKeyIdentity);

                return await backupDataSignatureVerifier.VerifySignatureAsync(backupStream, signatureStream).ConfigureAwait(false);
            }
        }
    }
}
