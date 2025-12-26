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

namespace Tuvi.Core.Backup
{
    /// <summary>
    /// Factory interface to create mutual backup data package builders and parsers.
    /// </summary>
    public interface IBackupSerializationFactory
    {
        /// <summary>
        /// Set backup packages identifier which later created builders and parsers will use.
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        void SetPackageIdentifier(string packageIdentifier);

        /// <summary>
        /// Used to build backup package. One builder per one backup package.
        /// </summary>
        IBackupBuilder CreateBackupBuilder();

        /// <summary>
        /// Used to parse backup package. One parser per one backup package.
        /// </summary>
        IBackupParser CreateBackupParser();
    }
}
