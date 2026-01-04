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
using BackupServiceClientLibrary;
using NUnit.Framework;
using Org.BouncyCastle.Bcpg.OpenPgp;
using SecurityManagementTests;
using Tuvi.Core.Backup.Impl;
using Tuvi.Core.Web.BackupService;
using TuviPgpLibImpl;

namespace BackupTests
{
    [Explicit("Integration tests are disabled for ci.")]
    public class UploadAndDownloadBackupTests : BaseBackupTest
    {
        const string UploadUrl = "https://testnet.eppie.io/api/UploadBackupFunction?code=1";
        const string DownloadUrl = "https://testnet.eppie.io/api/DownloadBackupFunction?code=1&name=";

        //const string UploadUrl = "http://localhost:7071/api/UploadBackupFunction";
        //const string DownloadUrl = "http://localhost:7071/api/DownloadBackupFunction?name=";

        [OneTimeSetUp]
        protected void InitializeContext()
        {
            Initialize();
        }

        [Test]
        [Category("Upload")]
        [Category("Download")]
        [Category("Backup")]
        public async Task UploadAndDownloadBackupFileAsync()
        {
            var publickeyName = Fingerprint + DataIdentificators.PublicKeyExtension;
            var signatureName = Fingerprint + DataIdentificators.SignatureExtension;
            var backupName = Fingerprint + DataIdentificators.BackupExtension;

            {// Upload

                using (var publicKeyStream = new MemoryStream())
                using (var deatachedSignatureData = new MemoryStream())
                using (var backup = await BuildBackupAsync().ConfigureAwait(true))
                {
                    await BackupDataProtector.CreateDetachedSignatureDataAsync(backup, deatachedSignatureData, publicKeyStream).ConfigureAwait(false);

                    var responce = await BackupServiceClient.UploadAsync(new Uri(UploadUrl), Fingerprint, publicKeyStream, deatachedSignatureData, backup).ConfigureAwait(true);
                    Assert.That(responce, Is.True);
                }
            }

            {// Download

                using (var backup = await BackupServiceClient.DownloadAsync(new Uri(DownloadUrl), backupName).ConfigureAwait(true))
                {
                    var verificationKeyStorage = new MockPgpKeyStorage().Get();

                    using (var publicKeyStream = await BackupServiceClient.DownloadAsync(new Uri(DownloadUrl), publickeyName).ConfigureAwait(true))
                    using (var verificationContext = new TuviPgpContext(verificationKeyStorage))
                    {
                        await verificationContext.LoadContextAsync().ConfigureAwait(true);

                        var ring = new PgpPublicKeyRing(publicKeyStream);
                        var bundle = new PgpPublicKeyRingBundle(new PgpObject[] { ring });
                        verificationContext.Import(bundle);

                        var backupDataSignatureVerifier = BackupProtectorCreator.CreateBackupProtector(verificationContext);
                        backupDataSignatureVerifier.SetPgpKeyIdentity(TestData.BackupPgpKeyIdentity);

                        using (var deatachedSignatureData = await BackupServiceClient.DownloadAsync(new Uri(DownloadUrl), signatureName).ConfigureAwait(true))
                        {
                            var signed = await backupDataSignatureVerifier.VerifySignatureAsync(backup, deatachedSignatureData).ConfigureAwait(false);
                            Assert.That(signed, Is.True);
                        }
                    }

                    backup.Position = 0;
                    await ParseBackupAsync(backup).ConfigureAwait(true);
                }
            }
        }
    }
}
