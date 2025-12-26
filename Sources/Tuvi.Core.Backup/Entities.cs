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

namespace Tuvi.Core.Backup
{
    /// <summary>
    /// Enumeration of package header versions.
    /// </summary>
    /// <remarks>
    /// Used in binary protocol.
    /// Don't change existing elements and order.
    /// </remarks>
    public enum PackageHeaderVersion : Int32
    {
        V1
    }

    /// <summary>
    /// Enumeration of possible data protection formats.
    /// </summary>
    /// <remarks>
    /// Used in binary protocol.
    /// Don't change existing elements and order.
    /// </remarks>
    public enum DataProtectionFormat : Int32
    {
        PgpEncryptionWithSignature = 0
    }

    /// <summary>
    /// Enumeration of possible content serialization formats.
    /// </summary>
    /// <remarks>
    /// Used in binary protocol.
    /// Don't change existing elements and order.
    /// </remarks>
    public enum ContentSerializationFormat : Int32
    {
        JsonUtf8 = 0
    }
}
