using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl.SecurityManagement;
using Tuvi.Core.Mail;
using Tuvi.Core.Mail.Impl;

[assembly: InternalsVisibleTo("Tuvi.Core.Mail.Tests")]

// TODO: change namespace to Tuvi.Dec
namespace Tuvi.Core.Dec.Impl
{
    public static class MailBoxCreator
    {
        public static IMailBox Create(Account account, IDecStorage storage)
        {
            return new DecMailBox(account,
                                  storage,
                                  DecStorageBuilder.CreateAzureClient(new Uri("https://testnet2.eppie.io/api")),
                                  new PgpDecProtector(storage));
        }
    }

    internal class DecMailBox : IMailBox
    {
        private readonly Account AccountSettings;
        private readonly IEnumerable<IDecStorageClient> DecClients;
        private readonly IDecStorage Storage;
        private readonly IDecProtector Protector;

        public bool HasFolderCounters => false;

        public DecMailBox(Account accountData, IDecStorage storage, IDecStorageClient decClient, IDecProtector protector)
            : this(accountData, storage, new List<IDecStorageClient>() { decClient }, protector)
        {
        }

        public DecMailBox(Account accountData, IDecStorage storage, IEnumerable<IDecStorageClient> decClients, IDecProtector protector)
        {
            AccountSettings = accountData;
            Storage = storage;
            DecClients = decClients;
            Protector = protector;
        }

        private static string GetStringHashSha256(string input)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes(input));

                return StringHelper.BytesToHex(hash);
            }
        }

        public async Task SendMessageAsync(Message message, CancellationToken cancellationToken)
        {
            if (message.To.Exists(x => !StringHelper.IsDecentralizedEmail(x) && !x.IsHybrid))
            {
                throw new ArgumentException($"Message should contain only decentralized recipients.");
            }

            message.Date = DateTime.Now;
            var rawMessage = new DecMessageRaw(message);
            var data = JsonConvert.SerializeObject(rawMessage);
            string finalHash = "";

            foreach (var email in message.To)
            {
                var address = GetDecAddress(email);
                var encryptedData = await Protector.EncryptAsync(address, data, cancellationToken).ConfigureAwait(false);
                finalHash += await SendToDecClientsAsync(address, encryptedData, cancellationToken).ConfigureAwait(false);
            }

            message.IsMarkedAsRead = true;
            message.IsDecentralized = true;

            // save dec message to sent folder
            await Storage.AddDecMessageAsync(AccountSettings.Email, new DecMessage(GetStringHashSha256(finalHash), message), cancellationToken).ConfigureAwait(false);
        }

        private static string GetDecAddress(EmailAddress email)
        {
            return email.IsHybrid
                ? email.DecentralizedAddress
                : StringHelper.GetDecentralizedAddress(email);
        }

        public Task<IList<Folder>> GetFoldersStructureAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IList<Folder>>(new List<Folder>
            {
                GetInboxFolder(),
                new Folder("SENT", FolderAttributes.Sent),
                new Folder("DRAFT", FolderAttributes.Draft),
                new Folder("TRASH", FolderAttributes.Trash),
            });
        }
        public Task<Folder> GetDefaultInboxFolderAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(GetInboxFolder());
        }

        private static Folder GetInboxFolder()
        {
            return new Folder("INBOX", FolderAttributes.Inbox);
        }

        private async Task<List<Message>> GetMessageListAsync(Folder folder, int count, CancellationToken cancellationToken)
        {
            var email = AccountSettings.Email;
            if (folder.IsInbox)
            {
                var address = GetDecAddress(email);
                var list = await ListDecClientsMessagesAsync(address).ConfigureAwait(false);
                var trash = (await GetFoldersStructureAsync(cancellationToken).ConfigureAwait(false)).Where(f => f.IsTrash).FirstOrDefault();

                var tasks = list.Distinct().Select(hash => Task.Run(() => GetMessageListImplAsync(email, folder, trash, address, hash, cancellationToken))).ToList();
                await tasks.DoWithLogAsync<DecMailBox>().ConfigureAwait(false);
            }

            var items = await Storage.GetDecMessagesAsync(email, folder, count, cancellationToken).ConfigureAwait(false);
            return items.ConvertAll(x => x.ToMessage());
        }

        private async Task GetMessageListImplAsync(EmailAddress email, Folder folder, Folder trashFolder, string address, string hash, CancellationToken cancellationToken)
        {
            bool exists = await Storage.IsDecMessageExistsAsync(email, folder.FullName, hash, cancellationToken).ConfigureAwait(false)
            || (trashFolder != null && await Storage.IsDecMessageExistsAsync(email, trashFolder.FullName, hash, cancellationToken).ConfigureAwait(false));

            if (exists)
            {
                return;
            }
            var data = await GetDecClientsMessageAsync(address, hash).ConfigureAwait(false);
            var json = await Protector.DecryptAsync(AccountSettings.GetPgpUserIdentity(),
                                                    AccountSettings.GetPgpKeyTag(),
                                                    data,
                                                    cancellationToken).ConfigureAwait(false);
            var message = JsonConvert.DeserializeObject<DecMessageRaw>(json).ToMessage();
            message.Folder = folder;
            await Storage.AddDecMessageAsync(email, new DecMessage(hash, message), cancellationToken).ConfigureAwait(false);
        }


        private async Task<string> SendToDecClientsAsync(string address, byte[] data, CancellationToken cancellationToken)
        {
            var tasks = DecClients.Select(x => Task.Run(() => x.SendAsync(address, data))).ToList();
            await tasks.DoWithLogAsync<DecMailBox>().ConfigureAwait(false);
            return string.Concat(tasks.Where(x => x.Status == TaskStatus.RanToCompletion).Select(x => x.Result));
        }

        private async Task<IReadOnlyList<string>> ListDecClientsMessagesAsync(string address)
        {
            var tasks = DecClients.Select(x => Task.Run(() => x.ListAsync(address))).ToList();
            await tasks.DoWithLogAsync<DecMailBox>().ConfigureAwait(false);
            ConvertExceptions(tasks);
            return tasks.Where(x => x.Status == TaskStatus.RanToCompletion).SelectMany(x => x.Result).ToList();
        }

        private static void ConvertExceptions(IEnumerable<Task> tasks)
        {
            var connectionError = tasks.Where(x => x.Status == TaskStatus.Faulted)
                                       .SelectMany(x => x.Exception.Flatten().InnerExceptions).FirstOrDefault();
            if (connectionError != null && connectionError is HttpRequestException)
            {
                throw new ConnectionException();
            }
        }

        private async Task<byte[]> GetDecClientsMessageAsync(string address, string hash)
        {
            var tasks = DecClients.Select(x => Task.Run(() => x.GetAsync(address, hash))).ToList();
            await tasks.DoWithLogAsync<DecMailBox>().ConfigureAwait(false);
            // TODO: probably we should throw an exception here
            ConvertExceptions(tasks);
            return tasks.Where(x => x.Status == TaskStatus.RanToCompletion).Select(x => x.Result).FirstOrDefault();
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
            // TODO: make different loagic for message retrieving during synchronization
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
            var trash = AccountSettings.FoldersStructure.Find(f => f.IsTrash);

            decMessage.FolderName = trash.FullName;
            decMessage.FolderAttributes = trash.Attributes;

            await Storage.UpdateDecMessageAsync(AccountSettings.Email, decMessage, cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteMessagesAsync(IReadOnlyList<uint> ids, Folder folder, bool permanentDelete = false, CancellationToken cancellationToken = default)
        {
            foreach (var id in ids)
            {
                await DeleteMessageAsync(id, folder, permanentDelete, cancellationToken).ConfigureAwait(false);
            }
        }

        public Task MoveMessagesAsync(IReadOnlyList<uint> ids, Folder folder, Folder targetFolder, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
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

        public void Dispose() { }
    }
}
