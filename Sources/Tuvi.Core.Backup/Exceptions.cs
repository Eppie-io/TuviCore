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
using TuviPgpLib.Entities;

namespace Tuvi.Core.Backup
{
    public class BackupVerificationException : BackupDataProtectionException
    {
        public BackupVerificationException()
        {
        }

        public BackupVerificationException(string message) : base(message)
        {
        }

        public BackupVerificationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class BackupDataProtectionException : CryptoContextException
    {
        public BackupDataProtectionException()
        {
        }

        public BackupDataProtectionException(string message) : base(message)
        {
        }

        public BackupDataProtectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
