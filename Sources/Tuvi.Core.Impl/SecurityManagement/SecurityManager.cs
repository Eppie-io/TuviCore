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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KeyDerivation;
using KeyDerivation.Keys;
using KeyDerivationLib;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
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
            IBackupProtector backupProtector,
            IPublicKeyService publicKeyService)
        {
            return new SecurityManager(storage, pgpContext, messageProtector, backupProtector, publicKeyService);
        }

        public static ISeedQuiz CreateSeedQuiz(string[] seedPhrase)
        {
            return new SeedQuiz(seedPhrase);
        }
    }

    internal class SecurityManager : ISecurityManager
    {
        private SeedQuiz _seedQuiz;
        private IKeyDerivationDetailsProvider _keyDerivationDetails;
        private MasterKeyFactory _keyFactory;
        private Dictionary<SpecialPgpKeyType, string> _specialPgpKeyIdentities;
        private readonly SeedValidator _seedValidator;
        private readonly IKeyStorage _keyStorage;
        private readonly IDataStorage _dataStorage;
        private readonly ITuviPgpContext _pgpContext;
        private readonly IMessageProtector _messageProtector;
        private readonly IBackupProtector _backupProtector;
        private readonly IPublicKeyService _publicKeyService;

        /// <param name="backupProtector">Setup of protector is performed here.</param>
        public SecurityManager(
            IDataStorage storage,
            ITuviPgpContext pgpContext,
            IMessageProtector messageProtector,
            IBackupProtector backupProtector,
            IPublicKeyService publicKeyService)
        {
            _seedValidator = new SeedValidator();
            _keyStorage = storage ?? throw new ArgumentNullException(nameof(storage));
            _dataStorage = storage;
            _pgpContext = pgpContext ?? throw new ArgumentNullException(nameof(pgpContext));
            _messageProtector = messageProtector ?? throw new ArgumentNullException(nameof(messageProtector));
            _backupProtector = backupProtector ?? throw new ArgumentNullException(nameof(backupProtector));
            _publicKeyService = publicKeyService ?? throw new ArgumentNullException(nameof(publicKeyService));
        }

        private void InitializeManager()
        {
            _keyFactory = new MasterKeyFactory(_keyDerivationDetails);
            _specialPgpKeyIdentities = _keyDerivationDetails.GetSpecialPgpKeyIdentities();
            _backupProtector.SetPgpKeyIdentity(_specialPgpKeyIdentities[SpecialPgpKeyType.Backup]);
        }

        public void SetKeyDerivationDetails(IKeyDerivationDetailsProvider keyDerivationDetailsProvider)
        {
            if (keyDerivationDetailsProvider is null)
            {
                throw new ArgumentNullException(nameof(keyDerivationDetailsProvider));
            }

            _keyDerivationDetails = keyDerivationDetailsProvider;
            InitializeManager();
        }

        public async Task<bool> IsSeedPhraseInitializedAsync(CancellationToken cancellationToken = default)
        {
            return await _keyStorage.IsMasterKeyExistAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<string[]> CreateSeedPhraseAsync()
        {
            if (_keyFactory is null)
            {
                throw new InvalidOperationException($"{nameof(_keyFactory)} is not initialized.");
            }

            var seedPhrase = await Task.Run(() => _keyFactory.GenerateSeedPhrase()).ConfigureAwait(false);
            _seedQuiz = new SeedQuiz(seedPhrase);

            return seedPhrase;
        }

        public Task RestoreSeedPhraseAsync(string[] seedPhrase)
        {
            if (_keyFactory is null)
            {
                throw new InvalidOperationException($"{nameof(_keyFactory)} is not initialized.");
            }

            return Task.Run(() => _keyFactory.RestoreSeedPhrase(seedPhrase));
        }

        public async Task StartAsync(string password, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await _dataStorage.IsStorageExistAsync(cancellationToken).ConfigureAwait(false))
                {
                    await _dataStorage.OpenAsync(password, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _dataStorage.CreateAsync(password, cancellationToken).ConfigureAwait(false);
                }

                await _pgpContext.LoadContextAsync().ConfigureAwait(false);

                if (!await _keyStorage.IsMasterKeyExistAsync(cancellationToken).ConfigureAwait(false))
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
            using (var masterKey = await Task.Run(() => _keyFactory.GetMasterKey()).ConfigureAwait(false))
            {
                await _keyStorage.InitializeMasterKeyAsync(masterKey, cancellationToken).ConfigureAwait(false);
                await CreateDefaultPgpKeysForAllAccountsAsync(cancellationToken).ConfigureAwait(false);
                await CreateSpecialPgpKeysAsync().ConfigureAwait(false);
            }
        }

        public async Task ResetAsync()
        {
            // TODO: Zeroise seed phrase.
            _seedQuiz = null;

            await _dataStorage.ResetAsync().ConfigureAwait(false);
        }

        public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken)
        {
            await _dataStorage.ChangePasswordAsync(currentPassword, newPassword, cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> IsNeverStartedAsync(CancellationToken cancellationToken)
        {
            return !await _dataStorage.IsStorageExistAsync(cancellationToken).ConfigureAwait(false);
        }

        public ISeedQuiz GetSeedQuiz()
        {
            return _seedQuiz;
        }

        public ISeedValidator GetSeedValidator()
        {
            return _seedValidator;
        }

        private Task<MasterKey> GetMasterKeyAsync(CancellationToken cancellationToken = default)
        {
            return _keyStorage.GetMasterKeyAsync(cancellationToken);
        }

        private async Task CreateSpecialPgpKeysAsync()
        {
            using (var masterKey = await GetMasterKeyAsync().ConfigureAwait(false))
            {
                foreach (var specialKey in _specialPgpKeyIdentities)
                {
                    string keyIdentity = specialKey.Value;
                    var dummyBackupAddress = new EmailAddress(keyIdentity);

                    _pgpContext.GeneratePgpKeysByTagOld(masterKey, keyIdentity, keyIdentity);
                }
            }
        }

        private async Task CreateDefaultPgpKeysForAllAccountsAsync(CancellationToken cancellationToken = default)
        {
            var accounts = await _dataStorage.GetAccountsAsync(cancellationToken).ConfigureAwait(false);

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
                _pgpContext.GeneratePgpKeysByTag(masterKey, account.GetPgpUserIdentity(), account.GetKeyTag());
            }
            else if (account.Email.IsDecentralized)
            {
                _pgpContext.GeneratePgpKeysByBip44(masterKey, account.GetPgpUserIdentity(), account.GetCoinType(), account.DecentralizedAccountIndex, account.GetChannel(), account.GetKeyIndex());
            }
            else
            {
                _pgpContext.GeneratePgpKeysByTagOld(masterKey, account.GetPgpUserIdentity(), account.GetKeyTag());
            }
        }

        public ICollection<PgpKeyInfo> GetPublicPgpKeysInfo()
        {
            List<PgpKeyInfo> keys = _pgpContext.GetPublicKeysInfo().ToList();
            keys.RemoveAll(key => IsServiceKey(key));
            return keys;
        }

        private bool IsServiceKey(PgpKeyInfo pgpKey)
        {
            foreach (var specialKey in _specialPgpKeyIdentities)
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
                _pgpContext.ImportPublicKeys(stream, true);
            }
        }

        public void ImportPgpKeyRingBundle(Stream keyBundle)
        {
            using (ArmoredInputStream keyIn = new ArmoredInputStream(keyBundle))
            {
                var header = keyIn.GetArmorHeaderLine();
                if (header?.IndexOf("private", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    _pgpContext.ImportSecretKeys(keyIn, false);
                    return;
                }
                if (header?.IndexOf("public", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    _pgpContext.ImportPublicKeys(keyIn, false);
                    return;
                }
            }
            throw new PgpException("Stream does not contain any key bundle.");
        }

        public Task ExportPgpKeyRingAsync(long keyId, Stream stream, CancellationToken cancellationToken)
        {
            return _pgpContext.ExportPublicKeyRingAsync(keyId, stream, cancellationToken);
        }

        public int GetRequiredSeedPhraseLength()
        {
            if (_keyDerivationDetails is null)
            {
                throw new InvalidOperationException($"{nameof(_keyDerivationDetails)} is not set.");
            }

            return _keyDerivationDetails.GetSeedPhraseLength();
        }

        public IMessageProtector GetMessageProtector()
        {
            return _messageProtector;
        }

        public IBackupProtector GetBackupProtector()
        {
            return _backupProtector;
        }

        public void RemovePgpKeys(Account account)
        {
            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            RemovePgpKeys(account.Email);
        }

        public void RemovePgpKeys(EmailAddress email)
        {
            if (email is null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            string identity = email.GetPgpUserIdentity();

            // Skip removal if it matches any special service key identity
            foreach (var specialKey in _specialPgpKeyIdentities)
            {
                if (identity.Equals(specialKey.Value, StringComparison.Ordinal))
                {
                    return; // Do not remove service keys
                }
            }

            _pgpContext.RemoveKeys(identity);
        }

        public async Task<(string, int)> GetNextDecAccountPublicKeyAsync(NetworkType network, CancellationToken cancellationToken)
        {
            var settings = await _dataStorage.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            int accountIndex;
            switch (network)
            {
                case NetworkType.Eppie:
                    accountIndex = settings.EppieAccountCounter;
                    break;
                case NetworkType.Bitcoin:
                    accountIndex = settings.BitcoinAccountCounter;
                    break;
                case NetworkType.Ethereum:
                    accountIndex = settings.EthereumAccountCounter;
                    break;
                default:
                    throw new NotSupportedException($"Network type {network} is not supported.");
            }

            using (var masterKey = await GetMasterKeyAsync(cancellationToken).ConfigureAwait(false))
            {
                var address = _publicKeyService.DeriveNetworkAddress(masterKey, network, network.GetCoinType(), accountIndex, network.GetChannel(), network.GetKeyIndex());
                return (address, accountIndex);
            }
        }

        // TODO: Remove secret key export functionality.
        public async Task<string> GetSecretKeyWIFAsync(Account account, CancellationToken cancellationToken)
        {
            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            using (var masterKey = await GetMasterKeyAsync(cancellationToken).ConfigureAwait(false))
            {
                var network = account.Email.Network;
                switch (network)
                {
                    case NetworkType.Bitcoin:
                        {
                            return Tools.DeriveBitcoinSecretKeyWif(masterKey, account.DecentralizedAccountIndex, network.GetKeyIndex());
                        }
                    case NetworkType.Ethereum:
                        {
                            return Dec.Ethereum.EthereumClientFactory
                                    .Create(Dec.Ethereum.EthereumNetwork.MainNet, null)
                                    .DeriveEthereumPrivateKeyHex(masterKey, account.DecentralizedAccountIndex, network.GetKeyIndex());
                        }
                    default:
                        throw new NotSupportedException($"Network type {network} is not supported.");
                }
            }
        }

        public async Task<string> GetEmailPublicKeyStringAsync(EmailAddress email, CancellationToken cancellationToken = default)
        {
            if (email is null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            if (email.IsDecentralized)
            {
                return await _publicKeyService.GetEncodedByEmailAsync(email, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using (var masterKey = await GetMasterKeyAsync(cancellationToken).ConfigureAwait(false))
                {
                    return _publicKeyService.DeriveEncoded(masterKey, email.GetKeyTag());
                }
            }
        }

        public async Task ActivateAddressAsync(Account account, CancellationToken cancellationToken = default)
        {
            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            if (!account.Email.IsDecentralized || account.Email.Network != NetworkType.Bitcoin)
            {
                throw new NotSupportedException($"The provided account ({account.Email}) is not a Bitcoin decentralized account.");
            }

            if (account.DecentralizedAccountIndex < 0)
            {
                throw new InvalidOperationException("Account decentralized index is not initialized.");
            }

            using (var masterKey = await GetMasterKeyAsync(cancellationToken).ConfigureAwait(false))
            {
                await Tools.ActivateBitcoinAddressAsync(masterKey, account.DecentralizedAccountIndex, account.Email.Network.GetKeyIndex(), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
