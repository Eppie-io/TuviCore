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
using System.Threading.Tasks;
using SQLite;

namespace Tuvi.Core.DataStorage.Impl
{
    internal sealed class StorageConnection : IDisposable
    {
        private readonly SQLiteConnectionWithLock _connection;

        public StorageConnection(SQLiteConnectionString options)
        {
            _connection = new SQLiteConnectionWithLock(options);
        }

        public T Run<T>(Func<SQLiteConnection, T> operation)
        {
            using (_connection.Lock())
            {
                return operation(_connection);
            }
        }

        public void Run(Action<SQLiteConnection> operation)
        {
            using (_connection.Lock())
            {
                operation(_connection);
            }
        }

        public Task<T> RunAsync<T>(Func<SQLiteConnection, T> operation)
        {
            return Task.Run(() => Run(operation));
        }

        public Task RunAsync(Action<SQLiteConnection> operation)
        {
            return Task.Run(() => Run(operation));
        }

        public Task RunInTransactionAsync(Action<SQLiteConnection> operation)
        {
            return RunAsync(connection => connection.RunInTransaction(() => operation(connection)));
        }

        public Task CloseAsync()
        {
            return Task.Run(Dispose);
        }

        public void Dispose()
        {
            using (_connection.Lock())
            {
                _connection.Dispose();
            }
        }
    }
}
