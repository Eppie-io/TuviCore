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

using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Org.BouncyCastle.Bcpg.OpenPgp;
using SecurityManagementTests;
using Tuvi.Core.Backup.Impl;
using TuviPgpLibImpl;

namespace BackupTests
{
    public class CreateAndVerifyBackupTests : BaseBackupTest
    {
        [OneTimeSetUp]
        protected void InitializeContext()
        {
            Initialize();
        }

        [Test]
        [Category("Backup")]
        public async Task CreateAndVerifyBackupAsync()
        {

            // Create
            using (var publicKeyStream = new MemoryStream())
            using (var deatachedSignatureData = new MemoryStream())
            using (var backup = await BuildBackupAsync().ConfigureAwait(true))
            {
                await BackupDataProtector.CreateDetachedSignatureDataAsync(backup, deatachedSignatureData, publicKeyStream).ConfigureAwait(false);

                backup.Position = 0;
                publicKeyStream.Position = 0;
                deatachedSignatureData.Position = 0;

                // Verify
                var verificationKeyStorage = new MockPgpKeyStorage().Get();

                using (var verificationContext = new TuviPgpContext(verificationKeyStorage))
                {
                    await verificationContext.LoadContextAsync().ConfigureAwait(true);

                    var ring = new PgpPublicKeyRing(publicKeyStream);
                    var bundle = new PgpPublicKeyRingBundle(new PgpObject[] { ring });
                    verificationContext.Import(bundle);

                    var backupDataSignatureVerifier = BackupProtectorCreator.CreateBackupProtector(verificationContext);
                    backupDataSignatureVerifier.SetPgpKeyIdentity(TestData.BackupPgpKeyIdentity);

                    var signed = await backupDataSignatureVerifier.VerifySignatureAsync(backup, deatachedSignatureData).ConfigureAwait(false);
                    Assert.That(signed, Is.True);
                }

                backup.Position = 0;
                await ParseBackupAsync(backup).ConfigureAwait(true);
            }
        }
    }
}
