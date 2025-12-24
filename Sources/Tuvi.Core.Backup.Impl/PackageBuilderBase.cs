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
