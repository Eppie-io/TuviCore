using KeyDerivation.Keys;
using System;
using System.Threading;
using System.Threading.Tasks;
using TuviPgpLib;
using TuviPgpLib.Entities;
using TuviPgpLibImpl;

namespace Tuvi.Core.Dec.Impl
{
    public class TemporalKeyStorage : IKeyStorage
    {
        public static async Task<TuviPgpContext> GetTemporalContextAsync(IKeyStorage storage)
        {
            var context = new TuviPgpContext(new TemporalKeyStorage(storage));
            await context.LoadContextAsync().ConfigureAwait(false);
            return context;
        }

        private readonly IKeyStorage _externalKeyStorage;
        public TemporalKeyStorage(IKeyStorage keyStorage)
        {
            _externalKeyStorage = keyStorage;
        }

        public Task<MasterKey> GetMasterKeyAsync(CancellationToken cancellationToken = default)
        {
            return _externalKeyStorage.GetMasterKeyAsync(cancellationToken);
        }

        public Task<PgpPublicKeyBundle> GetPgpPublicKeysAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PgpPublicKeyBundle());
        }

        public Task<PgpSecretKeyBundle> GetPgpSecretKeysAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PgpSecretKeyBundle());
        }

        public Task InitializeMasterKeyAsync(MasterKey masterKey, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsMasterKeyExistAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public void SavePgpPublicKeys(PgpPublicKeyBundle keyBundle)
        {
            // doesn't save the state
        }

        public void SavePgpSecretKeys(PgpSecretKeyBundle keyBundle)
        {
            // doesn't save the state
        }
    }
}
