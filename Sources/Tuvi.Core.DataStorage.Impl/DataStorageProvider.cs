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
        /// Use one <see cref="IDataStorage"/> per file in a single process.
        /// Cross-process access and WebAssembly are unsupported. Different files have independent lifecycles.
        /// <para>
        /// Reset permanently rejects database access, even if cleanup fails or the file is absent.
        /// It waits for active local database work, closes the connection and deletes the file.
        /// A new instance may own the file after the previous owner's close and cleanup have completed.
        /// Dispose permanently rejects access and waits for an admitted reset and connection close.
        /// Reset retains its original result; Dispose also reports any unconfirmed connection close.
        /// Public tasks and committed event callbacks may finish later; neither operation interrupts SQL.
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
