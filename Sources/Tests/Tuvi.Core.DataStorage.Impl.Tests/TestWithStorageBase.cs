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
using System.Threading.Tasks;
using Tuvi.Core.DataStorage.Impl;

namespace Tuvi.Core.DataStorage.Tests
{
    public class TestWithStorageBase
    {
        protected const string Password = "123456";
        protected const string IncorrectPassword = "65432211";
        protected const string NewPassword = "987654321";

        protected IDataStorage GetDataStorage()
        {
            return DataStorageProvider.GetDataStorage(DataBasePath);
        }

        protected async Task<IDataStorage> OpenDataStorageAsync()
        {
            var db = GetDataStorage();
            await db.OpenAsync(Password).ConfigureAwait(false);
            return db;
        }

        protected async Task<IDataStorage> CreateDataStorageAsync()
        {
            var db = GetDataStorage();
            await db.CreateAsync(Password).ConfigureAwait(false);
            return db;
        }

        protected bool DatabaseFileExists()
        {
            return File.Exists(DataBasePath);
        }

        protected void DeleteStorage()
        {
            if (DatabaseFileExists())
            {
                File.Delete(DataBasePath);
            }
        }

        private const string DataBaseFileName = "TestTuviMail.db";
        private readonly string DataBasePath = Path.Combine(Environment.CurrentDirectory, DataBaseFileName);
    }
}
