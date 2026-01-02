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
using System.Threading;
using System.Threading.Tasks;
using KeyDerivation.Keys;
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
