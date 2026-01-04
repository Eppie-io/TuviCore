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
