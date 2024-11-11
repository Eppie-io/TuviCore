using Tuvi.Core.Backup.Impl;
using NUnit.Framework;
using SecurityManagementTests;
using System.IO;
using System.Threading.Tasks;
using TuviPgpLibImpl;
using Org.BouncyCastle.Bcpg.OpenPgp;

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
                    Assert.IsTrue(signed);
                }

                backup.Position = 0;
                await ParseBackupAsync(backup).ConfigureAwait(true);
            }
        }
    }
}
