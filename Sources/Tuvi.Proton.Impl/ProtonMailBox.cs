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
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Cryptography;
using MimeKit.Utils;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;
using Org.BouncyCastle.Utilities.IO;
using Tuvi.Auth.Proton.Exceptions;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using Tuvi.Core.Logging;
using Tuvi.Core.Mail;
using Tuvi.Proton.Client;
using Tuvi.Proton.Impl;
using TuviSRPLib;

[assembly: InternalsVisibleTo("Tuvi.Core.Mail.Tests")]
[assembly: InternalsVisibleTo("Tuvi.Core.Mail.Impl.Tests")]
namespace Tuvi.Proton
{
    public delegate Task<(bool completed, string code)> TwoFactorCodeProvider(Exception previousAttemptException, CancellationToken cancellationToken);
    public delegate Task<(bool completed, string password)> MailboxPasswordProvider(Exception previousAttemptException, CancellationToken cancellationToken);
    public delegate Task<(bool completed, string verificationType, string token)> HumanVerifier(Uri verifierUrl, Exception previousAttemptException, CancellationToken cancellationToken);

    public static class ClientAuth
    {
        public static async Task<(string, string, string)> LoginFullAsync(string userName,
                                                                          string password,
                                                                          TwoFactorCodeProvider twoFactorCodeProvider,
                                                                          MailboxPasswordProvider mailboxPasswordProvider,
                                                                          HumanVerifier humanVerifier,
                                                                          CancellationToken cancellationToken)
        {
            Debug.Assert(twoFactorCodeProvider != null);
            Debug.Assert(mailboxPasswordProvider != null);

            using (var client = await Impl.Client.CreateWithLoginAsync(() => new HttpClient(),
                                                                       userName,
                                                                       password,
                                                                       twoFactorCodeProvider,
                                                                       humanVerifier,
                                                                       null,
                                                                       cancellationToken)
                                          .ConfigureAwait(false))
            {
                var userTask = client.GetUserAsync(cancellationToken);
                var saltsTask = client.GetSaltsAsync(cancellationToken);
                await Task.WhenAll(userTask, saltsTask).ConfigureAwait(false);

                var user = userTask.Result;
                var salts = saltsTask.Result;

                var primaryKey = user.Keys.Where(x => x.Primary > 0).First();
                var salt = salts.FirstOrDefault(x => x.ID == primaryKey.ID);

                string saltedKeyPassword = await UnlockAsync().ConfigureAwait(false);

                return (client.UserId, client.RefreshToken, saltedKeyPassword);

                async Task<string> UnlockAsync(Exception previousAttemptException = null)
                {
                    try
                    {
                        string mailboxPassword = password;

                        if (client.IsTwoPasswordMode)
                        {
                            (bool completed, string mailboxPass) = await mailboxPasswordProvider(previousAttemptException, cancellationToken).ConfigureAwait(false);
                            mailboxPassword = mailboxPass;

                            if (completed == false)
                            {
                                throw new OperationCanceledException();
                            }
                        }

                        byte[] saltedMailboxPassword = SaltForKey(salt.KeySalt ?? string.Empty, Encoding.ASCII.GetBytes(mailboxPassword));
                        string saltedKeyPass = Encoding.ASCII.GetString(saltedMailboxPassword);

                        UnlockMailbox(primaryKey.PrivateKey, saltedKeyPass);

                        return saltedKeyPass;
                    }
                    catch (AuthorizationException ex) when (client.IsTwoPasswordMode)
                    {
                        return await UnlockAsync(ex).ConfigureAwait(false);
                    }
                }
            }
        }

        private static byte[] SaltForKey(string salt, byte[] keyPass)
        {
            var decodedSalt = Base64.Decode(salt);
            var hashed = ProtonSRPUtilities.GetMailboxPassword(keyPass, decodedSalt);
            // Cut off last 31 bytes
            return hashed.AsSpan((hashed.Length - 31), 31).ToArray();
        }

        private static void UnlockMailbox(string armoredKey, string password)
        {
            try
            {
                using (ArmoredInputStream key = new ArmoredInputStream(new MemoryStream(Encoding.ASCII.GetBytes(armoredKey))))
                {
                    var pgpBundle = new PgpSecretKeyRingBundle(key);
                    foreach (var pgpKeyRing in pgpBundle.GetKeyRings())
                    {
                        foreach (var pgpKey in pgpKeyRing.GetSecretKeys())
                        {
                            pgpKey.ExtractPrivateKey(password.ToCharArray());
                        }
                    }
                }
            }
            catch (PgpException ex)
            {
                throw new AuthorizationException("Proton: invalid mailbox password", ex);
            }
        }
    }

    public static class MailBoxCreator
    {
        public static IMailBox Create(Account account, ICredentialsProvider credentialsProvider, IStorage storage)
        {
            return new ProtonMailBox(() => new HttpClient(), account, credentialsProvider, storage);

        }
        internal static IMailBox Create(Func<HttpClient> httpClientCreator, Account account, ICredentialsProvider credentialsProvider, IStorage storage)
        {
            return new ProtonMailBox(httpClientCreator, account, credentialsProvider, storage);
        }
    }

    #region Helpers

    static class Extensions
    {
        public static Folder ToFolder(this Impl.Label label)
        {
            var folder = new Folder();
            folder.FullName = label.Name;
            folder.Attributes = GetFolderAttributes(label.ID);
            return folder;
        }
        public static Message ToLocalMessage(this MessageMetadata metadata)
        {
            var message = new Message()
            {
                Subject = metadata.Subject,
                MessageId = metadata.ID,
                Unread = metadata.Unread > 0,
                IsFlagged = metadata.LabelIDs.Contains(LabelID.StarredLabel),
                // Flags = Flags,
                Time = DateTimeOffset.FromUnixTimeSeconds(metadata.Time),
                NumAttachments = metadata.NumAttachments,
                From = JoinAddress(metadata.SenderName, metadata.SenderAddress),
                To = JoinAddresses(metadata.ToList),
                Cc = JoinAddresses(metadata.CCList),
                Bcc = JoinAddresses(metadata.BCCList),
                LabelIds = metadata.LabelIDs.ToList()
            };
            return message;
        }

        public static Core.Entities.Message ToCoreMessage(this Message message)
        {
            var coreMessage = new Core.Entities.Message()
            {
                Id = (uint)message.Id,
                Subject = message.Subject,
                Date = message.Time,
                IsMarkedAsRead = !message.Unread,
                IsFlagged = message.IsFlagged
            };
            coreMessage.From.AddRange(SplitAddresses(message.From));
            coreMessage.To.AddRange(SplitAddresses(message.To));
            coreMessage.Cc.AddRange(SplitAddresses(message.Cc));
            coreMessage.Bcc.AddRange(SplitAddresses(message.Bcc));

            return coreMessage;
        }

        private static IList<EmailAddress> SplitAddresses(string list)
        {
            return list.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x =>
            {
                var p = x.Split(':');
                return new EmailAddress(p[1], p[0]);
            }).ToList();
        }
        private static FolderAttributes GetFolderAttributes(string id)
        {
            switch (id)
            {
                case LabelID.InboxLabel:
                    return FolderAttributes.Inbox;
                case LabelID.AllDraftsLabel:
                    return FolderAttributes.Draft;
                case LabelID.AllSentLabel:
                    return FolderAttributes.Sent;
                case LabelID.TrashLabel:
                    return FolderAttributes.Trash;
                case LabelID.SpamLabel:
                    return FolderAttributes.Junk;
                case LabelID.AllMailLabel:
                    return FolderAttributes.All;
                case LabelID.ArchiveLabel: // TODO: add Archive
                    return FolderAttributes.None;
                case LabelID.SentLabel:
                    return FolderAttributes.Sent;
                case LabelID.DraftsLabel:
                    return FolderAttributes.Draft;
                case LabelID.OutboxLabel:
                    return FolderAttributes.None;
                case LabelID.StarredLabel:
                    return FolderAttributes.Important; // TODO should be Flagged
                case LabelID.AllScheduledLabel:
                    return FolderAttributes.None;
                default:
                    return FolderAttributes.None;
            }
        }

        private static string JoinAddresses(IReadOnlyList<EmailAddress> list)
        {
            return string.Join(";", list.Select(x => JoinAddress(x.Name, x.Address)).ToArray());
        }

        private static string JoinAddress(string name, string address)
        {
            return string.Join(":", name, address);
        }
    }

    class MySecureRandom : SecureRandom
    {
        bool _initialCall = true;
        byte[] _bytes;
        public MySecureRandom(byte[] bytes) : base()
        {
            _bytes = bytes;
        }

        public override void NextBytes(byte[] buf)
        {
            if (_initialCall)
            {
                Debug.Assert(buf.Length == _bytes.Length);
                Array.Copy(_bytes, buf, buf.Length);
                _initialCall = false;
                return;
            }
            base.NextBytes(buf);
        }

    }

    internal class MyOpenPgpContext : GnuPGContext
    {
        private readonly IDictionary<long, string> _passwords = new Dictionary<long, string>();
        public static string InternalUserId = "internal_user_id@temporary.not_real_tld";
        public MyOpenPgpContext()
        {
            this.DefaultEncryptionAlgorithm = EncryptionAlgorithm.Aes256;
            // override default behavior
            SecretKeyRingBundle = new PgpSecretKeyRingBundle(Array.Empty<byte>());
            PublicKeyRingBundle = new PgpPublicKeyRingBundle(Array.Empty<byte>());
        }

        public void AddKeyPassword(long keyId, string password)
        {
            if (!_passwords.ContainsKey(keyId))
            {
                _passwords[keyId] = password;
            }
        }

        public string GetPassword(PgpSecretKey key)
        {
            return GetPasswordForKey(key);
        }

        public void AddKey(string armoredKey, string keyPass)
        {
            using (ArmoredInputStream keyIn = new ArmoredInputStream(
                new MemoryStream(Encoding.ASCII.GetBytes(armoredKey))))
            {
                var pgpBundle = new PgpSecretKeyRingBundle(keyIn);
                foreach (var pgpKeyRing in pgpBundle.GetKeyRings())
                {
                    foreach (var pgpKey in pgpKeyRing.GetSecretKeys())
                    {
                        AddKeyPassword(pgpKey.KeyId, keyPass);
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    Import(pgpBundle, default);
                }
                catch (Exception ex)
                {
                    this.Log().LogError(ex, "An error occurred while adding the key.");
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }
        }

        public void AddKeysToTemporalKeyRing(PgpPublicKey publicKey, PgpPrivateKey privateKey)
        {
            var random = new SecureRandom();
            var masterKeyGen = GeneratorUtilities.GetKeyPairGenerator("Ed25519");
            masterKeyGen.Init(new Ed25519KeyGenerationParameters(random));
            // temporary keypair for master key
            var masterKeyPair = new PgpKeyPair(PublicKeyAlgorithmTag.EdDsa, masterKeyGen.GenerateKeyPair(), privateKey.PublicKeyPacket.GetTime());
            var keyPair = new PgpKeyPair(publicKey, privateKey);

            var keyRingGen = new PgpKeyRingGenerator(PgpSignature.PositiveCertification,
                                                     masterKeyPair,
                                                     InternalUserId,
                                                     SymmetricKeyAlgorithmTag.Aes256,
                                                     "".ToCharArray(),
                                                     true,
                                                     null,
                                                     null,
                                                     new SecureRandom());
            keyRingGen.AddSubKey(keyPair);
            Import(keyRingGen.GenerateSecretKeyRing());
            Import(keyRingGen.GeneratePublicKeyRing());
        }

        protected override string GetPasswordForKey(PgpSecretKey key)
        {
            if (_passwords.TryGetValue(key.KeyId, out string password))
            {
                return password;
            }
            return "";
        }

        public override void Import(PgpSecretKeyRingBundle bundle, CancellationToken cancellationToken)
        {
            if (bundle is null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            foreach (PgpSecretKeyRing keyRing in bundle.GetKeyRings())
            {
                var constructorInfo = typeof(PgpPublicKeyRing).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(IList<PgpPublicKey>) }, null);
                var pubKeys = keyRing.GetPublicKeys().ToList();

                var publicKeyRing = (PgpPublicKeyRing)constructorInfo.Invoke(new[] { pubKeys });
                PublicKeyRingBundle = PgpPublicKeyRingBundle.AddPublicKeyRing(PublicKeyRingBundle, publicKeyRing);
                SecretKeyRingBundle = PgpSecretKeyRingBundle.AddSecretKeyRing(SecretKeyRingBundle, keyRing);
            }
        }
    }

    #endregion

    internal class ProtonMailBox : IMailBox
    {
        private readonly Func<HttpClient> _httpClientCreator;
        private readonly Account _account;
        private readonly ICredentialsProvider _credentialsProvider;
        private readonly IStorage _storage;
        private volatile Impl.Client _client;
        private MyOpenPgpContext _context;
        private SemaphoreSlim _clientMutex = new SemaphoreSlim(1, 1);
        private SemaphoreSlim _syncSemaphore = new SemaphoreSlim(1);
        private DateTime _lastSyncTime = DateTime.MinValue;
        private bool _isDisposed;
        private ConcurrentDictionary<string, string> _folderToLabelMap = new ConcurrentDictionary<string, string>();

        public ProtonMailBox(Func<HttpClient> httpClientCreator, Account account, ICredentialsProvider credentialsProvider, IStorage storage)
        {
            Debug.Assert(storage != null);
            _httpClientCreator = httpClientCreator;
            _account = account;
            _credentialsProvider = credentialsProvider;
            _storage = storage;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _context?.Dispose();
            _client?.Dispose();
            _clientMutex.Dispose();
            _syncSemaphore.Dispose();
            _isDisposed = true;
        }

        #region IMailBox interface implementation
        public bool HasFolderCounters => false;

        public async Task<Core.Entities.Message> AppendDraftMessageAsync(Core.Entities.Message message, CancellationToken cancellationToken)
        {
            var draftMessage = await CreateDraftAsync(message, cancellationToken).ConfigureAwait(false);
            var localMessage = draftMessage.ToLocalMessage();
            await _storage.AddOrUpdateMessagesAsync(message.Folder.AccountId, new List<Message>() { localMessage }, cancellationToken).ConfigureAwait(false);
            return localMessage.ToCoreMessage();
        }

        public async Task DeleteMessagesAsync(IReadOnlyList<uint> ids, Folder folder, bool permanentDelete, CancellationToken cancellationToken)
        {
            var labelId = await GetMessageLabelIdAsync(folder, cancellationToken).ConfigureAwait(false);

            if (folder.IsTrash)
            {
                permanentDelete = true;
            }

            var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
            var messages = await _storage.GetMessagesAsync(_account.Id, ids, cancellationToken).ConfigureAwait(false);

            if (permanentDelete)
            {
                await client.DeleteMessagesAsync(messages.Select(x => x.MessageId).ToList(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await client.LabelMessagesAsync(messages.Select(x => x.MessageId).ToList(), LabelID.TrashLabel, cancellationToken).ConfigureAwait(false);
            }

            await _storage.DeleteMessagesByIds(_account.Id, ids, labelId, cancellationToken).ConfigureAwait(false);
        }

        public async Task MoveMessagesAsync(IReadOnlyList<uint> ids, Folder folder, Folder targetFolder, CancellationToken cancellationToken)
        {
            var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
            var messages = await _storage.GetMessagesAsync(_account.Id, ids, cancellationToken).ConfigureAwait(false);

            var targetLabelId = await GetMessageLabelIdAsync(targetFolder, cancellationToken).ConfigureAwait(false);
            await client.LabelMessagesAsync(messages.Select(x => x.MessageId).ToList(), targetLabelId, cancellationToken).ConfigureAwait(false);
        }

        public async Task FlagMessagesAsync(IEnumerable<Core.Entities.Message> messages, CancellationToken cancellationToken)
        {
            var storedMessages = await _storage.GetMessagesAsync(_account.Id, messages.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);
            var messageIds = storedMessages.Select(x => x.MessageId).ToList();
            var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
            await client.LabelMessagesAsync(messageIds, LabelID.StarredLabel, cancellationToken).ConfigureAwait(false);
        }

        public async Task UnflagMessagesAsync(IEnumerable<Core.Entities.Message> messages, CancellationToken cancellationToken)
        {
            var storedMessages = await _storage.GetMessagesAsync(_account.Id, messages.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);
            var messageIds = storedMessages.Select(x => x.MessageId).ToList();
            var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
            await client.UnlabelMessagesAsync(messageIds, LabelID.StarredLabel, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Folder> GetDefaultInboxFolderAsync(CancellationToken cancellationToken)
        {
            var folders = await GetFoldersStructureAsync(cancellationToken).ConfigureAwait(false);
            return folders.First();
        }

        public Task<IReadOnlyList<Core.Entities.Message>> GetEarlierMessagesAsync(Folder folder,
                                                                                  int count,
                                                                                  Core.Entities.Message lastMessage,
                                                                                  CancellationToken cancellationToken)
        {
            return GetMessagesAsync(folder, count, lastMessage, true, updateLocal: false, cancellationToken);
        }

        public Task<IReadOnlyList<Core.Entities.Message>> GetEarlierMessagesForSynchronizationAsync(Folder folder,
                                                                                                    int count,
                                                                                                    Core.Entities.Message lastMessage,
                                                                                                    CancellationToken cancellationToken)
        {
            return GetMessagesAsync(folder, count, lastMessage, true, updateLocal: true, cancellationToken);
        }

        public async Task<IList<Folder>> GetFoldersStructureAsync(CancellationToken cancellationToken)
        {
            var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
            var labels = await client.GetLabelsAsync(new LabelType[] { LabelType.System, LabelType.Folder }, cancellationToken)
                                     .ConfigureAwait(false);
            // convert labels to folders
            var res = new List<Folder>(labels.Count);
            foreach (var label in labels)
            {
                if (label.Type == LabelType.System && !IsSupportedSystemLabel(label.ID))
                {
                    continue;// TODO: we ignore labels with the same name for a while, investigate
                }

                _folderToLabelMap.TryAdd(label.Name, label.ID);
                var folder = label.ToFolder();
                res.Add(folder);
            }
            return res;

            bool IsSupportedSystemLabel(string id)
            {
                switch (id)
                {
                    case LabelID.InboxLabel:
                    case LabelID.AllDraftsLabel:
                    case LabelID.AllSentLabel:
                    case LabelID.TrashLabel:
                    case LabelID.SpamLabel:
                    case LabelID.AllMailLabel:
                    case LabelID.ArchiveLabel:
                    case LabelID.SentLabel:
                    case LabelID.DraftsLabel:
                    case LabelID.OutboxLabel:
                    case LabelID.StarredLabel:
                    case LabelID.AllScheduledLabel:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public Task<IReadOnlyList<Core.Entities.Message>> GetLaterMessagesAsync(Folder folder,
                                                                                int count,
                                                                                Core.Entities.Message lastMessage,
                                                                                CancellationToken cancellationToken)
        {
            return GetMessagesAsync(folder, count, lastMessage, false, updateLocal: false, cancellationToken);
        }

        public async Task<Core.Entities.Message> GetMessageByIDAsync(Folder folder, uint id, CancellationToken cancellationToken)
        {
            var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);

            var labelId = await GetMessageLabelIdAsync(folder, cancellationToken).ConfigureAwait(false);
            var storedLastMessage = await _storage.GetMessageAsync(folder.AccountId, labelId, id, cancellationToken).ConfigureAwait(false);
            Debug.Assert(storedLastMessage != null);

            var messageData = await client.GetMessageAsync(storedLastMessage.MessageId, cancellationToken)
                                          .ConfigureAwait(false);

            //if (!storedLastMessage.IsEncrypted && !storedLastMessage.IsSigned)
            //{
            //    throw new CoreException("Proton: unexpected message");
            //}

            var message = storedLastMessage.ToCoreMessage();
            message.Folder = folder;
            // TODO: handle signature verification
            var decryptedBody = Crypto.DecryptArmored(_context, messageData.Body, verifySignature: false);

            bool replaceInlinedImages = false;

            switch (messageData.MIMEType)
            {
                case MIMETypes.TextPlain:
                    message.TextBody = decryptedBody;
                    break;
                case MIMETypes.TextHtml:
                    message.HtmlBody = decryptedBody;
                    replaceInlinedImages = true;
                    break;
                default:
                    throw new CoreException("Proton: unsupported body MIME");
            }

            foreach (var att in messageData.Attachments)
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    var stream = await client.GetAttachmentAsync(att.ID, cancellationToken).ConfigureAwait(false);
                    using (var decrypted = Crypto.DecryptAttachment(_context, att.KeyPackets, stream))// TODO: handle signature verification
                    {
                        if (att.Disposition == ContentDisposition.Attachment)
                        {
                            var attachment = new Core.Entities.Attachment
                            {
                                FileName = att.Name
                            };
                            attachment.Data = decrypted.ToArray();
                            message.Attachments.Add(attachment);
                        }
                        else if (att.MIMEType == "image/png" && replaceInlinedImages)
                        {
                            string contentId;
                            if (!att.Headers.TryGetValue("content-id", out contentId))
                            {
                                continue;
                            }
                            contentId = "cid:" + contentId.TrimStart('<').TrimEnd('>');
                            var buffer = decrypted.GetBuffer();
                            var length = (int)decrypted.Length;
                            var base64 = Convert.ToBase64String(buffer, 0, length);

                            var data = $"data:{att.MIMEType};base64,{base64}";

                            message.HtmlBody = message.HtmlBody.Replace(contentId, data);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Log().LogError(ex, "An error occurred while processing the attachments.");
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            //response.Message.Attachments.Select(x=>LoadAttachmentAsync(x.ID, cancellationToken)).

            //if (storedLastMessage.IsEncrypted)
            //{
            //    if (storedLastMessage.IsSigned)
            //    {
            //        message.Protection.Type = MessageProtectionType.SignatureAndEncryption;
            //    }
            //    else
            //    {
            //        message.Protection.Type = MessageProtectionType.Encryption;
            //    }
            //}
            //else
            //{
            message.Protection.Type = MessageProtectionType.None;
            //}

            return message;
        }

        public Task<Core.Entities.Message> GetMessageByIDHighPriorityAsync(Folder folder, uint id, CancellationToken cancellationToken)
        {
            // TODO: implement high priority
            return GetMessageByIDAsync(folder, id, cancellationToken);
        }

        public Task<IReadOnlyList<Core.Entities.Message>> GetMessagesAsync(Folder folder, int count, CancellationToken cancellationToken)
        {
            return GetMessagesAsync(folder, count, null, true, updateLocal: false, cancellationToken);
        }

        public Task MarkMessagesAsReadAsync(IEnumerable<Core.Entities.Message> messages, CancellationToken cancellationToken)
        {
            return ProcessMessagesAsync(messages, (c, ids) => { return c.MarkMessagesAsReadAsync(ids, cancellationToken); }, cancellationToken);
        }

        public Task MarkMessagesAsUnReadAsync(IEnumerable<Core.Entities.Message> messages, CancellationToken cancellationToken)
        {
            return ProcessMessagesAsync(messages, (c, ids) => { return c.MarkMessagesAsUnreadAsync(ids, cancellationToken); }, cancellationToken);
        }

        public async Task<Core.Entities.Message> ReplaceDraftMessageAsync(uint id, Core.Entities.Message message, CancellationToken cancellationToken)
        {
            var storedMessage = await _storage.GetMessageAsync(message.Folder.AccountId, LabelID.DraftsLabel, id, cancellationToken).ConfigureAwait(false);
            Debug.Assert(storedMessage != null);

            var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
            var req = new UpdateDraftReq()
            {
                Message = CreateDraftTemplate(message)
            };
            await client.UpdateDraftAsync(storedMessage.MessageId, req, cancellationToken).ConfigureAwait(false);
            return message.ShallowCopy();
        }

        public async Task SendMessageAsync(Core.Entities.Message message, CancellationToken cancellationToken)
        {
            var draftMessage = await CreateDraftAsync(message, cancellationToken).ConfigureAwait(false);
            var attachmentKeys = await UploadAttachmentsAsync(draftMessage.ID, message, cancellationToken).ConfigureAwait(false);
            await SendDraftAsync(message, draftMessage, attachmentKeys, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Helper methods

        private async Task<IDictionary<string, DecrypedSessionKey>> UploadAttachmentsAsync(string messageId,
                                                                                           Core.Entities.Message message,
                                                                                           CancellationToken cancellationToken)
        {
            Debug.Assert(_context != null);
            var res = new Dictionary<string, DecrypedSessionKey>();
            var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
            var signer = message.From.First();
            foreach (var attachment in message.Attachments)
            {
                var (decSessionKey, encSessionKey, encBody) = Crypto.EncryptAttachment(_context, signer, attachment.Data);
                var detachedSignature = Crypto.SignDetached(_context, signer, attachment.Data);

                var req = new CreateAttachmentReq
                {
                    MessageID = messageId,
                    Filename = attachment.FileName,
                    MIMEType = MimeTypes.GetMimeType(attachment.FileName),
                    Disposition = Dispositions.Attachment,
                    ContentID = "",
                    EncKey = encSessionKey,
                    EncBody = encBody,
                    Signature = detachedSignature
                };
                var att = await client.UploadAttachmentAsync(req, cancellationToken).ConfigureAwait(false);
                res.Add(att.ID, decSessionKey);
            }
            return res;
        }

        private Task<IReadOnlyList<Core.Entities.Message>> GetMessagesAsync(Folder folder,
                                                                            int count,
                                                                            Core.Entities.Message lastMessage,
                                                                            bool earlier,
                                                                            bool updateLocal,
                                                                            CancellationToken cancellationToken)
        {
            Debug.Assert(folder != null);
            return TranslateExceptions<IReadOnlyList<Core.Entities.Message>>(async () =>
            {
                await SynchronizedMessagesAsync(cancellationToken).ConfigureAwait(false);
                await GetFoldersStructureAsync(cancellationToken).ConfigureAwait(false);
                string labelId = await GetMessageLabelIdAsync(folder, cancellationToken).ConfigureAwait(false);
                uint knownMessageId = lastMessage != null ? lastMessage.Id : 0;

                var storedItems = await _storage.GetMessagesAsync(folder.AccountId, labelId, knownMessageId, earlier, count, cancellationToken)
                                                .ConfigureAwait(false);

                return storedItems.Select(x => x.ToCoreMessage()).ToList();
            });
        }

        private async Task<T> TranslateExceptions<T>(Func<Task<T>> func)
        {
            try
            {
                return await func().ConfigureAwait(false);
            }
            catch (RestClient.Exceptions.ConnectionException ex)
            {
                throw new ConnectionException("Proton: Failed to connect", ex);
            }
            catch (AuthProtonException ex)
            {
                throw new Core.Entities.AuthenticationException(this._account.Email, ex.Message, ex);
            }
            catch (Core.Entities.AuthenticationException ex)
            {
                throw new Core.Entities.AuthenticationException(this._account.Email, ex.Message, ex);
            }
            catch (Core.Entities.AuthorizationException ex)
            {
                throw new Core.Entities.AuthorizationException(this._account.Email, ex.Message, ex);
            }
        }

        private async Task SynchronizedMessagesAsync(CancellationToken cancellationToken)
        {
            const int SyncPageSize = 150;
            const int SyncIntervalSeconds = 20;

            await _syncSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                TimeSpan timeSpan = DateTime.Now - _lastSyncTime;
                if (timeSpan < TimeSpan.FromSeconds(SyncIntervalSeconds))
                {
                    // not syncing more than once every 20 seconds
                    return;
                }
                var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
                var remoteIds = await client.GetMessageIDsAsync("", cancellationToken)
                                            .ConfigureAwait(false);

                var storedlds = await _storage.LoadMessageIDsAsync(_account.Id, cancellationToken).ConfigureAwait(false);
                var localIds = storedlds.Select(x => x.Key).ToList();
                // should be stored in the same order
                var deletedIds = localIds.Except(remoteIds).ToList();
                await _storage.DeleteMessageByMessageIdsAsync(_account.Id, deletedIds, cancellationToken).ConfigureAwait(false);
                var newIds = remoteIds.Except(localIds).ToList();
                if (newIds.Count > 0)
                {
                    await _storage.AddMessageIDs(_account.Id, newIds, cancellationToken).ConfigureAwait(false);
                }

                var anyMessageFilter = new MessageFilter();
                var tasks = new List<Task>();
                for (long i = 0, page = 0; i < remoteIds.Count; page++, i += SyncPageSize)
                {
                    tasks.Add(SynchronizeMessagePageAsync((int)page));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
                _lastSyncTime = DateTime.Now;

                async Task SynchronizeMessagePageAsync(int page)
                {
                    var messages = await client.GetMessageMetadataPagedAsync(page, SyncPageSize, anyMessageFilter, cancellationToken)
                                               .ConfigureAwait(false);
                    await _storage.AddOrUpdateMessagesAsync(_account.Id, messages.Select(x => x.ToLocalMessage()).ToList(), cancellationToken)
                                  .ConfigureAwait(false);
                }
            }
            finally
            {
                _syncSemaphore.Release();
            }
        }

        private async Task<Impl.Message> CreateDraftAsync(Core.Entities.Message message, CancellationToken cancellationToken)
        {
            var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
            var req = new CreateDraftReq()
            {
                Message = CreateDraftTemplate(message),
                Action = CreateDraftAction.ForwardAction
            };
            return await client.CreateDraftAsync(req, cancellationToken).ConfigureAwait(false);
        }

        private DraftTemplate CreateDraftTemplate(Core.Entities.Message message)
        {
            Debug.Assert(_context != null);
            var sender = message.From.First();
            var body = Canonicalize(message.HtmlBody ?? message.TextBody);
            var MIMEType = message.HtmlBody is null ? MIMETypes.TextPlain : MIMETypes.TextHtml;

            var encryptedBody = Crypto.EncryptAndSignArmored(_context, sender, Encoding.UTF8.GetBytes(body));
            return new DraftTemplate()
            {
                Subject = message.Subject,
                Sender = _account.Email,
                ToList = message.To.Where(x => !x.IsDecentralized).ToList(),
                CCList = message.Cc.Where(x => !x.IsDecentralized).ToList(),
                BCCList = message.Bcc.Where(x => !x.IsDecentralized).ToList(),
                MIMEType = MIMEType,
                Body = Encoding.UTF8.GetString(Streams.ReadAll(encryptedBody)),
                ExternalID = MimeUtils.GenerateMessageId()

            };
        }

        private static string Canonicalize(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        }

        private async Task SendDraftAsync(Core.Entities.Message message,
                                          Impl.Message draft,
                                          IDictionary<string, DecrypedSessionKey> attachmentKeys,
                                          CancellationToken cancellationToken)
        {
            var sender = message.From.First();

            var body = Canonicalize(message.HtmlBody ?? message.TextBody);

            (var decSessionKey, var encBody) = Crypto.EncryptAndSignSplit(_context, sender, Encoding.UTF8.GetBytes(body));

            var sendReq = new SendDraftReq()
            {
                Packages = new List<MessagePackage>()
                {
                    new MessagePackage()
                    {
                        MIMEType = draft.MIMEType,
                        Body = Convert.ToBase64String(encBody.ToArray()),
                        Addresses = new Dictionary<string, MessageRecipient>()
                    }
                }
            };
            var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
            var package = sendReq.Packages.FirstOrDefault();
            foreach (var recipient in message.AllRecipients.Where(x => !x.IsDecentralized))
            {
                (var keys, var recipientType) = await client.GetPublicKeysAsync(recipient.Address, cancellationToken)
                                                            .ConfigureAwait(false);

                if (recipientType == RecipientType.Internal)
                {
                    var publicKey = Crypto.ExtractPgpPublicKey(keys.First().PublicKeyProp);
                    package.Addresses[recipient.Address] = new MessageRecipient()
                    {
                        Type = EncryptionScheme.InternalScheme,
                        Signature = SignatureType.DetachedSignature,
                        BodyKeyPacket = Convert.ToBase64String(Crypto.EncryptSessionKey(publicKey, decSessionKey)),
                        AttachmentKeyPackets = GetAttachmentKeyPackets(publicKey, attachmentKeys)
                    };
                    package.Type |= EncryptionScheme.InternalScheme;
                }
                else
                {
                    package.Addresses[recipient.Address] = new MessageRecipient()
                    {
                        Type = EncryptionScheme.ClearScheme,
                        Signature = SignatureType.NoSignature,
                    };
                    package.BodyKey = NewSessionKey(decSessionKey);

                    package.AttachmentKeys = new Dictionary<string, SessionKey>();
                    foreach (var pair in attachmentKeys)
                    {
                        package.AttachmentKeys[pair.Key] = NewSessionKey(pair.Value);
                    }
                    package.Type |= EncryptionScheme.ClearScheme;
                }
            }
            await client.SendDraftAsync(draft.ID, sendReq, cancellationToken)
                        .ConfigureAwait(false);

        }

        private static IDictionary<string, string> GetAttachmentKeyPackets(PgpPublicKey publicKey,
                                                                           IDictionary<string, DecrypedSessionKey> attachmentKeys)
        {
            var res = new Dictionary<string, string>();
            foreach (var attachmentKey in attachmentKeys)
            {
                res[attachmentKey.Key] = Convert.ToBase64String(Crypto.EncryptSessionKey(publicKey, attachmentKey.Value));
            }
            return res;
        }

        private static SessionKey NewSessionKey(DecrypedSessionKey decryptedSessionKey)
        {
            return new SessionKey()
            {
                Key = Convert.ToBase64String(decryptedSessionKey.Key),
                Algorithm = decryptedSessionKey.Algo.ToString().ToLower(CultureInfo.CurrentCulture)
            };
        }

        private async Task ProcessMessagesAsync(IEnumerable<Core.Entities.Message> messages, Func<Impl.Client, IEnumerable<string>, Task> action, CancellationToken cancellationToken)
        {
            var tasks = messages.Select(x => GetMessageAsync(x)).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);
            var storedMessageIDs = tasks.Where(x => x.Status == TaskStatus.RanToCompletion).Select(x => x.Result.MessageId).ToList();
            var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
            await action(client, storedMessageIDs).ConfigureAwait(false);

            async Task<Message> GetMessageAsync(Core.Entities.Message message)
            {
                var labelID = await GetMessageLabelIdAsync(message.Folder, cancellationToken).ConfigureAwait(false);
                return await _storage.GetMessageAsync(message.Folder.AccountId, labelID, message.Id, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<string> GetMessageLabelIdAsync(Folder folder, CancellationToken cancellationToken)
        {
            if (_folderToLabelMap is null || _folderToLabelMap.IsEmpty)
            {
                await GetFoldersStructureAsync(cancellationToken).ConfigureAwait(false);
            }

            Debug.Assert(folder != null);

            string labelId;
            if (!_folderToLabelMap.TryGetValue(folder.FullName, out labelId))
            {
                throw new CoreException("Proton: Failed to get labelID for folder");
            }

            return labelId;
        }

        private async Task<Impl.Client> GetClientAsync(CancellationToken cancellationToken)
        {
            if (_client != null)
            {
                return _client;
            }

            await _clientMutex.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
#pragma warning disable CA1508 // Avoid dead conditional code
                if (_client != null) // double check
                {
                    return _client;
                }
#pragma warning restore CA1508 // Avoid dead conditional code
                var dataStorage = _storage as IDataStorage;

                try
                {
                    // Update credentials if account already exists in storage
                    var account = await dataStorage.GetAccountAsync(_account.Email, cancellationToken).ConfigureAwait(false);
                    _account.AuthData = account.AuthData;
                }
                catch (AccountIsNotExistInDatabaseException)
                {
                }
                var authData = _account.AuthData as ProtonAuthData;
                if (authData is null)
                {
                    throw new AuthenticationException("Proton: there is no authentication data");
                }

                var client = await Impl.Client.CreateFromRefreshAsync(_httpClientCreator, authData.UserId, authData.RefreshToken, OnRefreshAsync, cancellationToken)
                                              .ConfigureAwait(false);

                try
                {
                    _context = await GetCryptoContextAsync(client, _account, cancellationToken).ConfigureAwait(false);
                    _client = client;
                    client = null;
                }
                finally
                {
#pragma warning disable CA1508 // Avoid dead conditional code
                    client?.Dispose();
#pragma warning restore CA1508 // Avoid dead conditional code
                }

            }
            catch (RestClient.Exceptions.ConnectionException ex)
            {
                throw new ConnectionException("Proton: Failed to connect", ex);
            }
            finally
            {
                _clientMutex.Release();
            }

            return _client;
        }

        private async Task OnRefreshAsync(Session session, CancellationToken cancellationToken)
        {
            try
            {
                var dataStorage = _storage as IDataStorage;

                if (_account.AuthData is ProtonAuthData authData)
                {
                    authData.RefreshToken = session.RefreshToken;
                    authData.UserId = session.UserId;
                    await dataStorage.UpdateAccountAuthAsync(_account, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (AccountIsNotExistInDatabaseException)
            {
            }
        }

        private async Task<MyOpenPgpContext> GetCryptoContextAsync(Impl.Client client, Account account, CancellationToken cancellationToken)
        {
            var userTask = client.GetUserAsync(cancellationToken);
            var addressesTask = client.GetAddressesAsync(cancellationToken);
            await Task.WhenAll(userTask, addressesTask).ConfigureAwait(false);

            var user = userTask.Result;
            var addresses = addressesTask.Result;
            var saltedKeyPass = (account.AuthData as ProtonAuthData).SaltedPassword;

            var primaryKey = user.Keys.FirstOrDefault(x => x.Primary > 0);
            if (primaryKey.Equals(default(Key)))
            {
                throw new InvalidOperationException("Proton: No primary key found for the account.");
            }

            var context = new MyOpenPgpContext();
            context.AddKey(primaryKey.PrivateKey, saltedKeyPass);

            this.Log().LogDebug("Primary key was added to the crypto context.");

            try
            {
                foreach (var address in addresses)
                {
                    var key = address.Keys.FirstOrDefault(x => x.Active == 1);
                    if (key.Equals(default(Key)))
                    {
                        this.Log().LogWarning("No active key found for address. Skipping.");
                        continue;
                    }

                    context.AddKey(key.PrivateKey, Crypto.DecryptArmored(context, key.Token));

                    this.Log().LogDebug("Active address key added for address.");
                }
            }
            catch (System.UnauthorizedAccessException)
            {
                // invalid password
                throw new AuthorizationException("Proton: invalid mailbox password");
            }

            return context;
        }

        public Task<Folder> CreateFolderAsync(string folderName, CancellationToken cancellationToken = default)
        {
            // Proton Mail does not support folder creation
            throw new NotSupportedException("Proton Mail does not support folder creation.");
        }

        #endregion
    }
}
