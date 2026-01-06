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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities.Exceptions;

namespace Tuvi.Core.Backup.Impl.JsonUtf8
{
    internal abstract class PackageParserBase
    {
        private readonly PackageHeaderVersion HeaderVersion = PackageHeaderVersion.V1;

        protected abstract string GetPackageIdentifier();
        protected abstract Task ParsePackageContentAsync(Stream content, CancellationToken cancellationToken);
        protected abstract DataProtectionFormat GetSupportedDataProtectionFormat();

        protected async Task ParsePackageAsync(Stream package, CancellationToken cancellationToken)
        {
            try
            {
                await TryParsePackageAsync(package, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw new BackupParsingException("Error in package parsing.", exception);
            }
        }

        private async Task TryParsePackageAsync(Stream package, CancellationToken cancellationToken)
        {
            var contentProtectionFormat = await ParsePackageHeaderAsync(package, cancellationToken).ConfigureAwait(false);
            if (contentProtectionFormat == GetSupportedDataProtectionFormat())
            {
                await ParsePackageContentAsync(package, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<DataProtectionFormat> ParsePackageHeaderAsync(Stream package, CancellationToken cancellationToken)
        {
            if (!await IsCorrectPackageIdentifierAsync(package, cancellationToken).ConfigureAwait(false))
            {
                throw new NotBackupPackageException();
            }

            if (HeaderVersion != await GetHeaderVersionAsync(package, cancellationToken).ConfigureAwait(false))
            {
                throw new BackupParsingException("Unsupported package header version.");
            }

            return await GetDataProtectionFormatAsync(package, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> IsCorrectPackageIdentifierAsync(Stream package, CancellationToken cancellationToken)
        {
            byte[] referencePackageIdentifier = Encoding.ASCII.GetBytes(GetPackageIdentifier());
            byte[] packageIdentifier = new byte[referencePackageIdentifier.Length];

            await package.ReadAsync(packageIdentifier, 0, referencePackageIdentifier.Length, cancellationToken).ConfigureAwait(false);
            return packageIdentifier.SequenceEqual(referencePackageIdentifier);
        }

        private static async Task<PackageHeaderVersion> GetHeaderVersionAsync(Stream package, CancellationToken cancellationToken)
        {
            byte[] bytes = new byte[sizeof(PackageHeaderVersion)];

            await package.ReadAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);

            int headerVersion = bytes.FromByteBuffer();

            if (!Enum.IsDefined(typeof(PackageHeaderVersion), headerVersion))
            {
                throw new BackupParsingException("Unknown package header version.");
            }

            return (PackageHeaderVersion)headerVersion;
        }

        private static async Task<DataProtectionFormat> GetDataProtectionFormatAsync(Stream package, CancellationToken cancellationToken)
        {
            byte[] bytes = new byte[sizeof(DataProtectionFormat)];

            await package.ReadAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);

            int dataProtectionValue = bytes.FromByteBuffer();

            if (!Enum.IsDefined(typeof(DataProtectionFormat), dataProtectionValue))
            {
                throw new UnknownBackupProtectionException();
            }

            return (DataProtectionFormat)dataProtectionValue;
        }
    }
}
