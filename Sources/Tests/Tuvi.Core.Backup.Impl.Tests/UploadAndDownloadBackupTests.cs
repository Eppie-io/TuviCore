using System;
using System.IO;
using System.Threading.Tasks;
using Tuvi.Core.Web.BackupService;
using BackupServiceClientLibrary;
using NUnit.Framework;
using Org.BouncyCastle.Bcpg.OpenPgp;
using SecurityManagementTests;
using Tuvi.Core.Backup.Impl;
using TuviPgpLibImpl;

namespace BackupTests
{
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
