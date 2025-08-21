using KeyDerivation;
using KeyDerivation.Keys;
using KeyDerivationLib;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Backup;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Dec.Bitcoin.TestNet4;
using Tuvi.Core.Entities;
using Tuvi.Core.Mail;
using Tuvi.Core.Utils;
using TuviPgpLib;
using TuviPgpLib.Entities;

namespace Tuvi.Core.Impl.SecurityManagement
{
    public static class SecurityManagerCreator
    {
        public static ISecurityManager GetSecurityManager(
            IDataStorage storage,
            ITuviPgpContext pgpContext,
            IMessageProtector messageProtector,
            IBackupProtector backupProtector)
        {
            return new SecurityManager(storage, pgpContext, messageProtector, backupProtector);
        }

        public static ISeedQuiz CreateSeedQuiz(string[] seedPhrase)
        {
            return new SeedQuiz(seedPhrase);
        }
    }

    internal class SecurityManager : ISecurityManager
    {
        /// <param name="backupProtector">Setup of protector is performed here.</param>
        public SecurityManager(
            IDataStorage storage,
            ITuviPgpContext pgpContext,
            IMessageProtector messageProtector,
            IBackupProtector backupProtector)
        {
            SeedValidator = new SeedValidator();
            KeyStorage = storage;
            DataStorage = storage;
            PgpContext = pgpContext;
            MessageProtector = messageProtector;
            BackupProtector = backupProtector;
        }

        private void InitializeManager()
        {
            KeyFactory = new MasterKeyFactory(KeyDerivationDetails);
            SpecialPgpKeyIdentities = KeyDerivationDetails.GetSpecialPgpKeyIdentities();
            BackupProtector.SetPgpKeyIdentity(SpecialPgpKeyIdentities[SpecialPgpKeyType.Backup]);
        }

        public void SetKeyDerivationDetails(IKeyDerivationDetailsProvider keyDerivationDetailsProvider)
        {
            if (keyDerivationDetailsProvider is null)
            {
                throw new ArgumentNullException(nameof(keyDerivationDetailsProvider));
            }

            KeyDerivationDetails = keyDerivationDetailsProvider;
            InitializeManager();
        }

        public async Task<bool> IsSeedPhraseInitializedAsync(CancellationToken cancellationToken = default)
        {
            return await KeyStorage.IsMasterKeyExistAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<string[]> CreateSeedPhraseAsync()
        {
            if (KeyFactory is null)
            {
                throw new InvalidOperationException($"{nameof(KeyFactory)} is not initialized.");
            }

            var seedPhrase = await Task.Run(() => KeyFactory.GenerateSeedPhrase()).ConfigureAwait(false);
            SeedQuiz = new SeedQuiz(seedPhrase);

            return seedPhrase;
        }

        public Task RestoreSeedPhraseAsync(string[] seedPhrase)
        {
            if (KeyFactory is null)
            {
                throw new InvalidOperationException($"{nameof(KeyFactory)} is not initialized.");
            }

            return Task.Run(() => KeyFactory.RestoreSeedPhrase(seedPhrase));
        }

        public async Task StartAsync(string password, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await DataStorage.IsStorageExistAsync(cancellationToken).ConfigureAwait(false))
                {
                    await DataStorage.OpenAsync(password, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await DataStorage.CreateAsync(password, cancellationToken).ConfigureAwait(false);
                }

                await PgpContext.LoadContextAsync().ConfigureAwait(false);

                if (!await KeyStorage.IsMasterKeyExistAsync(cancellationToken).ConfigureAwait(false))
                {
                    await InitializeMasterKeyAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (DataBasePasswordException)
            {
                throw;
            }
        }

        public async Task InitializeMasterKeyAsync(CancellationToken cancellationToken = default)
        {
            using (var masterKey = await Task.Run(() => KeyFactory.GetMasterKey()).ConfigureAwait(false))
            {
                await KeyStorage.InitializeMasterKeyAsync(masterKey, cancellationToken).ConfigureAwait(false);
                await CreateDefaultPgpKeysForAllAccountsAsync(cancellationToken).ConfigureAwait(false);
                await CreateSpecialPgpKeysAsync().ConfigureAwait(false);
            }
        }

        public async Task ResetAsync()
        {
            // TODO: Zeroise seed phrase.
            SeedQuiz = null;

            await DataStorage.ResetAsync().ConfigureAwait(false);
        }

        public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken)
        {
            await DataStorage.ChangePasswordAsync(currentPassword, newPassword, cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> IsNeverStartedAsync(CancellationToken cancellationToken)
        {
            return !await DataStorage.IsStorageExistAsync(cancellationToken).ConfigureAwait(false);
        }

        public ISeedQuiz GetSeedQuiz()
        {
            return SeedQuiz;
        }

        public ISeedValidator GetSeedValidator()
        {
            return SeedValidator;
        }

        private Task<MasterKey> GetMasterKeyAsync(CancellationToken cancellationToken = default)
        {
            return KeyStorage.GetMasterKeyAsync(cancellationToken);
        }

        private async Task CreateSpecialPgpKeysAsync()
        {
            using (var masterKey = await GetMasterKeyAsync().ConfigureAwait(false))
            {
                foreach (var specialKey in SpecialPgpKeyIdentities)
                {
                    string keyIdentity = specialKey.Value;
                    var dummyBackupAddress = new EmailAddress(keyIdentity);

                    PgpContext.GeneratePgpKeysByTagOld(masterKey, keyIdentity, keyIdentity);
                }
            }
        }

        private async Task CreateDefaultPgpKeysForAllAccountsAsync(CancellationToken cancellationToken = default)
        {
            var accounts = await DataStorage.GetAccountsAsync(cancellationToken).ConfigureAwait(false);

            using (var masterKey = await GetMasterKeyAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var account in accounts)
                {
                    CreateDefaultPgpKeys(account, masterKey);
                }
            }
        }

        public async Task CreateDefaultPgpKeysAsync(Account account)
        {
            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            using (var masterKey = await GetMasterKeyAsync().ConfigureAwait(false))
            {
                CreateDefaultPgpKeys(account, masterKey);
            }
        }

        private void CreateDefaultPgpKeys(Account account, MasterKey masterKey)
        {
            if (account.Email.IsHybrid)
            {
                PgpContext.GeneratePgpKeysByTag(masterKey, account.GetPgpUserIdentity(), account.GetKeyTag());
            }
            else if (account.Email.IsDecentralized)
            {
                PgpContext.GeneratePgpKeysByBip44(masterKey, account.GetPgpUserIdentity(), account.GetCoinType(), account.DecentralizedAccountIndex, account.GetChannel(), account.GetKeyIndex());
            }
            else
            {
                PgpContext.GeneratePgpKeysByTagOld(masterKey, account.GetPgpUserIdentity(), account.GetKeyTag());
            }
        }

        public ICollection<PgpKeyInfo> GetPublicPgpKeysInfo()
        {
            List<PgpKeyInfo> keys = PgpContext.GetPublicKeysInfo().ToList();
            keys.RemoveAll(key => IsServiceKey(key));
            return keys;
        }

        private bool IsServiceKey(PgpKeyInfo pgpKey)
        {
            foreach (var specialKey in SpecialPgpKeyIdentities)
            {
                if (pgpKey.UserIdentity.Contains(specialKey.Value))
                {
                    return true;
                }
            }
            return false;
        }

        public void ImportPublicPgpKey(byte[] keyData)
        {
            using (var stream = new MemoryStream(keyData))
            {
                PgpContext.ImportPublicKeys(stream, true);
            }
        }

        public void ImportPgpKeyRingBundle(Stream keyBundle)
        {
            using (ArmoredInputStream keyIn = new ArmoredInputStream(keyBundle))
            {
                var header = keyIn.GetArmorHeaderLine();
                if (header?.IndexOf("private", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    PgpContext.ImportSecretKeys(keyIn, false);
                    return;
                }
                if (header?.IndexOf("public", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    PgpContext.ImportPublicKeys(keyIn, false);
                    return;
                }
            }
            throw new PgpException("Stream does not contain any key bundle.");
        }

        public Task ExportPgpKeyRingAsync(long keyId, Stream stream, CancellationToken cancellationToken)
        {
            return PgpContext.ExportPublicKeyRingAsync(keyId, stream, cancellationToken);
        }

        public int GetRequiredSeedPhraseLength()
        {
            if (KeyDerivationDetails is null)
            {
                throw new InvalidOperationException($"{nameof(KeyDerivationDetails)} is not set.");
            }

            return KeyDerivationDetails.GetSeedPhraseLength();
        }

        public IMessageProtector GetMessageProtector()
        {
            return MessageProtector;
        }

        public IBackupProtector GetBackupProtector()
        {
            return BackupProtector;
        }

        public void RemovePgpKeys(Account account)
        {
            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            PgpContext.RemoveKeys(account.GetPgpUserIdentity());
        }

        public async Task<(string, int)> GetNextDecAccountPublicKeyAsync(NetworkType network, CancellationToken cancellationToken)
        {
            var settings = await DataStorage.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            using (var masterKey = await GetMasterKeyAsync(cancellationToken).ConfigureAwait(false))
            {
                switch (network)
                {
                    case NetworkType.Eppie:
                        {
                            var account = settings.EppieAccountCounter;
                            return (PublicKeyConverter.ToPublicKeyBase32E(masterKey, network.GetCoinType(), account, network.GetChannel(), network.GetKeyIndex()), account);
                        }
                    case NetworkType.Bitcoin:
                        {
                            var account = settings.BitcoinAccountCounter;
                            return (GetBitcoinAddressString(masterKey, account), account);
                        }
                    default:
                        throw new NotSupportedException($"Network type {network} is not supported.");
                }
            }
        }

        public async Task<string> GetSecretKeyWIFAsync(Account account)
        {
            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            if (account.Email.Network == NetworkType.Bitcoin)
            {
                using (var masterKey = await GetMasterKeyAsync().ConfigureAwait(false))
                {
                    return GetBitcoinSecretKeyWIF(masterKey, account.DecentralizedAccountIndex);
                }
            }

            throw new NotSupportedException($"Network type {account.Email.Network} is not supported.");
        }

        private static string GetBitcoinAddressString(MasterKey masterKey, int account)
        {
            return Tools.DeriveBitcoinAddress(masterKey, account, NetworkType.Bitcoin.GetKeyIndex());
        }

        private static string GetBitcoinSecretKeyWIF(MasterKey masterKey, int account)
        {
            return Tools.DeriveBitcoinSecretKeyWif(masterKey, account, NetworkType.Bitcoin.GetKeyIndex());
        }

        public async Task<string> GetEmailPublicKeyStringAsync(EmailAddress email)
        {
            using (var masterKey = await GetMasterKeyAsync().ConfigureAwait(false))
            {
                return PublicKeyConverter.ToPublicKeyBase32E(masterKey, email.GetKeyTag());
            }
        }

        private SeedQuiz SeedQuiz;
        private IKeyDerivationDetailsProvider KeyDerivationDetails;
        private MasterKeyFactory KeyFactory;
        private Dictionary<SpecialPgpKeyType, string> SpecialPgpKeyIdentities;
        private readonly SeedValidator SeedValidator;
        private readonly IKeyStorage KeyStorage;
        private readonly IDataStorage DataStorage;
        private readonly ITuviPgpContext PgpContext;
        private readonly IMessageProtector MessageProtector;
        private readonly IBackupProtector BackupProtector;
    }
}