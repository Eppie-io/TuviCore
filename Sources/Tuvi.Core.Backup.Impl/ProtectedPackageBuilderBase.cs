using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core.Backup.Impl
{
    internal abstract class ProtectedPackageBuilderBase : PackageBuilderBase
    {
        private readonly IBackupProtector DataLocker;

        protected ProtectedPackageBuilderBase(IBackupProtector dataLocker)
        {
            if (dataLocker is null)
            {
                throw new ArgumentNullException(nameof(dataLocker));
            }

            DataLocker = dataLocker;
        }

        protected abstract Task BuildProtectedPackageContentAsync(Stream content, CancellationToken cancellationToken);

        protected sealed override DataProtectionFormat GetDataProtectionFormat()
        {
            return DataLocker.GetSupportedDataProtectionFormat();
        }

        protected sealed override async Task BuildPackageContentAsync(Stream protectedPackage, CancellationToken cancellationToken)
        {
            using (var unprotectedData = new MemoryStream())
            {
                await BuildProtectedPackageContentAsync(unprotectedData, cancellationToken).ConfigureAwait(false);
                unprotectedData.Position = 0;
                await DataLocker.LockDataAsync(unprotectedData, protectedPackage, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
