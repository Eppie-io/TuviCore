using KeyDerivation.Keys;
using MimeKit;
using MimeKit.Cryptography;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core;
using Tuvi.Core.Entities;
using Tuvi.Core.Utils;
using TuviPgpLib;
using TuviPgpLib.Entities;
using TuviPgpLibImpl;

namespace Tuvi.Core.Impl
{
    internal class TemporalKeyStorage : IKeyStorage
    {
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

    public static class EccPgpExtension
    {
        const int EppieCoinType = 3630;
        const int EmailChannel = 10;
        const int KeyIndex = 0;

        public static async Task<TuviPgpContext> GetTemporalContextAsync(IKeyStorage storage)
        {
            var context = new TuviPgpContext(new TemporalKeyStorage(storage));
            await context.LoadContextAsync().ConfigureAwait(false);
            return context;
        }

        public static byte[] Encrypt(TuviPgpContext pgpContext, string address, string message, CancellationToken cancellationToken = default)
        {
            if (pgpContext is null)
            {
                throw new ArgumentNullException(nameof(pgpContext));
            }

            ECPublicKeyParameters reconvertedPublicKey = PublicKeyConverter.ConvertEmailNameToPublicKey(address);
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

        public static string Decrypt(TuviPgpContext pgpContext, MasterKey masterKey, string identity, string tag, byte[] data, CancellationToken cancellationToken = default)
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

        public static string Decrypt(TuviPgpContext pgpContext, MasterKey masterKey, string identity, int account, byte[] data, CancellationToken cancellationToken = default)
        {
            Contract.Requires(pgpContext != null);
            Contract.Requires(masterKey != null);
            Contract.Requires(identity != null);
            Contract.Requires(data != null);

            pgpContext.GeneratePgpKeysByBip44(masterKey, identity, EppieCoinType, account, EmailChannel, KeyIndex);

            using (var encryptedData = new MemoryStream(data))
            using (var decryptedData = new MemoryStream(data.Length))
            {
                pgpContext.DecryptTo(encryptedData, decryptedData, cancellationToken);
                return Encoding.UTF8.GetString(decryptedData.ToArray());
            }
        }

        public static MailboxAddress ToMailboxAddress(this EmailAddress emailAddress)
        {
            Contract.Requires(emailAddress != null);
            return new MailboxAddress(emailAddress.Name ?? "", emailAddress.Address);
        }

        public static string GetPublicKeyString(MasterKey masterKey, int account)
        {
            var publicKeyPar = EccPgpContext.GenerateEccPublicKey(masterKey, EppieCoinType, account, EmailChannel, KeyIndex);

            return PublicKeyConverter.ConvertPublicKeyToEmailName(publicKeyPar);
        }

        public static string GetPublicKeyString(MasterKey masterKey, string keyTag)
        {            
            var publicKeyPar = EccPgpContext.GenerateEccPublicKey(masterKey, keyTag);

            return PublicKeyConverter.ConvertPublicKeyToEmailName(publicKeyPar);
        }

        public static OpenPgpContext TryToAddDecPublicKeys(this OpenPgpContext context, EmailAddress emailAddress)
        {
            Contract.Requires(context != null);
            Contract.Requires(emailAddress != null);
            try
            {
                var keys = context.GetPublicKeys(new List<MailboxAddress>() { emailAddress.ToMailboxAddress() }).ToList();
                var existingPublicKey = keys.FirstOrDefault();
                if (existingPublicKey != null)
                {
                    return context;
                }
            }
            catch (PublicKeyNotFoundException)
            {
            }
            var decAddress = emailAddress.DecentralizedAddress;
            if (String.IsNullOrEmpty(decAddress))
            {
                return context;
            }

            ECPublicKeyParameters reconvertedPublicKey = PublicKeyConverter.ConvertEmailNameToPublicKey(decAddress);
            PgpPublicKeyRing keyRing = TuviPgpContext.CreatePgpPublicKeyRing(reconvertedPublicKey, reconvertedPublicKey, emailAddress.Address);

            context.Import(keyRing);
            return context;
        }

        public static void CreatePgpKeys(this ITuviPgpContext pgpContext, MasterKey masterKey, Account account)
        {
            if (pgpContext is null)
            {
                throw new ArgumentNullException(nameof(pgpContext));
            }

            if (masterKey is null)
            {
                throw new ArgumentNullException(nameof(masterKey));
            }

            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            if (account.Email.IsHybrid)
            {
                pgpContext.GeneratePgpKeysByTag(masterKey, account.GetPgpUserIdentity(), account.GetKeyTag());
            }
            else if (account.Email.IsDecentralized)
            {
                pgpContext.GeneratePgpKeysByBip44(masterKey, account.GetPgpUserIdentity(), EppieCoinType, account.DecentralizedAccountIndex, EmailChannel, KeyIndex);
            }
            else
            {
                pgpContext.GeneratePgpKeysByTagOld(masterKey, account.GetPgpUserIdentity(), account.GetKeyTag());
            }
        }
    }
}
