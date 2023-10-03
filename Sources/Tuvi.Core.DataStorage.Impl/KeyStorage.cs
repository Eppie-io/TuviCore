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
            try
            {
                string fileName = nameof(MasterKey);
                var file = await Database.FindAsync<Files>(x => x.Name == fileName).ConfigureAwait(false);
                return file != null;
            }
            catch (SQLite.SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }

        public async Task<MasterKey> GetMasterKeyAsync(CancellationToken cancellationToken)
        {
            try
            {
                const string fileName = nameof(MasterKey);
                var file = await Database.FindAsync<Files>(x => x.Name == fileName).ConfigureAwait(false);
                if (file is null)
                {
                    throw new CoreException("Master key is not found");
                }

                return KeySerialization.ToMasterKey(file.Data); ;
            }
            catch (SQLite.SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }

        public async Task<PgpPublicKeyBundle> GetPgpPublicKeysAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string fileName = nameof(PgpPublicKeyBundle);
                var file = await Database.FindAsync<Files>(x => x.Name == fileName).ConfigureAwait(false);

                if (file != null)
                {
                    return new PgpPublicKeyBundle { Data = file.Data };
                }

                return null;
            }
            catch (SQLite.SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }

        public async Task<PgpSecretKeyBundle> GetPgpSecretKeysAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string fileName = nameof(PgpSecretKeyBundle);
                var file = await Database.FindAsync<Files>(x => x.Name == fileName).ConfigureAwait(false);

                if (file != null)
                {
                    return new PgpSecretKeyBundle { Data = file.Data };
                }

                return null;
            }
            catch (SQLite.SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }

        public async Task InitializeMasterKeyAsync(MasterKey masterKey, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Run(() => SaveMasterKey(masterKey), cancellationToken).ConfigureAwait(false);
            }
            catch (SQLite.SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }

        public void SavePgpPublicKeys(PgpPublicKeyBundle keyBundle)
        {
            InsertOrUpdateFile(nameof(PgpPublicKeyBundle), keyBundle.Data);
        }



        public void SavePgpSecretKeys(PgpSecretKeyBundle keyBundle)
        {
            InsertOrUpdateFile(nameof(PgpSecretKeyBundle), keyBundle.Data);
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

        private async void SaveMasterKey(MasterKey masterKey)
        {
            string fileName = nameof(MasterKey);
            await Database.InsertAsync(new Files { Key = fileName, Name = fileName, Data = masterKey.ToByteBuffer() }).ConfigureAwait(false);
        }
    }
}
