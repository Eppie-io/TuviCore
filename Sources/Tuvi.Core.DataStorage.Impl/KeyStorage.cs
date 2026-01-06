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

using System.Threading;
using System.Threading.Tasks;
using KeyDerivation.Keys;
using Tuvi.Core.Entities;
using TuviPgpLib;
using TuviPgpLib.Entities;

namespace Tuvi.Core.DataStorage.Impl
{
    [SQLite.Table("Settings")]
    public class SettingsTable
    {
        [SQLite.PrimaryKey]
        [SQLite.Unique]
        public string Key { get; set; }

        public int Value { get; set; }
    }

    class Files
    {
        [SQLite.PrimaryKey]
        public string Key { get; set; }

        [SQLite.Indexed]
        public string Name { get; set; }

        public byte[] Data { get; set; }
    }

    internal class KeyStorage : PasswordProtectedStorage, IKeyStorage
    {
        public KeyStorage(string filePath)
            : base(filePath)
        {
        }

        public async Task<bool> IsMasterKeyExistAsync(CancellationToken cancellationToken)
        {
            var key = await GetMasterKeyImplAsync(cancellationToken).ConfigureAwait(false);
            return key != null;
        }

        public async Task<MasterKey> GetMasterKeyAsync(CancellationToken cancellationToken)
        {
            var file = await GetMasterKeyImplAsync(cancellationToken).ConfigureAwait(false);
            if (file is null)
            {
                throw new CoreException("Master key is not found");
            }

            return KeySerialization.ToMasterKey(file.Data);
        }

        public async Task<PgpPublicKeyBundle> GetPgpPublicKeysAsync(CancellationToken cancellationToken)
        {
            var file = await GetFilesAsync(nameof(PgpPublicKeyBundle), cancellationToken).ConfigureAwait(false);
            if (file != null)
            {
                return new PgpPublicKeyBundle { Data = file.Data };
            }

            return null;
        }

        public async Task<PgpSecretKeyBundle> GetPgpSecretKeysAsync(CancellationToken cancellationToken = default)
        {
            var file = await GetFilesAsync(nameof(PgpSecretKeyBundle), cancellationToken).ConfigureAwait(false);
            if (file != null)
            {
                return new PgpSecretKeyBundle { Data = file.Data };
            }

            return null;
        }

        public Task InitializeMasterKeyAsync(MasterKey masterKey, CancellationToken cancellationToken)
        {
            return DoCancellableTaskAsync(async (ct) =>
            {
                try
                {
                    const string fileName = nameof(MasterKey);
                    await Database.InsertAsync(new Files { Key = fileName, Name = fileName, Data = masterKey.ToByteBuffer() })
                                  .ConfigureAwait(false);
                }
                catch (SQLite.SQLiteException exp)
                {
                    throw new DataBaseException(exp.Message, exp);
                }
            }, cancellationToken);
        }

        public void SavePgpPublicKeys(PgpPublicKeyBundle keyBundle)
        {
            InsertOrUpdateFile(nameof(PgpPublicKeyBundle), keyBundle.Data);
        }

        public void SavePgpSecretKeys(PgpSecretKeyBundle keyBundle)
        {
            InsertOrUpdateFile(nameof(PgpSecretKeyBundle), keyBundle.Data);
        }

        private Task<Files> GetMasterKeyImplAsync(CancellationToken cancellationToken)
        {
            return GetFilesAsync(nameof(MasterKey), cancellationToken);
        }

        private Task<Files> GetFilesAsync(string fileName, CancellationToken cancellationToken)
        {
            return DoCancellableTaskAsync(async (ct) =>
            {
                try
                {
                    return await Database.FindAsync<Files>(x => x.Name == fileName).ConfigureAwait(false);
                }
                catch (SQLite.SQLiteException exp)
                {
                    throw new DataBaseException(exp.Message, exp);
                }
            }, cancellationToken);
        }

        private void InsertOrUpdateFile(string fileName, byte[] data)
        {
            try
            {
                Database.GetConnection().InsertOrReplace(new Files { Key = fileName, Name = fileName, Data = data });
            }
            catch (SQLite.SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }
    }
}
