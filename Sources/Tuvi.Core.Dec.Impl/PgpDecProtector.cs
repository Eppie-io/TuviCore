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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KeyDerivation.Keys;
using MimeKit;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Parameters;
using Tuvi.Core.Entities;
using Tuvi.Core.Utils;
using TuviPgpLib;
using TuviPgpLibImpl;

[assembly: InternalsVisibleTo("Tuvi.Core.Mail.Impl.Tests")]

namespace Tuvi.Core.Dec.Impl
{
    internal class PgpDecProtector : IDecProtector
    {
        private readonly IKeyStorage _keyStorage;
        private readonly IPublicKeyService _publicKeyService;

        public PgpDecProtector(IKeyStorage keyStorage, IPublicKeyService publicKeyService)
        {
            _keyStorage = keyStorage ?? throw new ArgumentNullException(nameof(keyStorage));
            _publicKeyService = publicKeyService ?? throw new ArgumentNullException(nameof(publicKeyService));
        }

        public Task<string> DecryptAsync(Account account, byte[] data, CancellationToken cancellationToken)
        {
            if (account.Email.IsHybrid)
            {
                return DecryptAsync(account.GetPgpUserIdentity(), account.GetKeyTag(), data, cancellationToken);
            }
            else
            {
                return DecryptAsync(account.Email.Network, account.GetPgpUserIdentity(), account.DecentralizedAccountIndex, data, cancellationToken);
            }
        }

        public async Task<byte[]> EncryptAsync(string address, string data, CancellationToken cancellationToken)
        {
            using (var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(_keyStorage).ConfigureAwait(false))
            {
                return Encrypt(pgpContext, address, data, cancellationToken);
            }
        }

        private async Task<string> DecryptAsync(string identity, string tag, byte[] data, CancellationToken cancellationToken)
        {
            using (var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(_keyStorage).ConfigureAwait(false))
            {
                var masterKey = await _keyStorage.GetMasterKeyAsync(cancellationToken).ConfigureAwait(false);
                return Decrypt(pgpContext, masterKey, identity, tag, data, cancellationToken);
            }
        }

        private async Task<string> DecryptAsync(NetworkType network, string identity, int account, byte[] data, CancellationToken cancellationToken)
        {
            using (var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(_keyStorage).ConfigureAwait(false))
            {
                var masterKey = await _keyStorage.GetMasterKeyAsync(cancellationToken).ConfigureAwait(false);
                return Decrypt(pgpContext, masterKey, identity, network, account, data, cancellationToken);
            }
        }

        private byte[] Encrypt(TuviPgpContext pgpContext, string address, string message, CancellationToken cancellationToken = default)
        {
            if (pgpContext is null)
            {
                throw new ArgumentNullException(nameof(pgpContext));
            }

            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentException("Address cannot be null or empty.", nameof(address));
            }

            ECPublicKeyParameters reconvertedPublicKey = _publicKeyService.Decode(address);
            PgpPublicKeyRing publicKeyRing = TuviPgpContext.CreatePgpPublicKeyRing(reconvertedPublicKey, reconvertedPublicKey, address);
            PgpPublicKey publicKey = publicKeyRing.GetPublicKeys().FirstOrDefault(x => x.IsEncryptionKey);

            using (var inputData = new MemoryStream(Encoding.UTF8.GetBytes(message ?? "")))
            {
                var encryptedMime = pgpContext.Encrypt(new List<PgpPublicKey> { publicKey }, inputData, cancellationToken);

                using (var encryptedData = new MemoryStream())
                {
                    encryptedMime.WriteTo(FormatOptions.Default, encryptedData, contentOnly: true, cancellationToken);

                    return encryptedData.ToArray();
                }
            }
        }

        private static string Decrypt(TuviPgpContext pgpContext, MasterKey masterKey, string identity, string tag, byte[] data, CancellationToken cancellationToken = default)
        {
            Contract.Requires(pgpContext != null);
            Contract.Requires(masterKey != null);
            Contract.Requires(identity != null);
            Contract.Requires(data != null);

            pgpContext.GeneratePgpKeysByTag(masterKey, identity, tag);

            using (var encryptedData = new MemoryStream(data))
            using (var decryptedData = new MemoryStream(data.Length))
            {
                pgpContext.DecryptTo(encryptedData, decryptedData, cancellationToken);
                return Encoding.UTF8.GetString(decryptedData.ToArray());
            }
        }

        private static string Decrypt(TuviPgpContext pgpContext, MasterKey masterKey, string identity, NetworkType network, int account, byte[] data, CancellationToken cancellationToken = default)
        {
            Contract.Requires(pgpContext != null);
            Contract.Requires(masterKey != null);
            Contract.Requires(identity != null);
            Contract.Requires(data != null);

            pgpContext.GeneratePgpKeysByBip44(masterKey, identity, network.GetCoinType(), account, network.GetChannel(), network.GetKeyIndex());

            using (var encryptedData = new MemoryStream(data))
            using (var decryptedData = new MemoryStream(data.Length))
            {
                pgpContext.DecryptTo(encryptedData, decryptedData, cancellationToken);
                return Encoding.UTF8.GetString(decryptedData.ToArray());
            }
        }
    }
}
