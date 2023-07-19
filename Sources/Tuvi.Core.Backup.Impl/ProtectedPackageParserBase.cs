using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Backup.Impl.JsonUtf8;

namespace Tuvi.Core.Backup.Impl
{
    internal abstract class ProtectedPackageParserBase : PackageParserBase
    {
        private readonly IBackupDataUnlocker DataUnlocker;

        protected ProtectedPackageParserBase(IBackupDataUnlocker dataUnlocker)
        {
            if (dataUnlocker is null)
            {
                throw new ArgumentNullException(nameof(dataUnlocker));
            }

            DataUnlocker = dataUnlocker;
        }

        protected abstract Task ParseProtectedPackageContentAsync(Stream content, CancellationToken cancellationToken);

        protected sealed override async Task ParsePackageContentAsync(Stream protectedContent, CancellationToken cancellationToken)
        {
            using (var unprotectedContent = new MemoryStream())
            {
                await DataUnlocker.UnlockDataAsync(protectedContent, unprotectedContent, cancellationToken).ConfigureAwait(false);
                unprotectedContent.Position = 0;
                await ParseProtectedPackageContentAsync(unprotectedContent, cancellationToken).ConfigureAwait(false);
            }
        }

        protected sealed override DataProtectionFormat GetSupportedDataProtectionFormat()
        {
            return DataUnlocker.GetSupportedDataProtectionFormat();
        }
    }
}
