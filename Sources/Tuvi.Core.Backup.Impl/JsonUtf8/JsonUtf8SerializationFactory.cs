using System;

namespace Tuvi.Core.Backup.Impl.JsonUtf8
{
    public class JsonUtf8SerializationFactory : IBackupSerializationFactory
    {
        private string PackageIdentifier;
        private IBackupProtector BackupDataProtector;

        public JsonUtf8SerializationFactory(IBackupProtector dataProtector)
        {
            if (dataProtector is null)
            {
                throw new ArgumentNullException(nameof(dataProtector));
            }

            BackupDataProtector = dataProtector;
        }

        public void SetPackageIdentifier(string packageIdentifier)
        {
            if (packageIdentifier is null)
            {
                throw new ArgumentNullException(nameof(packageIdentifier));
            }

            PackageIdentifier = packageIdentifier;
        }

        public IBackupBuilder CreateBackupBuilder()
        {
            return new JsonUtf8BackupBuilder(PackageIdentifier, BackupDataProtector);
        }

        public IBackupParser CreateBackupParser()
        {
            return new JsonUtf8BackupParser(PackageIdentifier, BackupDataProtector);
        }
    }
}
