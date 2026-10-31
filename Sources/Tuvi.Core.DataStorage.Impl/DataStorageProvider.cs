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

namespace Tuvi.Core.DataStorage.Impl
{
    public static class DataStorageProvider
    {

        /// <summary>
        /// Creates a storage instance for accounts, messages and keys.
        /// </summary>
        /// <param name="path">Path to the database file.</param>
        /// <returns>The storage instance that owns the file.</returns>
        /// <remarks>
        /// Use one <see cref="IDataStorage"/> per file; cross-process access is unsupported.
        /// Different files have independent lifecycles.
        /// <para>
        /// Reset rejects new database access and waits for the current operation. After
        /// success, this instance can recreate the database; old composite calls cannot enter it.
        /// Dispose permanently rejects access and waits for connection close. Rejected
        /// waiters may finish later; neither operation forcibly interrupts SQL.
        /// </para>
        /// Application workflows must coordinate reset separately.
        /// </remarks>
        public static IDataStorage GetDataStorage(string path)
        {
            var db = new DataStorage(path);
            return db;
        }
    }
}
