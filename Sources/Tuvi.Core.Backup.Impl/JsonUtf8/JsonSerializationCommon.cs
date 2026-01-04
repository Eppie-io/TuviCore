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

using System.Collections.Generic;

namespace Tuvi.Core.Backup.Impl
{
    /// <summary>
    /// Common internal json backup data representation.
    /// </summary>
    /// <remarks>
    /// byte[] is a backup section content: json-serialized base64-encoded data.
    /// </remarks>
    internal class BackupSectionsDictionary : Dictionary<BackupSectionType, byte[]>
    { }

    /// <summary>
    /// Enumeration of logical backup sections.
    /// </summary>
    /// <remarks>
    /// Don't change existing element names.
    /// </remarks>
    public enum BackupSectionType : int
    {
        Version = 0,
        Time,
        Accounts,
        AddressBook,
        Messages,
        PublicKeys,
        Settings,
    }
}
