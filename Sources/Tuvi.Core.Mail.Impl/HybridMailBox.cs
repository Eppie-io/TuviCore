using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Dec;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;
using Tuvi.Core.Mail.Impl.Protocols;

namespace Tuvi.Core.Mail.Impl
{
    internal class HybridMailBox : MailBox
    {
        private readonly IDataStorage DataStorage;

        private IDecStorageClient DecClient;

        public HybridMailBox(Account account, ICredentialsProvider credentialsProvider, IDataStorage dataStorage)
            : base(account, credentialsProvider)
        {
            DataStorage = dataStorage;
            DecClient = DecStorageBuilder.CreateAzureClient(new Uri("https://decsimulator.azurewebsites.net/api"));
        }

        private static uint Hash2Id(string hash)
        {
            // !WARNING: ugly hack
            // this temporary solution, we calc 32bit cityhash to store it as message ID
            // to not mix with mailkit messages
            var cityhash = System.Data.HashFunction.CityHash.CityHashFactory.Instance.Create();
            var val = cityhash.ComputeHash(Encoding.ASCII.GetBytes(hash));
            var hex = val.AsHexString();

            return Convert.ToUInt32(hex, 16);
        }

        public override async Task SendMessageAsync(Message message, CancellationToken cancellationToken)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.To.Any(x => !x.IsHybrid) && message.To.Any(x => x.IsHybrid))
            {
                throw new ArgumentException($"Message with mixed addresses not allowed.");
            }

            if (message.To.All(x => !x.IsHybrid))
            {
                await base.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                message.Date = DateTime.Now;

                var rawMessage = new DecMessageRaw(message);
                var data = JsonConvert.SerializeObject(rawMessage);

                using (var pgpContext = await EccPgpExtension.GetTemporalContextAsync(DataStorage).ConfigureAwait(false))
                {
                    foreach (var email in message.To)
                    {
                        var address = email.DecentralizedAddress;

                        var encryptedData = EccPgpExtension.Encrypt(pgpContext, address, data, cancellationToken);
                        var hash = await DecClient.SendAsync(address, encryptedData.ToArray()).ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(hash))
                        {
                            message.Id = Hash2Id(hash);

                            message.Folder = AccountSettings.SentFolder;
                            message.Date = DateTime.Now;
                            message.IsMarkedAsRead = true;
                            message.IsDecentralized = true;

                            await DataStorage.AddMessageAsync(AccountSettings.Email, message, updateUnreadAndTotal: true, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            throw new ArgumentException("Message not sent.");
                        }

                    }
                }
            }
        }

        public override async Task<IReadOnlyList<Message>> GetLaterMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken)
        {
            var newMessages = new List<Message>(await base.GetLaterMessagesAsync(folder, count, lastMessage, cancellationToken).ConfigureAwait(false));

            await GetMessagesListAsync(newMessages, folder, cancellationToken).ConfigureAwait(false);

            // !TODO: check the messages count here, it can't be more than requested

            return newMessages;
        }

        public override async Task<IReadOnlyList<Message>> GetMessagesAsync(Folder folder, int count, CancellationToken cancellationToken)
        {
            var newMessages = new List<Message>(await base.GetMessagesAsync(folder, count, cancellationToken).ConfigureAwait(false));

            await GetMessagesListAsync(newMessages, folder, cancellationToken).ConfigureAwait(false);

            // !TODO: check the messages count here, it can't be more than requested

            return newMessages;
        }

        private async Task GetMessagesListAsync(IList<Message> newMessages, Folder folder, CancellationToken cancellationToken)
        {
            // !WARNING: ugly hack
            // check for hybrid messages for only INBOX folder
            // A good solution here would be creating a separate folder just for decentralized messages,
            // this can help us avoid UIDs collision
            if (folder.IsInbox)
            {
                var pubkey = AccountSettings.Email.DecentralizedAddress;
                var list = await DecClient.ListAsync(pubkey).ConfigureAwait(false);

                foreach (var hash in list)
                {
                    var id = Hash2Id(hash);

                    bool exist = await DataStorage.IsMessageExistAsync(AccountSettings.Email, folder.FullName, id, cancellationToken).ConfigureAwait(false);

                    if (!exist)
                    {
                        var data = await DecClient.GetAsync(pubkey, hash).ConfigureAwait(false);

                        using (var pgpContext = await EccPgpExtension.GetTemporalContextAsync(DataStorage).ConfigureAwait(true))
                        {
                            var masterKey = await DataStorage.GetMasterKeyAsync(cancellationToken).ConfigureAwait(false);

                            var json = EccPgpExtension.Decrypt(pgpContext, masterKey, AccountSettings.Email.StandardAddress, AccountSettings.Email.StandardAddress, data, cancellationToken);
                            var message = JsonConvert.DeserializeObject<DecMessageRaw>(json).ToMessage();
                            message.Id = id;
                            message.Folder = folder;
                            newMessages.Add(message);
                        }
                    }
                }
            }
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
