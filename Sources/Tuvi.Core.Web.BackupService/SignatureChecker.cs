using Org.BouncyCastle.Bcpg.OpenPgp;
using System;
using System.IO;
using System.Threading.Tasks;
using Tuvi.Core.Backup.Impl;
using TuviPgpLibImpl;

namespace Tuvi.Core.Web.BackupService
{
    public static class SignatureChecker
    {
        public static async Task<bool> IsValidSignatureAsync(Stream publicKeyStream, Stream signatureStream, Stream backupStream)
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
                backupDataSignatureVerifier.SetPgpKeyIdentity(ImplementationDetailsProvider.BackupPgpKeyIdentity);

                return await backupDataSignatureVerifier.VerifySignatureAsync(backupStream, signatureStream).ConfigureAwait(false);
            }
        }
    }
}
