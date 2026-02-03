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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using Tuvi.Core.Logging;
using Tuvi.Core.Mail;
using Tuvi.Core.Utils;

[assembly: InternalsVisibleTo("Tuvi.Core.Mail.Tests")]

namespace Tuvi.Core.Dec.Impl
{
    /// <summary>
    /// Factory for decentralized mailbox.
    /// </summary>
    public static class MailBoxCreator
    {
        public static IMailBox Create(Account account, IDecStorage storage, IDecStorageClient decClient, IPublicKeyService publicKeyService)
        {
            if (account is null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            if (storage is null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            var protector = new PgpDecProtector(storage, publicKeyService);

            return new DecMailBox(account, storage, decClient, protector, publicKeyService);
        }
    }

    public class DecEnvelope
    {
        public int Version { get; set; } = 1;
        public string PayloadHash { get; set; }
        public string Nonce { get; set; }
    }

    internal class DecMailBox : IMailBox
    {
        private readonly Account AccountSettings;
        private readonly IDecStorageClient DecClient;
        private readonly IDecStorage Storage;
        private readonly IDecProtector Protector;
        private readonly IPublicKeyService PublicKeyService;

        public bool HasFolderCounters => false;

        internal DecMailBox(Account accountData,
                           IDecStorage storage,
                           IDecStorageClient decClient,
                           IDecProtector protector,
                           IPublicKeyService publicKeyService)
        {
            AccountSettings = accountData ?? throw new ArgumentNullException(nameof(accountData));
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
            DecClient = decClient ?? throw new ArgumentNullException(nameof(decClient));
            Protector = protector ?? throw new ArgumentNullException(nameof(protector));
            PublicKeyService = publicKeyService ?? throw new ArgumentNullException(nameof(publicKeyService));
        }

        private static string GetStringHashSha256(string input)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes(input));

                return StringHelper.BytesToHex(hash);
            }
        }

        private static string CreateEnvelopeNonceBase64()
        {
            var nonce = new byte[32];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce);
            }

            var base64 = Convert.ToBase64String(nonce);
            return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Sends a decentralized message using a two-object transport to avoid metadata leakage.
        /// </summary>
        /// <remarks>
        /// Workflow per recipient:
        /// <list type="number">
        /// <item>
        /// <description>
        /// Serialize the message into JSON (<see cref="DecMessageRaw"/>), encrypt it with recipient public key and upload it.
        /// This creates the <c>payload</c> object and returns <c>payloadHash</c>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Build <see cref="DecEnvelope"/> as a small JSON { version, payloadHash, nonce }, encrypt it with the same recipient public key and upload it.
        /// This creates the <c>envelope</c> object and returns <c>envelopeHash</c>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Send only <c>envelopeHash</c> to the recipient mailbox (<see cref="MailboxId"/>).
        /// On the transport level, only mailboxId + envelopeHash are observable.
        /// </description>
        /// </item>
        /// </list>
        /// The message is additionally saved to the local Sent folder; for Sent items the stored hash is a deterministic
        /// SHA-256 over concatenated per-recipient <c>envelopeHash</c> values.
        /// </remarks>
        public async Task SendMessageAsync(Message message, CancellationToken cancellationToken)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            message.Date = DateTime.Now;
            var rawMessage = new DecMessageRaw(message);
            var data = JsonConvert.SerializeObject(rawMessage);
            var finalHashBuilder = new StringBuilder();

            foreach (var email in message.AllRecipients.Where(x => x.IsDecentralized))
            {
                string publicKey = await PublicKeyService.GetEncodedByEmailAsync(email, cancellationToken).ConfigureAwait(false);

                // payload
                var payloadBytes = await Protector.EncryptAsync(publicKey, data, cancellationToken).ConfigureAwait(false);
                var payloadHash = await DecClient.PutAsync(payloadBytes, cancellationToken).ConfigureAwait(false);

                // envelope
                var envelope = new DecEnvelope
                {
                    PayloadHash = payloadHash,
                    Nonce = CreateEnvelopeNonceBase64()
                };
                var envelopeInner = JsonConvert.SerializeObject(envelope);
                var envelopeBytes = await Protector.EncryptAsync(publicKey, envelopeInner, cancellationToken).ConfigureAwait(false);
                var envelopeHash = await DecClient.PutAsync(envelopeBytes, cancellationToken).ConfigureAwait(false);

                // send only envelope hash to mailbox
                var mailboxId = new MailboxId(publicKey);
                await DecClient.SendAsync(mailboxId.ToString(), envelopeHash, cancellationToken).ConfigureAwait(false);
                finalHashBuilder.Append(envelopeHash);
            }

            message.IsMarkedAsRead = true;
            message.IsDecentralized = true;

            // TODO: Sent currently stores a synthetic hash (SHA-256 of concatenated per-recipient envelopeHash values).
            // In future, keep one Sent item but persist the full list of envelope hashes (delivery details) inside Message/storage
            // and use a stable per-message local id instead of hashing concatenation.
            var finalHash = GetStringHashSha256(finalHashBuilder.ToString());
            await Storage.AddDecMessageAsync(AccountSettings.Email, new DecMessage(finalHash, message), cancellationToken).ConfigureAwait(false);
        }

        public Task<IList<Folder>> GetFoldersStructureAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IList<Folder>>(new List<Folder>
            {
                GetInboxFolder(),
                new Folder("Sent", FolderAttributes.Sent),
                new Folder("Drafts", FolderAttributes.Draft),
                new Folder("Trash", FolderAttributes.Trash),
            });
        }

        public Task<Folder> GetDefaultInboxFolderAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(GetInboxFolder());
        }

        private static Folder GetInboxFolder()
        {
            return new Folder("Inbox", FolderAttributes.Inbox);
        }

        /// <summary>
        /// Retrieves messages for the given folder.
        /// </summary>
        /// <remarks>
        /// For Inbox, the decentralized transport returns a list of <c>envelopeHash</c> values.
        /// Each envelope is processed in two steps:
        /// <list type="number">
        /// <item>
        /// <description>
        /// Download and decrypt the envelope by <c>envelopeHash</c> to extract <c>payloadHash</c>.
        /// Envelope corruption is tolerated: invalid/undecryptable/unparseable envelopes are skipped and logged.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Download and decrypt the payload by <c>payloadHash</c> to obtain the message and persist it locally.
        /// Payload failures are not swallowed and will propagate to the caller.
        /// </description>
        /// </item>
        /// </list>
        /// Received messages are stored locally with <see cref="DecMessage.Hash"/> set to <c>envelopeHash</c>.
        /// </remarks>
        private async Task<List<Message>> GetMessageListAsync(Folder folder, int count, CancellationToken cancellationToken)
        {
            var email = AccountSettings.Email;
            if (folder.IsInbox)
            {
                var mailboxId = await GetMailboxIdAsync(AccountSettings, cancellationToken).ConfigureAwait(false);
                var list = await ListDecClientsMessagesAsync(mailboxId, cancellationToken).ConfigureAwait(false);
                var trash = (await GetFoldersStructureAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(f => f.IsTrash);

                var tasks = list.Distinct().Select(hash => GetMessageListImplAsync(email, folder, trash, hash, cancellationToken));
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            var items = await Storage.GetDecMessagesAsync(email, folder, count, cancellationToken).ConfigureAwait(false);
            return items.ConvertAll(x => x.ToMessage());
        }

        private async Task<MailboxId> GetMailboxIdAsync(Account accountSettings, CancellationToken cancellationToken)
        {
            string publicKey;
            var masterKey = await Storage.GetMasterKeyAsync(cancellationToken).ConfigureAwait(false);

            if (accountSettings.Email.IsHybrid)
            {
                publicKey = PublicKeyService.DeriveEncoded(masterKey, accountSettings.GetKeyTag());
            }
            else
            {
                publicKey = PublicKeyService.DeriveEncoded(masterKey, accountSettings.GetCoinType(), accountSettings.DecentralizedAccountIndex, accountSettings.GetChannel(), accountSettings.GetKeyIndex());
            }

            return new MailboxId(publicKey);
        }

        /// <summary>
        /// Processes a single inbox item identified by <paramref name="envelopeHash"/>.
        /// </summary>
        /// <remarks>
        /// Envelope issues are treated as non-fatal (logged and skipped). Payload/message issues are treated as fatal
        /// and are propagated to the caller.
        /// </remarks>
        private async Task GetMessageListImplAsync(EmailAddress email, Folder folder, Folder trashFolder, string envelopeHash, CancellationToken cancellationToken)
        {
            bool exists = await Storage.IsDecMessageExistsAsync(email, folder.FullName, envelopeHash, cancellationToken).ConfigureAwait(false)
                         || (trashFolder != null && await Storage.IsDecMessageExistsAsync(email, trashFolder.FullName, envelopeHash, cancellationToken).ConfigureAwait(false));

            if (exists)
            {
                return;
            }

            DecEnvelope envelope;

            try
            {
                // resolve envelope -> payload
                var envelopeBytes = await GetDecClientsMessageAsync(envelopeHash, cancellationToken).ConfigureAwait(false);
                var envelopeJson = await Protector.DecryptAsync(AccountSettings, envelopeBytes, cancellationToken).ConfigureAwait(false);
                envelope = JsonConvert.DeserializeObject<DecEnvelope>(envelopeJson);

                if (envelope is null || string.IsNullOrWhiteSpace(envelope.PayloadHash))
                {
                    this.Log().LogWarning("DEC envelope is invalid. Account={Account}, EnvelopeHash={EnvelopeHash}", AccountSettings?.Email?.Address, envelopeHash);
                    return;
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                this.Log().LogWarning(ex, "Failed to process DEC envelope. Account={Account}, EnvelopeHash={EnvelopeHash}", AccountSettings?.Email?.Address, envelopeHash);
                return;
            }

            var payloadBytes = await GetDecClientsMessageAsync(envelope.PayloadHash, cancellationToken).ConfigureAwait(false);
            var payloadJson = await Protector.DecryptAsync(AccountSettings, payloadBytes, cancellationToken).ConfigureAwait(false);
            var message = JsonConvert.DeserializeObject<DecMessageRaw>(payloadJson).ToMessage();
            message.Folder = folder;
            await Storage.AddDecMessageAsync(email, new DecMessage(envelopeHash, message), cancellationToken).ConfigureAwait(false);
        }

        private async Task<IReadOnlyList<string>> ListDecClientsMessagesAsync(MailboxId address, CancellationToken cancellationToken)
        {
            var res = await DecClient.ListAsync(address.ToString(), cancellationToken).ConfigureAwait(false);
            return res.ToList();
        }

        private Task<byte[]> GetDecClientsMessageAsync(string hash, CancellationToken cancellationToken)
        {
            return DecClient.GetAsync(hash, cancellationToken);
        }

        public async Task<IReadOnlyList<Message>> GetMessagesAsync(Folder folder, int count, CancellationToken cancellationToken)
        {
            return await GetMessageListAsync(folder, count, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Message> GetMessageByIDAsync(Folder folder, uint id, CancellationToken cancellationToken)
        {
            var decMessage = await Storage.GetDecMessageAsync(AccountSettings.Email, folder, id, cancellationToken).ConfigureAwait(false);

            return decMessage.ToMessage();
        }

        public Task<Message> GetMessageByIDHighPriorityAsync(Folder folder, uint id, CancellationToken cancellationToken)
        {
            // TODO: implement high priority
            return GetMessageByIDAsync(folder, id, cancellationToken);
        }

        public async Task<IReadOnlyList<Message>> GetLaterMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken)
        {
            var messages = await GetMessageListAsync(folder, count, cancellationToken).ConfigureAwait(false);

            return lastMessage != null ? messages.FindAll(x => x.Id > lastMessage.Id) : messages;
        }

        public async Task<IReadOnlyList<Message>> GetEarlierMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken)
        {
            var messages = await GetMessageListAsync(folder, count, cancellationToken).ConfigureAwait(false);

            return lastMessage != null ? messages.FindAll(x => x.Id < lastMessage.Id) : messages;
        }

        public Task<IReadOnlyList<Message>> GetEarlierMessagesForSynchronizationAsync(Folder folder,
                                                                                      int count,
                                                                                      Message lastMessage,
                                                                                      CancellationToken cancellationToken)
        {
            // TODO: make different logic for message retrieving during synchronization
            return GetEarlierMessagesAsync(folder, count, lastMessage, cancellationToken);
        }

        public async Task MarkMessageAsReadAsync(uint id, Folder folder, CancellationToken cancellationToken)
        {
            var decMessage = await Storage.GetDecMessageAsync(AccountSettings.Email, folder, id, cancellationToken).ConfigureAwait(false);

            if (decMessage != null)
            {
                decMessage.IsMarkedAsRead = true;
                await Storage.UpdateDecMessageAsync(AccountSettings.Email, decMessage, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task MarkMessagesAsReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            foreach (var item in messages)
            {
                await MarkMessageAsReadAsync(item.Id, item.Folder, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task MarkMessageAsUnReadAsync(uint id, Folder folder, CancellationToken cancellationToken)
        {
            var decMessage = await Storage.GetDecMessageAsync(AccountSettings.Email, folder, id, cancellationToken).ConfigureAwait(false);

            if (decMessage != null)
            {
                decMessage.IsMarkedAsRead = false;
                await Storage.UpdateDecMessageAsync(AccountSettings.Email, decMessage, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task MarkMessagesAsUnReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            foreach (var item in messages)
            {
                await MarkMessageAsUnReadAsync(item.Id, item.Folder, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task DeleteMessageAsync(uint id, Folder folder, bool permanentDelete, CancellationToken cancellationToken)
        {
            var decMessage = await Storage.GetDecMessageAsync(AccountSettings.Email, folder, id, cancellationToken).ConfigureAwait(false);

            if (permanentDelete)
            {
                await Storage.DeleteDecMessageAsync(AccountSettings.Email, folder, id, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var trash = AccountSettings.FoldersStructure.Find(f => f.IsTrash);

                decMessage.FolderName = trash.FullName;
                decMessage.FolderAttributes = trash.Attributes;

                await Storage.UpdateDecMessageAsync(AccountSettings.Email, decMessage, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task DeleteMessagesAsync(IReadOnlyList<uint> ids, Folder folder, bool permanentDelete = false, CancellationToken cancellationToken = default)
        {
            permanentDelete = permanentDelete || folder.IsTrash;

            foreach (var id in ids)
            {
                await DeleteMessageAsync(id, folder, permanentDelete, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task MoveMessagesAsync(IReadOnlyList<uint> ids, Folder folder, Folder targetFolder, CancellationToken cancellationToken)
        {
            foreach (var id in ids)
            {
                var decMessage = await Storage.GetDecMessageAsync(AccountSettings.Email, folder, id, cancellationToken).ConfigureAwait(false);

                decMessage.FolderName = targetFolder.FullName;
                decMessage.FolderAttributes = targetFolder.Attributes;

                await Storage.UpdateDecMessageAsync(AccountSettings.Email, decMessage, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<Message> AppendDraftMessageAsync(Message message, CancellationToken cancellationToken)
        {
            var decMessage = await Storage.AddDecMessageAsync(AccountSettings.Email, new DecMessage(null, message), cancellationToken).ConfigureAwait(false);

            return decMessage.ToMessage();
        }

        public async Task<Message> ReplaceDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken)
        {
            await Storage.DeleteDecMessageAsync(AccountSettings.Email, message.Folder, id, cancellationToken).ConfigureAwait(false);

            var decMessage = await Storage.AddDecMessageAsync(AccountSettings.Email, new DecMessage(null, message), cancellationToken).ConfigureAwait(false);

            return decMessage.ToMessage();
        }

        private async Task FlagMessageAsync(uint id, Folder folder, CancellationToken cancellationToken)
        {
            var decMessage = await Storage.GetDecMessageAsync(AccountSettings.Email, folder, id, cancellationToken).ConfigureAwait(false);

            if (decMessage != null)
            {
                decMessage.IsFlagged = true;
                await Storage.UpdateDecMessageAsync(AccountSettings.Email, decMessage, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task FlagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            foreach (var item in messages)
            {
                await FlagMessageAsync(item.Id, item.Folder, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task UnflagMessageAsync(uint id, Folder folder, CancellationToken cancellationToken)
        {
            var decMessage = await Storage.GetDecMessageAsync(AccountSettings.Email, folder, id, cancellationToken).ConfigureAwait(false);

            if (decMessage != null)
            {
                decMessage.IsFlagged = false;
                await Storage.UpdateDecMessageAsync(AccountSettings.Email, decMessage, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task UnflagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            foreach (var item in messages)
            {
                await UnflagMessageAsync(item.Id, item.Folder, cancellationToken).ConfigureAwait(false);
            }
        }

        public Task<Folder> CreateFolderAsync(string folderName, CancellationToken cancellationToken = default)
        {
            // DEC protocol doesn't support folder creation in the traditional sense
            throw new NotSupportedException("DEC protocol does not support folder creation.");
        }

        public Task DeleteFolderAsync(Folder folder, CancellationToken cancellationToken = default)
        {
            // DEC protocol doesn't support folder deletion in the traditional sense
            throw new NotSupportedException("DEC protocol does not support folder deletion.");
        }

        public void Dispose() { }
    }
}
