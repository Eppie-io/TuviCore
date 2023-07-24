using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities.Exceptions;

namespace Tuvi.Core.Backup.Impl
{
    internal abstract class PackageBuilderBase
    {
        private readonly PackageHeaderVersion HeaderVersion = PackageHeaderVersion.V1;

        protected abstract string GetPackageIdentifier();
        protected abstract DataProtectionFormat GetDataProtectionFormat();
        protected abstract Task BuildPackageContentAsync(Stream package, CancellationToken cancellationToken);

        protected async Task BuildPackageAsync(Stream package, CancellationToken cancellationToken)
        {
            try
            {
                await BuildPackageHeaderAsync(package, cancellationToken).ConfigureAwait(false);
                await BuildPackageContentAsync(package, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw new BackupBuildingException("Error in backup package construction.", exception);
            }
        }

        private async Task BuildPackageHeaderAsync(Stream outputStream, CancellationToken cancellationToken)
        {
            byte[] packageIdentifier = Encoding.ASCII.GetBytes(GetPackageIdentifier());
            await outputStream.WriteAsync(packageIdentifier, 0, packageIdentifier.Length, cancellationToken).ConfigureAwait(false);

            byte[] headerVersion = Convert.ToInt32(HeaderVersion, CultureInfo.InvariantCulture).ToByteBuffer();
            await outputStream.WriteAsync(headerVersion, 0, headerVersion.Length, cancellationToken).ConfigureAwait(false);

            byte[] dataProtection = Convert.ToInt32(GetDataProtectionFormat(), CultureInfo.InvariantCulture).ToByteBuffer();
            await outputStream.WriteAsync(dataProtection, 0, dataProtection.Length, cancellationToken).ConfigureAwait(false);
        }
    }
}
