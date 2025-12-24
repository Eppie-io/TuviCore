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

namespace Tuvi.Core.Backup.Impl.JsonUtf8
{
    public class JsonUtf8SerializationFactory : IBackupSerializationFactory
    {
        private string PackageIdentifier;
        private IBackupProtector BackupDataProtector;

        public JsonUtf8SerializationFactory(IBackupProtector dataProtector)
        {
            if (dataProtector is null)
            {
                throw new ArgumentNullException(nameof(dataProtector));
            }

            BackupDataProtector = dataProtector;
        }

        public void SetPackageIdentifier(string packageIdentifier)
        {
            if (packageIdentifier is null)
            {
                throw new ArgumentNullException(nameof(packageIdentifier));
            }

            PackageIdentifier = packageIdentifier;
        }

        public IBackupBuilder CreateBackupBuilder()
        {
            return new JsonUtf8BackupBuilder(PackageIdentifier, BackupDataProtector);
        }

        public IBackupParser CreateBackupParser()
        {
            return new JsonUtf8BackupParser(PackageIdentifier, BackupDataProtector);
        }
    }
}
