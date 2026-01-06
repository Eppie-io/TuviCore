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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using Tuvi.Core.Logging;
using Tuvi.Core.Mail;

namespace Tuvi.Core.Impl
{
    public class AccountService : IAccountService
    {
        private readonly Account Account;
        private readonly IMailBox MailBox;
        private readonly IDataStorage DataStorage;
        private readonly IMessageProtector MessageProtector;

        public event EventHandler<MessageDeletedEventArgs> MessageDeleted;
        public event EventHandler<MessagesAttributeChangedEventArgs> MessagesIsReadChanged;
        public event EventHandler<MessagesAttributeChangedEventArgs> MessagesIsFlaggedChanged;
        public event EventHandler<UnreadMessagesReceivedEventArgs> UnreadMessagesReceived;
        public event EventHandler<FolderMessagesReceivedEventArgs> MessagesReceived;
        public AccountService(Account account, IDataStorage dataStorage, IMailBox mailBox, IMessageProtector messageProtector)
        {
            Account = account;
            MailBox = mailBox;
            DataStorage = dataStorage;
            MessageProtector = messageProtector;
        }

        public async Task<IReadOnlyList<Message>> ReceiveEarlierMessagesAsync(Folder folder, int count, CancellationToken cancellationToken)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }
            var earliestFolderMessage = await DataStorage.GetEarliestMessageAsync(folder, cancellationToken).ConfigureAwait(false);
            var receivedMessages = await GetEarlierRemoteMessagesAsync(folder, count, earliestFolderMessage, cancellationToken).ConfigureAwait(false);
            await AddMessagesToDataStorageOnSyncAsync(folder, receivedMessages, cancellationToken).ConfigureAwait(false);

            return receivedMessages;
        }

        public async Task SendMessageAsync(Message message, bool encrypt, bool sign, CancellationToken cancellationToken = default)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var messageCopy = message.ShallowCopy();
            if (encrypt && sign)
            {
                await MessageProtector.SignAndEncryptAsync(messageCopy, cancellationToken).ConfigureAwait(false);
            }
            else if (encrypt)
            {
                await MessageProtector.EncryptAsync(messageCopy, cancellationToken).ConfigureAwait(false);
            }
            else if (sign)
            {
                await MessageProtector.SignAsync(messageCopy, cancellationToken).ConfigureAwait(false);
            }

            messageCopy.Folder = Account.SentFolder;
            await MailBox.SendMessageAsync(messageCopy, cancellationToken).ConfigureAwait(false);
            // Here we intentionally delete original message
            await PermanentDeleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }

        public Task MarkMessageAsReadAsync(Message message, CancellationToken cancellationToken = default)
        {
            return UpdateMessagesReadFlagAsync(new List<Message> { message }, isRead: true, cancellationToken);
        }

        public Task MarkMessageAsUnReadAsync(Message message, CancellationToken cancellationToken = default)
        {
            return UpdateMessagesReadFlagAsync(new List<Message> { message }, isRead: false, cancellationToken);
        }

        public async Task MarkMessagesAsReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default)
        {
            await UpdateMessagesReadFlagAsync(messages, isRead: true, cancellationToken).ConfigureAwait(false);
        }

        public async Task MarkMessagesAsUnReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default)
        {
            await UpdateMessagesReadFlagAsync(messages, isRead: false, cancellationToken).ConfigureAwait(false);
        }

        private async Task UpdateMessagesReadFlagAsync(IEnumerable<Message> messages, bool isRead, CancellationToken cancellationToken)
        {
            if (messages is null)
            {
                throw new ArgumentNullException(nameof(messages));
            }
            foreach (var message in messages)
            {
                message.IsMarkedAsRead = isRead;
            }

            foreach (var messageGroup in messages.GroupBy(x => x.Folder))
            {
                Task remoteTask = isRead ? MailBox.MarkMessagesAsReadAsync(messageGroup, cancellationToken)
                                         : MailBox.MarkMessagesAsUnReadAsync(messageGroup, cancellationToken);
                Task localTask = UpdateLocalMessagesReadFlagAsync(messageGroup, cancellationToken);
                await Task.WhenAll(remoteTask, localTask).ConfigureAwait(false);
            }
        }

        private async Task UpdateLocalMessagesReadFlagAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            await DataStorage.UpdateMessagesFlagsAsync(Account.Email, messages, updateUnreadAndTotal: true, cancellationToken).ConfigureAwait(false);
            RaiseMessagesIsReadChanged(messages);
        }

        private void RaiseMessagesIsReadChanged(IEnumerable<Message> messages)
        {
            if (MessagesIsReadChanged is null)
            {
                return;
            }
            foreach (var messageGroup in messages.GroupBy(message => message.Folder))
            {
                MessagesIsReadChanged.Invoke(null, new MessagesAttributeChangedEventArgs(Account.Email,
                                                                                         messageGroup.Key,
                                                                                         messageGroup.Select(message => message).ToList()));
            }
        }

        public Task FlagMessageAsync(Message message, CancellationToken cancellationToken = default)
        {
            return UpdateMessagesFlaggedAsync(new List<Message>() { message }, isFlagged: true, cancellationToken);
        }

        public Task FlagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default)
        {
            return UpdateMessagesFlaggedAsync(messages, isFlagged: true, cancellationToken);
        }

        public Task UnflagMessageAsync(Message message, CancellationToken cancellationToken = default)
        {
            return UpdateMessagesFlaggedAsync(new List<Message>() { message }, isFlagged: false, cancellationToken);
        }

        public async Task UnflagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default)
        {
            await UpdateMessagesFlaggedAsync(messages, isFlagged: false, cancellationToken).ConfigureAwait(false);
        }

        private async Task UpdateMessagesFlaggedAsync(IEnumerable<Message> messages, bool isFlagged, CancellationToken cancellationToken)
        {
            if (messages is null)
            {
                throw new ArgumentNullException(nameof(messages));
            }
            foreach (var message in messages)
            {
                message.IsFlagged = isFlagged;
            }
            Task remoteTask = isFlagged ? MailBox.FlagMessagesAsync(messages, cancellationToken)
                                        : MailBox.UnflagMessagesAsync(messages, cancellationToken);
            Task localTask = UpdateMessagesFlagAsync(messages, cancellationToken);
            await Task.WhenAll(remoteTask, localTask).ConfigureAwait(false);
        }

        private async Task UpdateMessagesFlagAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            await DataStorage.UpdateMessagesFlagsAsync(Account.Email, messages, updateUnreadAndTotal: true, cancellationToken).ConfigureAwait(false);
            RaiseMessagesIsFlaggedChanged(messages);
        }

        private void RaiseMessagesIsFlaggedChanged(IEnumerable<Message> messages)
        {
            if (MessagesIsFlaggedChanged is null)
            {
                return;
            }

            foreach (var messageGroup in messages.GroupBy(message => message.Folder))
            {
                MessagesIsFlaggedChanged.Invoke(null, new MessagesAttributeChangedEventArgs(Account.Email,
                                                                                            messageGroup.Key,
                                                                                            messageGroup.Select(message => message).ToList()));
            }
        }

        public async Task<Message> GetMessageBodyHighPriorityAsync(Message message, CancellationToken cancellationToken = default)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Message fullMessage = null;
            if (message.IsNoBodyLoaded())
            {
                fullMessage = await MailBox.GetMessageByIDHighPriorityAsync(message.Folder, message.Id, cancellationToken).ConfigureAwait(false);
            }

            return await GetMessageBodyImplAsync(message, fullMessage, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Message> GetMessageBodyAsync(Message message, CancellationToken cancellationToken = default)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Message fullMessage = null;
            if (message.IsNoBodyLoaded())
            {
                fullMessage = await MailBox.GetMessageByIDAsync(message.Folder, message.Id, cancellationToken).ConfigureAwait(false);
            }

            return await GetMessageBodyImplAsync(message, fullMessage, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Message> GetMessageBodyImplAsync(Message message, Message fullMessage, CancellationToken cancellationToken = default)
        {
            message = await UpdateMessageAsync(message, fullMessage, cancellationToken).ConfigureAwait(false);

            await MessageProtector.TryVerifyAndDecryptAsync(message, cancellationToken).ConfigureAwait(false);
            await DataStorage.UpdateMessageAsync(Account.Email, message, updateUnreadAndTotal: true, cancellationToken).ConfigureAwait(false);

            return message;
        }

        private async Task<Message> UpdateMessageAsync(Message incompleteMessage, Message fullMessage, CancellationToken cancellationToken)
        {
            try
            {
                if (fullMessage != null)
                {
                    incompleteMessage.CopyInitialParameters(fullMessage);
                    await DataStorage.UpdateMessageAsync(Account.Email, fullMessage, updateUnreadAndTotal: true, cancellationToken).ConfigureAwait(false);

                    return fullMessage;
                }
                else
                {
                    return incompleteMessage;
                }
            }
            catch (MessageIsNotExistException)
            {
                await DeleteLocalMessageAsync(incompleteMessage.Folder,
                                              incompleteMessage,
                                              updateUnreadAndTotal: true,
                                              cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        // TODO: Move constant to settings
        // TVM-241
        private const int _loadBatchItemsCount = 20;

        public async Task<IReadOnlyList<Message>> ReceiveNewMessagesInFolderAsync(Folder folder, CancellationToken cancellationToken)
        {
            if (folder is null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            var lastMessage = await DataStorage.GetLatestMessageAsync(Account.Email, folder.FullName, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<Message> receivedMessages;
            var newMessages = new List<Message>();

            if (lastMessage != null)
            {
                receivedMessages = await MailBox.GetLaterMessagesAsync(folder, 0, lastMessage, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                receivedMessages = await MailBox.GetMessagesAsync(folder, _loadBatchItemsCount, cancellationToken).ConfigureAwait(false);
            }
            Debug.Assert(receivedMessages != null, "It should be a list or an exception should be thrown");

            foreach (var message in receivedMessages)
            {
                bool exist = await DataStorage.IsMessageExistAsync(Account.Email, folder.FullName, message.Id, cancellationToken).ConfigureAwait(false);
                if (!exist)
                {
                    newMessages.Add(message);
                }
            }

            if (newMessages.Count > 0)
            {
                await AddMessagesToDataStorageOnSyncAsync(folder, newMessages, cancellationToken).ConfigureAwait(false);
            }

            return newMessages;
        }

        private Task<IReadOnlyList<Message>> GetEarlierRemoteMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken)
        {
            return MailBox.GetEarlierMessagesAsync(folder, count, lastMessage, cancellationToken);
        }

        public Task DeleteMessageAsync(Message message, CancellationToken cancellationToken)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return DoDeleteMessageAsync(message, false, cancellationToken);
        }

        public Task DeleteMessagesAsync(Folder folder, IReadOnlyList<Message> messages, CancellationToken cancellationToken)
        {
            if (folder is null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            return DoDeleteMessagesAsync(folder, messages, false, cancellationToken);
        }

        public async Task MoveMessagesAsync(Folder folder, Folder targetFolder, IReadOnlyList<Message> messages, CancellationToken cancellationToken)
        {
            if (folder is null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (targetFolder is null)
            {
                throw new ArgumentNullException(nameof(targetFolder));
            }

            var uids = messages.Select(x => x.Id).Where(x => x > 0).ToList();
            if (uids.Count == 0)
            {
                // there is nothing to move, given messages were not stored anywhere
                return;
            }

            var remoteTask = MailBox.MoveMessagesAsync(uids, folder, targetFolder, cancellationToken);
            var localTask = DeleteLocalMessagesAsync(folder, uids, updateUnreadAndTotal: true, cancellationToken);
            await Task.WhenAll(localTask, remoteTask).ConfigureAwait(false);
        }

        public Task PermanentDeleteMessageAsync(Message message, CancellationToken cancellationToken)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return DoDeleteMessageAsync(message, true, cancellationToken);
        }

        private Task DoDeleteMessageAsync(Message message, bool permanentDelete, CancellationToken cancellationToken)
        {
            return DoDeleteMessagesAsync(message.Folder, new List<Message> { message }, permanentDelete, cancellationToken);
        }

        private async Task DoDeleteMessagesAsync(Folder folder, IReadOnlyList<Message> messages, bool permanentDelete, CancellationToken cancellationToken)
        {
            var uids = messages.Select(x => x.Id).Where(x => x > 0).ToList();
            if (uids.Count == 0)
            {
                // there is nothing to delete, given messages were not stored anywhere
                return;
            }

            var remoteTask = MailBox.DeleteMessagesAsync(uids, folder, permanentDelete, cancellationToken);
            var localTask = DeleteLocalMessagesAsync(folder, uids, updateUnreadAndTotal: true, cancellationToken);
            await Task.WhenAll(localTask, remoteTask).ConfigureAwait(false);
        }

        private async Task DeleteLocalMessageAsync(Folder folder, Message message, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            await DeleteLocalMessagesAsync(folder, new List<Message>() { message }, updateUnreadAndTotal, cancellationToken).ConfigureAwait(false);
        }

        private Task DeleteLocalMessagesAsync(Folder folder, IReadOnlyList<Message> messages, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            var uids = messages.Select(x => x.Id).Where(x => x > 0);
            return DeleteLocalMessagesAsync(folder, uids, updateUnreadAndTotal, cancellationToken);
        }

        private async Task DeleteLocalMessagesAsync(Folder folder, IEnumerable<uint> uids, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            await DataStorage.DeleteMessagesAsync(Account.Email, folder.FullName, uids, updateUnreadAndTotal, cancellationToken).ConfigureAwait(false);

            if (MessageDeleted is null)
            {
                return;
            }
            foreach (var uid in uids)
            {
                RaiseMessageDeleted(folder, uid);
            }
        }

        private void RaiseMessageDeleted(Folder folder, uint uid)
        {
            this.Log().LogDebug("MessageDeleted {Folder}, {UID}", folder.FullName, uid);
            Debug.Assert(MessageDeleted != null);
            MessageDeleted.Invoke(null, new MessageDeletedEventArgs(Account.Email, folder, uid));
        }

        public async Task<Message> CreateDraftMessageAsync(Message message, CancellationToken cancellationToken)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var draftsFolder = Account.DraftFolder;
            if (draftsFolder is null)
            {
                this.Log().LogWarning("Drafts folder was not found, skipping the creation of a draft message.");
                return message;
            }

            message.Folder = draftsFolder;
            message = await MailBox.AppendDraftMessageAsync(message, cancellationToken).ConfigureAwait(false);
            message.Folder = draftsFolder; // could be reset
            await DataStorage.AddMessageAsync(Account.Email, message, updateUnreadAndTotal: true, cancellationToken).ConfigureAwait(false);

            return message;
        }

        public async Task<Message> UpdateDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken)
        {
            Debug.Assert(message != null);
            Debug.Assert(message.Id == id);
            var newMessage = await MailBox.ReplaceDraftMessageAsync(id, message, cancellationToken).ConfigureAwait(false);
            var existingMessage = await DataStorage.GetMessageAsync(Account.Email, message.Folder.FullName, id, false, cancellationToken).ConfigureAwait(false);
            newMessage.Pk = existingMessage.Pk;
            await DataStorage.UpdateMessageAsync(Account.Email, newMessage, updateUnreadAndTotal: true, cancellationToken).ConfigureAwait(false);

            return newMessage;
        }

        public Task AddMessagesToDataStorageAsync(Folder folder,
                                                  IReadOnlyList<Message> messageList,
                                                  CancellationToken cancellationToken)
        {
            if (folder is null)
            {
                throw new ArgumentNullException(nameof(folder));
            }
            return AddMessagesToDataStorageAsync(folder, messageList, updateUnreadAndTotal: true, cancellationToken);
        }

        private Task AddMessagesToDataStorageOnSyncAsync(Folder folder,
                                                         IReadOnlyList<Message> messageList,
                                                         CancellationToken cancellationToken)
        {
            return AddMessagesToDataStorageAsync(folder, messageList, updateUnreadAndTotal: !MailBox.HasFolderCounters, cancellationToken);
        }

        private async Task AddMessagesToDataStorageAsync(Folder folder,
                                                         IReadOnlyList<Message> messageList,
                                                         bool updateUnreadAndTotal,
                                                         CancellationToken cancellationToken)
        {
            await DataStorage.AddMessageListAsync(Account.Email,
                                                  folder.FullName,
                                                  messageList,
                                                  updateUnreadAndTotal,
                                                  cancellationToken).ConfigureAwait(false);

            if (messageList.Any())
            {
                MessagesReceived?.Invoke(null, new FolderMessagesReceivedEventArgs(Account.Email, folder, messageList));
            }

            if (messageList.Any(m => !m.IsMarkedAsRead))
            {
                UnreadMessagesReceived?.Invoke(null, new UnreadMessagesReceivedEventArgs(Account.Email, folder));
            }
        }

        public Task<int> GetUnreadMessagesCountInFolderAsync(Folder folder, CancellationToken cancellationToken)
        {
            if (folder is null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            return DataStorage.GetUnreadMessagesCountAsync(Account.Email, folder.FullName, cancellationToken);
        }

        public async Task UpdateFolderStructureAsync(CancellationToken cancellationToken)
        {
            if (!MailBox.HasFolderCounters)
            {
                return;
            }
            await TryToAccountUpdateFolderStructureAsync(Account, MailBox, cancellationToken).ConfigureAwait(false);
            await DataStorage.UpdateAccountFolderStructureAsync(Account, cancellationToken).ConfigureAwait(false);
        }

        public static async Task TryToAccountUpdateFolderStructureAsync(Account account, IMailBox mailBox, CancellationToken cancellationToken)
        {
            Debug.Assert(account != null);
            Debug.Assert(mailBox != null);
            var foldersStructure = await mailBox.GetFoldersStructureAsync(cancellationToken).ConfigureAwait(false);
            Debug.Assert(foldersStructure != null);
            account.FoldersStructure = new List<Folder>(foldersStructure);
            account.DefaultInboxFolder = await mailBox.GetDefaultInboxFolderAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task SynchronizeAsync(bool full, CancellationToken cancellationToken)
        {
            this.Log().LogDebug("Synchronization({Full}): {Email}", full, Account.Email.Address);
            var sw = new Stopwatch();
            sw.Start();
            try
            {
                foreach (var folder in Account.FoldersStructure)
                {
                    await SynchronizeFolderAsync(folder, full, cancellationToken).ConfigureAwait(false);
                }
                sw.Stop();
                this.Log().LogDebug("Synchronization({Full}): {Email} completed in {Time}", full, Account.Email.Address, sw.Elapsed);
            }
            catch
            {
                this.Log().LogDebug("Synchronization({Full}): {Email} interrupted by exception", full, Account.Email.Address);
                throw;
            }
        }

        private class MyFolderSynchronizer : FolderSynchronizer
        {
            private readonly Folder _folder;
            private readonly AccountService _accountService;
            private IDataStorage DataStorage => _accountService.DataStorage;
            private IMailBox MailBox => _accountService.MailBox;
            private Account Account => _accountService.Account;

            public MyFolderSynchronizer(AccountService accountService,
                                        Folder folder)
            {
                _accountService = accountService;
                _folder = folder;
            }
            protected override Task<IReadOnlyList<Message>> LoadLocalMessagesAsync(uint minUid,
                                                                                   uint maxUid,
                                                                                   CancellationToken cancellationToken)
            {
                return DataStorage.GetMessageListAsync(_folder.AccountEmail,
                                                       _folder.FullName,
                                                       (minUid, maxUid),
                                                       true,
                                                       cancellationToken);
            }

            protected override async Task<IReadOnlyList<Message>> LoadRemoteMessagesAsync(Message fromMessage,
                                                                                    int count,
                                                                                    CancellationToken cancellationToken)
            {
                Debug.Assert(count > 0);
                return (await MailBox.GetEarlierMessagesForSynchronizationAsync(_folder, count, fromMessage, cancellationToken).ConfigureAwait(false)).OrderByDescending(x => x.Id).ToList();
            }

            protected override async Task DeleteMessagesAsync(IReadOnlyList<Message> messages,
                                                              CancellationToken cancellationToken)
            {
                this.Log().LogDebug("Deleting messages {Count}", messages.Count);
                await _accountService.DeleteLocalMessagesAsync(_folder,
                                                               messages,
                                                               updateUnreadAndTotal: !MailBox.HasFolderCounters,
                                                               cancellationToken).ConfigureAwait(false);
            }
            protected override async Task UpdateMessagesAsync(IReadOnlyList<Message> messages,
                                                              CancellationToken cancellationToken)
            {
                this.Log().LogDebug("Updating messages {Count}", messages.Count);
                await DataStorage.UpdateMessagesFlagsAsync(Account.Email,
                                                           messages,
                                                           updateUnreadAndTotal: !MailBox.HasFolderCounters,
                                                           cancellationToken).ConfigureAwait(false);
                _accountService.RaiseMessagesIsFlaggedChanged(messages);
                _accountService.RaiseMessagesIsReadChanged(messages);
            }

            protected override async Task AddMessagesAsync(IReadOnlyList<Message> messages,
                                                           CancellationToken cancellationToken)
            {
                var sw = new Stopwatch();
                try
                {
                    Debug.Assert(messages.Count > 0);
                    sw.Start();
                    this.Log().LogDebug("Adding messages {Count}", messages.Count);
                    // divide into continuous intervals
                    var copy = new List<Message>(messages);
                    copy.Sort((left, right) => -left.Id.CompareTo(right.Id)); // descending
                    var intervals = new List<KeyValuePair<Message, int>>();
                    var start = new Message() { Id = copy[0].Id + 1 }; // we exclude this element
                    var next = start;
                    int count = 0;
                    foreach (var message in copy)
                    {
                        if (next.Id - message.Id == 1)
                        {
                            ++count;
                            next = message;
                            continue;
                        }
                        intervals.Add(new KeyValuePair<Message, int>(start, count));
                        start = new Message() { Id = message.Id + 1 };
                        count = 1;
                        next = message;
                    }
                    intervals.Add(new KeyValuePair<Message, int>(start, count));
                    intervals.Sort((left, right) => -left.Value.CompareTo(right.Value));

                    var updatedMessages = new List<Message>();
                    foreach (var interval in intervals)
                    {
                        //this.Log().LogDebug($"Receiving form ({interval.Key.Id}, {interval.Key.Id} +  {interval.Value}]...");
                        updatedMessages.AddRange(await MailBox.GetEarlierMessagesAsync(_folder,
                                                                                       interval.Value,
                                                                                       interval.Key,
                                                                                       cancellationToken).ConfigureAwait(false));
                        if (updatedMessages.Count > 1000)
                        {
                            await UpdateMessageListAsync(updatedMessages, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    await UpdateMessageListAsync(updatedMessages, cancellationToken).ConfigureAwait(false);
                    this.Log().LogDebug("{Count} updated messages stored", updatedMessages.Count);
                }
                catch
                {
                    this.Log().LogDebug("Storing interrupted, exception was thrown");
                    throw;
                }
                finally
                {
                    sw.Stop();
                    this.Log().LogDebug("Adding messages took {Time}", sw.Elapsed);
                }
            }

            private async Task UpdateMessageListAsync(List<Message> updatedMessages, CancellationToken cancellationToken)
            {
                this.Log().LogDebug("Storing {Count} updated messages...", updatedMessages.Count);
                await _accountService.AddMessagesToDataStorageOnSyncAsync(_folder,
                                                                          updatedMessages,
                                                                          cancellationToken).ConfigureAwait(false);
                this.Log().LogDebug("{Count} updated messages stored", updatedMessages.Count);
                updatedMessages.Clear();
            }
        }

        public async Task SynchronizeFolderAsync(Folder folder, bool full, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            this.Log().LogDebug("Sync({Full}) {Email} folder {Folder} started...", full, folder?.AccountEmail?.Address, folder?.FullName);
            var sw = new Stopwatch();
            sw.Start();
            try
            {
                Debug.Assert(folder != null);
                var latestMessage = await DataStorage.GetLatestMessageAsync(Account.Email,
                                                                            folder.FullName,
                                                                            cancellationToken).ConfigureAwait(false);
                var earliestMessage = await DataStorage.GetEarliestMessageAsync(Account.Email,
                                                                                folder.FullName,
                                                                                cancellationToken).ConfigureAwait(false);
                if (full && (earliestMessage is null || earliestMessage.Id > 1))
                {
                    earliestMessage = new Message() { Id = 1 };
                }

                if (latestMessage is null ||
                    earliestMessage is null ||
                    latestMessage.Id == 0 ||
                    earliestMessage.Id == 0 ||
                    latestMessage.Id < earliestMessage.Id)
                {
                    return;
                }

                var synchronizer = new MyFolderSynchronizer(this, folder);

                await synchronizer.SynchronizeAsync(earliestMessage,
                                                    latestMessage,
                                                    cancellationToken).ConfigureAwait(false);

            }
            catch (OperationCanceledException)
            {
                this.Log().LogDebug("Sync({Full}) {Email} folder {Folder} is interrupted", full, folder?.AccountEmail?.Address, folder?.FullName);
            }
            finally
            {
                sw.Stop();
                this.Log().LogDebug("Sync({Full}) {Email} folder {Folder} took {Time}", full, folder?.AccountEmail?.Address, folder?.FullName, sw.Elapsed);
            }
        }
    }

    public abstract class FolderSynchronizer
    {
        public int BatchSize { get; set; }
        protected FolderSynchronizer()
        {
            BatchSize = 10000;
        }

        public async Task SynchronizeAsync(Message earliestMessage,
                                           Message latestMessage,
                                           CancellationToken cancellationToken)
        {
            if (earliestMessage is null ||
                latestMessage is null)
            {
                // ignore, there is nothing to sync, borders should be defined
                return;
            }
            Debug.Assert(latestMessage.Id >= earliestMessage.Id);

            uint localMaxId = latestMessage.Id;
            Message latestRemoteMessage = new Message() { Id = latestMessage.Id < uint.MaxValue ? latestMessage.Id + 1 : latestMessage.Id };
            while (!cancellationToken.IsCancellationRequested)
            {
                IReadOnlyList<Message> remoteMessages;
                if (latestRemoteMessage.Id > earliestMessage.Id)
                {
                    uint approxCount = latestRemoteMessage.Id - earliestMessage.Id;
                    int countToLoad = (int)Math.Min(approxCount, (uint)BatchSize);
                    remoteMessages = await LoadRemoteMessagesAsync(latestRemoteMessage,
                                                                   countToLoad,
                                                                   cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    remoteMessages = new List<Message>();
                }

                uint remoteMinId = 0;
                uint remoteMaxId = 0;
                if (remoteMessages.Count > 0)
                {
                    remoteMinId = remoteMaxId = remoteMessages[0].Id;
                }
                latestRemoteMessage = null;
                foreach (var message in remoteMessages)
                {
                    remoteMaxId = Math.Max(remoteMaxId, message.Id);
                    remoteMinId = Math.Min(remoteMinId, message.Id);
                    if (remoteMinId == message.Id)
                    {
                        latestRemoteMessage = message;
                    }
                }

                uint localMinId = Math.Max(remoteMinId, earliestMessage.Id);
                var localMessages = await LoadLocalMessagesAsync(localMinId,
                                                                 localMaxId + 1,
                                                                 cancellationToken).ConfigureAwait(false);
                var addedMessages = new List<Message>();
                var updatedMessages = new List<Message>();
                var deletedMessages = new List<Message>();

                // Merging loop
                int iremote = 0;
                int ilocal = 0;
                while (iremote < remoteMessages.Count &&
                       remoteMessages[iremote].Id > localMaxId)
                {
                    ++iremote;
                }
                while (ilocal < localMessages.Count && iremote < remoteMessages.Count)
                {
                    var remoteMessage = remoteMessages[iremote];
                    var localMessage = localMessages[ilocal];
                    if (remoteMessage.Id == localMessage.Id)
                    {
                        if (localMessage.TryToUpdate(remoteMessage))
                        {
                            updatedMessages.Add(localMessage);
                        }
                        ++ilocal;
                        ++iremote;
                    }
                    else if (remoteMessage.Id > localMessage.Id)
                    {
                        addedMessages.Add(remoteMessage);
                        ++iremote;
                    }
                    else if (remoteMessage.Id < localMessage.Id)
                    {
                        deletedMessages.Add(localMessage);
                        ++ilocal;
                    }
                }
                while (ilocal < localMessages.Count)
                {
                    deletedMessages.Add(localMessages[ilocal]);
                    ++ilocal;
                }
                while (iremote < remoteMessages.Count &&
                       remoteMessages[iremote].Id >= localMinId)
                {
                    addedMessages.Add(remoteMessages[iremote]);
                    ++iremote;
                }
                localMaxId = localMinId - 1;

                // Commit changes and notify observers
                if (deletedMessages.Count > 0)
                {
                    await DeleteMessagesAsync(deletedMessages, cancellationToken).ConfigureAwait(false);
                }
                if (updatedMessages.Count > 0)
                {
                    await UpdateMessagesAsync(updatedMessages, cancellationToken).ConfigureAwait(false);
                }
                if (addedMessages.Count > 0)
                {
                    await AddMessagesAsync(addedMessages, cancellationToken).ConfigureAwait(false);
                }

                if (remoteMessages.Count == 0)
                {
                    break;
                }
            }
        }

        protected abstract Task<IReadOnlyList<Message>> LoadLocalMessagesAsync(uint minUid,
                                                                               uint maxUid,
                                                                               CancellationToken cancellationToken);
        protected abstract Task<IReadOnlyList<Message>> LoadRemoteMessagesAsync(Message fromMessage,
                                                                                int count,
                                                                                CancellationToken cancellationToken);
        protected abstract Task DeleteMessagesAsync(IReadOnlyList<Message> messages,
                                                    CancellationToken cancellationToken);
        protected abstract Task UpdateMessagesAsync(IReadOnlyList<Message> messages,
                                                    CancellationToken cancellationToken);
        protected abstract Task AddMessagesAsync(IReadOnlyList<Message> messages,
                                                 CancellationToken cancellationToken);
    }
}
