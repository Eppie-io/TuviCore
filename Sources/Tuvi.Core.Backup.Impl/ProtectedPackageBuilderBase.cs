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
