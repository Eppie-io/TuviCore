using KeyDerivation.Keys;
using System;
using System.Threading;
using System.Threading.Tasks;
using TuviPgpLib;
using TuviPgpLib.Entities;

namespace Tuvi.Core.Web.BackupService
{
    public sealed class PgpKeyStorage : IKeyStorage
    {
        private PgpPublicKeyBundle PublicKeyStorage;
        private PgpSecretKeyBundle SecretKeyStorage;

        public PgpKeyStorage()
        {
            PublicKeyStorage = null;
            SecretKeyStorage = null;
        }

        public Task InitializeMasterKeyAsync(MasterKey masterKey, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsMasterKeyExistAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<MasterKey> GetMasterKeyAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public void SavePgpPublicKeys(PgpPublicKeyBundle keyBundle)
        {
            PublicKeyStorage = keyBundle;
        }

        public void SavePgpSecretKeys(PgpSecretKeyBundle keyBundle)
        {
            SecretKeyStorage = keyBundle;
        }

        public Task<PgpPublicKeyBundle> GetPgpPublicKeysAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PublicKeyStorage);
        }

        public Task<PgpSecretKeyBundle> GetPgpSecretKeysAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SecretKeyStorage);
        }
    }
}
