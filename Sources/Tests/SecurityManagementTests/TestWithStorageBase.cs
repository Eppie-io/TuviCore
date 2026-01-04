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
using System.Threading.Tasks;
using Tuvi.Core.DataStorage;
using Tuvi.Core.DataStorage.Impl;
using TuviPgpLib;

namespace SecurityManagementTests
{
    public class TestWithStorageBase
    {
        protected const string Password = "123456";
        protected const string IncorrectPassword = "65432211";
        protected const string NewPassword = "987654321";

        protected IPasswordProtectedStorage GetPasswordProtectedStorage()
        {
            return GetDataStorage();
        }

        protected IKeyStorage GetKeyStorage()
        {
            return GetDataStorage();
        }

        protected IDataStorage GetDataStorage()
        {
            return DataStorageProvider.GetDataStorage(DataBasePath);
        }

        protected void DeleteStorage()
        {
            if (File.Exists(DataBasePath))
            {
                File.Delete(DataBasePath);
            }
        }

        protected Task CreateStorageAsync()
        {
            return GetDataStorage().CreateAsync(Password);
        }

        private const string DataBaseFileName = "TestTuviMail.db";
        private readonly string DataBasePath = Path.Combine(Environment.CurrentDirectory, DataBaseFileName);
    }
}
