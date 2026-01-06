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

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KeyDerivation;
using Tuvi.Core.Backup;
using Tuvi.Core.Entities;
using Tuvi.Core.Mail;
using TuviPgpLib.Entities;

namespace Tuvi.Core
{
    /// <summary>
    /// Application seed phrase and password protected storage management.
    /// PGP mail protection keys management.
    /// </summary>
    public interface ISecurityManager
    {
        /// <summary>
        /// Set key derivation details provider. Has to be setup before any key operations.
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        void SetKeyDerivationDetails(IKeyDerivationDetailsProvider keyDerivationDetails);

        /// <summary>
        /// Checks if security manager has never been started before.
        /// Can be used to detect application first start.
        /// </summary>
        Task<bool> IsNeverStartedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if seed phrase is initialized.
        /// </summary>
        Task<bool> IsSeedPhraseInitializedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Initialize master key.
        /// Seed has to be restored <see cref="RestoreSeedPhraseAsync(string[])"/>
        /// or created <see cref="CreateSeedPhraseAsync()"/> previously to call this method.
        /// </summary>
        /// <exception cref="DataBaseException" />
        Task InitializeMasterKeyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates new random seed phrase.
        /// </summary>
        /// <returns>Seed phrase used to restore same master key on another device or after application reset</returns>
        Task<string[]> CreateSeedPhraseAsync();

        /// <summary>
        /// Restores application seed phrase from <paramref name="seedPhrase"/>.
        /// </summary>
        Task RestoreSeedPhraseAsync(string[] seedPhrase);

        /// <summary>
        /// Start application security manager.
        /// On first application start will set up application password.
        /// Furthermore it will initialize seed phrase if <see cref="CreateSeedPhraseAsync"/> or <see cref="RestoreSeedPhraseAsync(string[])"/>
        /// has been called previously.
        /// </summary>
        /// <param name="storagePassword">Application key storage protection password</param>
        /// <exception cref="DataBasePasswordException">If incorrect password provided.</exception>
        /// <exception cref="DataBaseException">On other storage problems.</exception>
        Task StartAsync(string storagePassword, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reset application to uninitialized state. All keys and stored data are wiped.
        /// </summary>
        /// <exception cref="DataBaseException" />
        Task ResetAsync();

        /// <summary>
        /// Change application password to <paramref name="newPassword"/>.
        /// </summary>
        /// <exception cref="DataBasePasswordException">If incorrect <paramref name="currentPassword"/> provided.</exception>
        /// <exception cref="DataBaseException"/>
        Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);

        /// <summary>
        /// Initialize email account default PGP keys. Keys are deterministically derived from application master key.
        /// That's mean such PGP keys can be restored on another device with same seed phrase and email account identity.
        /// </summary>
        /// <param name="account">Account which PGP keys need to be initialized</param>
        /// <exception cref="PgpArgumentNullException"/>
        Task CreateDefaultPgpKeysAsync(Account account);

        /// <summary>
        /// Get all public PGP keys information.
        /// </summary>
        /// <returns>Public keys information</returns>
        ICollection<PgpKeyInfo> GetPublicPgpKeysInfo();

        /// <summary>
        /// Import public PGP key in armored format. 
        /// </summary>
        /// <param name="keyData">Key data in armored format.</param>
        /// <exception cref="PublicKeyAlreadyExistException" />
        /// <exception cref="UnknownPublicKeyAlgorithmException" />
        /// <exception cref="ImportPublicKeyException" />
        void ImportPublicPgpKey(byte[] keyData);

        /// <summary>
        ///  Import PGP key bundle in armored format (public or secret).
        /// </summary>
        /// <param name="keyBundle">Key bundle to import in armored format.</param>
        void ImportPgpKeyRingBundle(Stream keyBundle);

        /// <summary>
        /// Export armored public keyring containing key with <paramref name="keyId"/> to <paramref name="stream"/>.
        /// </summary>
        /// <exception cref="OperationCanceledException"/>
        /// <exception cref="ExportPublicKeyException"/>
        Task ExportPgpKeyRingAsync(long keyId, Stream stream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Length of seed phrase required by application
        /// </summary>
        int GetRequiredSeedPhraseLength();

        /// <summary>
        /// Get seed quiz game instance.
        /// </summary>
        ISeedQuiz GetSeedQuiz();

        /// <summary>
        /// Get seed validator instance.
        /// </summary>
        ISeedValidator GetSeedValidator();

        /// <summary>
        /// Get message protector instance.
        /// </summary>
        IMessageProtector GetMessageProtector();

        /// <summary>
        /// Get backup protector instance.
        /// </summary>
        IBackupProtector GetBackupProtector();

        /// <summary>
        /// Remove PGP keys for <paramref name="account"/>.
        /// </summary>
        void RemovePgpKeys(Account account);

        /// <summary>
        /// Remove PGP keys for <paramref name="email"/>.
        /// </summary>
        void RemovePgpKeys(EmailAddress email);

        /// <summary>
        /// Get next decentralized account public key and index.
        /// </summary> 
        Task<(string, int)> GetNextDecAccountPublicKeyAsync(NetworkType networkType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get secret key WIF.
        /// </summary> 
        Task<string> GetSecretKeyWIFAsync(Account account, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get email public key string for <paramref name="email"/>.
        /// For decentralized addresses: resolves to public key via PublicKeyService
        /// For other addresses: derives the public key from master key.
        /// </summary>
        Task<string> GetEmailPublicKeyStringAsync(EmailAddress email, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activate a decentralized address by building and broadcasting an activation transaction.
        /// The method derives keys from master key and performs activation. For Bitcoin network only for now.
        /// </summary>
        Task ActivateAddressAsync(Account account, CancellationToken cancellationToken = default);
    }
}
