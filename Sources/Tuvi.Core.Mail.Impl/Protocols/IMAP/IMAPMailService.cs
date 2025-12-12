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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using Microsoft.Extensions.Logging;
using Tuvi.Core.Entities;
using Tuvi.Core.Logging;

namespace Tuvi.Core.Mail.Impl.Protocols.IMAP
{

    class IMAPLogger : IProtocolLogger
    {
        public IMAPLogger() { }
        public IAuthenticationSecretDetector AuthenticationSecretDetector { get; set; }

        public void Dispose()
        {

        }

        public void LogClient(byte[] buffer, int offset, int count)
        {
            this.Log().LogTrace("IMAP Client: {Data}", Encoding.ASCII.GetString(buffer, offset, count));
        }

        public void LogConnect(Uri uri)
        {
            this.Log().LogTrace("IMAP connect: {URI}", uri);
        }

        public void LogServer(byte[] buffer, int offset, int count)
        {
            this.Log().LogTrace("IMAP Server: {Data}", Encoding.ASCII.GetString(buffer, offset, count));
        }
    }


    class IMAPMailService : ReceiverService
    {
        private MailKit.Net.Imap.ImapClient ImapClient { get; }
        private readonly SemaphoreSlim _forceReconnectLock = new SemaphoreSlim(1);
        private static readonly TimeSpan NoOpTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan NoOpThrottleInterval = TimeSpan.FromSeconds(30);
        private DateTime _lastNoOpUtc = DateTime.MinValue;
        private readonly object _noOpTimeLock = new object();

        protected override Task ForceReconnectCoreAsync(CancellationToken cancellationToken)
        {
            return ForceReconnectAsync(cancellationToken);
        }

        private async Task ForceReconnectAsync(CancellationToken cancellationToken)
        {
            this.Log().LogDebug("ForceReconnectAsync started");

            await _forceReconnectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (ImapClient.IsConnected)
                {
                    try
                    {
                        await ImapClient.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
                    }
                    catch (MailKit.ServiceNotConnectedException ex)
                    {
                        this.Log().LogDebug(ex, "IMAP already disconnected before force reconnect");
                    }
                    catch (MailKit.Net.Imap.ImapProtocolException ex)
                    {
                        this.Log().LogDebug(ex, "IMAP protocol error on disconnect during force reconnect");
                    }
                    catch (MailKit.Net.Imap.ImapCommandException ex)
                    {
                        this.Log().LogDebug(ex, "IMAP command error on disconnect during force reconnect");
                    }
                    catch (System.IO.IOException ex)
                    {
                        this.Log().LogDebug(ex, "IMAP IO error on disconnect during force reconnect");
                    }
                    catch (ObjectDisposedException ex)
                    {
                        this.Log().LogDebug(ex, "IMAP client disposed before force reconnect");
                    }
                }
                await ConnectAsync(cancellationToken).ConfigureAwait(false);
                await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _forceReconnectLock.Release();
            }
        }

        public IMAPMailService(string serverAddress, int serverPort, ICredentialsProvider credentialsProvider)
            : base(serverAddress, serverPort, credentialsProvider)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            ImapClient = new MailKit.Net.Imap.ImapClient(new IMAPLogger());
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        // this constructor need for tests
        internal IMAPMailService(MailKit.Net.Imap.ImapClient client, string serverAddress, int serverPort, ICredentialsProvider credentialsProvider)
            : base(serverAddress, serverPort, credentialsProvider)
        {
            ImapClient = client;
        }

        protected override MailKit.MailService Service { get => ImapClient; }

        /// <summary>
        /// Perform a lightweight NOOP to proactively detect silently closed connections (idle timeout) and restore if needed.
        /// Minimal helper: used only where justified to reduce risk of excessive round-trips.
        /// </summary>
        private async Task EnsureConnectionAliveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ImapClient.IsConnected && ImapClient.IsAuthenticated)
            {
                lock (_noOpTimeLock)
                {
                    if ((DateTime.UtcNow - _lastNoOpUtc) < NoOpThrottleInterval)
                    {
                        return;
                    }
                }

                this.Log().LogDebug("EnsureConnectionAliveAsync: NOOP check starting");

                try
                {
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        cts.CancelAfter(NoOpTimeout);
                        await ImapClient.NoOpAsync(cts.Token).ConfigureAwait(false);
                    }

                    lock (_noOpTimeLock)
                    {
                        _lastNoOpUtc = DateTime.UtcNow;
                    }

                    this.Log().LogDebug("EnsureConnectionAliveAsync: NOOP check succeeded");
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    this.Log().LogWarning("NOOP timed out - treating connection as stale and restoring");
                    await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (System.IO.IOException ex)
                {
                    this.Log().LogWarning(ex, "NOOP failed with IO error - restoring connection");
                    await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (MailKit.Net.Imap.ImapProtocolException ex)
                {
                    this.Log().LogWarning(ex, "NOOP failed with protocol error - restoring connection");
                    await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (MailKit.Net.Imap.ImapCommandException ex)
                {
                    this.Log().LogWarning(ex, "NOOP failed with command error - restoring connection");
                    await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                this.Log().LogDebug("EnsureConnectionAliveAsync: Connection not alive, restoring");
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public override async Task<IList<Folder>> GetFoldersStructureAsync(CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            const int retryDelayMs = 1000;
            Exception lastError = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    var folders = await ImapClient.GetFoldersAsync(ImapClient.PersonalNamespaces[0], cancellationToken: cancellationToken).ConfigureAwait(false);
                    var result = new List<Folder>();

                    foreach (var folder in folders)
                    {
                        if (!folder.Exists)
                        {
                            continue;
                        }

                        try
                        {
                            await folder.StatusAsync(StatusItems.Unread | StatusItems.Count, cancellationToken).ConfigureAwait(false);
                            result.Add(folder.ToTuviMailFolder());
                        }
                        catch (System.IO.IOException ex)
                        {
                            this.Log().LogError(ex, "Failed to get status for folder: {FolderFullName}", folder.FullName);
                            throw;
                        }
                        catch (MailKit.Net.Imap.ImapProtocolException ex)
                        {
                            this.Log().LogError(ex, "Failed to get status for folder: {FolderFullName}", folder.FullName);
                            throw;
                        }
                        catch (MailKit.Net.Imap.ImapCommandException ex)
                        {
                            this.Log().LogError(ex, "Failed to get status for folder: {FolderFullName}", folder.FullName);
                            throw;
                        }
                    }

                    return result;
                }
                catch (System.IO.IOException ex)
                {
                    lastError = ex;
                    await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (MailKit.Net.Imap.ImapProtocolException ex)
                {
                    lastError = ex;
                    await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (MailKit.Net.Imap.ImapCommandException ex)
                {
                    lastError = ex;
                    await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                }

                if (attempt < maxAttempts)
                {
                    await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }

            if (lastError != null)
            {
                throw lastError;
            }

            throw new ConnectionException("Failed to retrieve IMAP folders structure after retries.");
        }

        public override Folder GetDefaultInboxFolder()
        {
            var folder = ImapClient.Inbox.ToTuviMailFolder();
            return folder;
        }

        public override async Task<IReadOnlyList<Message>> GetMessagesAsync(Folder folder, int count, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                return await DoGetMessagesAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoGetMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoGetMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoGetMessagesAsync().ConfigureAwait(false);
            }

            async Task<IReadOnlyList<Message>> DoGetMessagesAsync()
            {
                var mailFolder = await ImapClient.GetFolderAsync(folder.FullName, cancellationToken).ConfigureAwait(false);
                await mailFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

                if (mailFolder.Count == 0)
                {
                    await SafeCloseAsync(mailFolder, cancellationToken).ConfigureAwait(false);
                    return new List<Message>();
                }

                if (count > mailFolder.Count || count == 0)
                {
                    count = mailFolder.Count;
                }

                List<Message> messages = await FetchMessagesAsync(mailFolder,
                                                                  mailFolder.Count - count,
                                                                  mailFolder.Count - 1, // this range should include border, for zero-base indecies we should substract 1
                                                                  false,
                                                                  cancellationToken).ConfigureAwait(false);
                await SafeCloseAsync(mailFolder, cancellationToken).ConfigureAwait(false);

                return messages;
            }
        }

        internal async Task<List<Message>> FetchMessagesAsync(IMailFolder mailFolder, int startIndex, int endIndex, bool fast, CancellationToken cancellationToken)
        {
            try
            {
                var items = GetFetchFlags(fast);
                var messagesSymmarys = await mailFolder.FetchAsync(startIndex, endIndex,
                    items,
                    cancellationToken).ConfigureAwait(false);

                return new List<Message>(from message in messagesSymmarys select message.ToTuviMailMessage(mailFolder.ToTuviMailFolder()));

            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
            }

            mailFolder = await ImapClient.GetFolderAsync(mailFolder.FullName, cancellationToken).ConfigureAwait(false);
            await mailFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

            var messages = new List<Message>();

            var total = mailFolder.Count;
            if (total == 0)
            {
                return messages;
            }

            if (startIndex < 0)
            {
                startIndex = 0;
            }

            if (endIndex >= total)
            {
                endIndex = total - 1;
            }

            if (startIndex > endIndex)
            {
                return messages;
            }

            var messageSummaries = await mailFolder.FetchAsync(startIndex, endIndex, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags, cancellationToken).ConfigureAwait(false);
            foreach (var messageSummary in messageSummaries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var mimeMessage = await mailFolder.GetMessageAsync(messageSummary.UniqueId, cancellationToken).ConfigureAwait(false);
                    var message = mimeMessage.ToTuviMailMessage(mailFolder.ToTuviMailFolder(), cancellationToken);
                    message.Id = messageSummary.UniqueId.Id;
                    if (messageSummary.Flags.HasValue)
                    {
                        message.IsMarkedAsRead = messageSummary.Flags.Value.HasFlag(MessageFlags.Seen);
                        message.IsFlagged = messageSummary.Flags.Value.HasFlag(MessageFlags.Flagged);
                    }

                    messages.Add(message);
                }
                catch (MessageNotFoundException)
                {
                    this.Log().LogDebug("IMAP skipped disappeared message {Uid} in folder {Folder}", messageSummary.UniqueId.Id, mailFolder.FullName);
                    continue;
                }
            }

            return messages;
        }

        private static MessageSummaryItems GetFetchFlags(bool fast)
        {
            return fast ? MessageSummaryItems.Flags | MessageSummaryItems.UniqueId
                        : MessageSummaryItems.Full | MessageSummaryItems.PreviewText | MessageSummaryItems.UniqueId;
        }

        public override async Task<int> GetMessageCountAsync(Folder folder, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await DoGetMessageCountAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoGetMessageCountAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoGetMessageCountAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoGetMessageCountAsync().ConfigureAwait(false);
            }

            async Task<int> DoGetMessageCountAsync()
            {
                var mailFolder = await ImapClient.GetFolderAsync(folder.FullName, cancellationToken).ConfigureAwait(false);

                if (!mailFolder.Exists)
                {
                    throw new FolderIsNotExistException(folder.FullName);
                }

                await mailFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
                int count = mailFolder.Count;
                await SafeCloseAsync(mailFolder, cancellationToken).ConfigureAwait(false);

                return count;
            }
        }

        public override async Task<Message> GetMessageAsync(Folder folder, uint id, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                return await GetMessageAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await GetMessageAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await GetMessageAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await GetMessageAsync().ConfigureAwait(false);
            }

            async Task<Message> GetMessageAsync()
            {
                try
                {
                    var imapFolder = await ImapClient.GetFolderAsync(folder.FullName, cancellationToken).ConfigureAwait(false);
                    await imapFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
                    var mimeMessage = await imapFolder.GetMessageAsync(new UniqueId(id), cancellationToken).ConfigureAwait(false);
                    var message = mimeMessage?.ToTuviMailMessage(imapFolder.ToTuviMailFolder(), cancellationToken);

                    var messageSummaries = await imapFolder.FetchAsync(new List<UniqueId>() { new UniqueId(id) }, MessageSummaryItems.Flags, cancellationToken).ConfigureAwait(false);
                    if (message != null && messageSummaries.Count > 0)
                    {
                        var messageSummary = messageSummaries[0];
                        message.IsMarkedAsRead = messageSummary.Flags.Value.HasFlag(MessageFlags.Seen);
                        message.IsFlagged = messageSummary.Flags.Value.HasFlag(MessageFlags.Flagged);
                    }

                    await SafeCloseAsync(imapFolder, cancellationToken).ConfigureAwait(false);

                    return message;
                }
                catch (MessageNotFoundException)
                {
                    throw new MessageIsNotExistException();
                }
            }
        }

        public override async Task<IReadOnlyList<Message>> GetLaterMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await DoGetLaterMessagesAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoGetLaterMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoGetLaterMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoGetLaterMessagesAsync().ConfigureAwait(false);
            }

            async Task<IReadOnlyList<Message>> DoGetLaterMessagesAsync()
            {
                List<Message> messages = new List<Message>();

                var mailFolder = await ImapClient.GetFolderAsync(folder.FullName, cancellationToken).ConfigureAwait(false);
                await mailFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

                int startIndex;
                if (lastMessage == null)
                {
                    if (count > mailFolder.Count || count == 0)
                    {
                        count = mailFolder.Count;
                    }

                    startIndex = 0;
                }
                else
                {
                    // Try to find message with last loaded id
                    var uids = new List<UniqueId> { new UniqueId(lastMessage.Id) };
                    var messageSummary = await mailFolder.FetchAsync(uids, MessageSummaryItems.Fast, cancellationToken).ConfigureAwait(false);
                    if (messageSummary.Count > 0)
                    {
                        // Start with next message index
                        startIndex = messageSummary[0].Index + 1;
                    }
                    else
                    {
                        // If failed to find message by id, try to find messages with higher id
                        var uidsRange = new UniqueIdRange(new UniqueId(lastMessage.Id + 1), UniqueId.MaxValue);
                        messageSummary = await mailFolder.FetchAsync(uidsRange, MessageSummaryItems.Fast, cancellationToken).ConfigureAwait(false);

                        if (messageSummary.Count > 0)
                        {
                            // Start with first found message index
                            startIndex = messageSummary[0].Index;
                        }
                        else
                        {
                            // No more later messages were found
                            await SafeCloseAsync(mailFolder, cancellationToken).ConfigureAwait(false);
                            return messages;
                        }
                    }

                    var restCount = mailFolder.Count - startIndex;
                    if (count == 0 || count > restCount)
                    {
                        count = restCount;
                    }
                }

                var endIndex = startIndex + count - 1;

                if (endIndex >= startIndex)
                {
                    messages = await FetchMessagesAsync(mailFolder, startIndex, endIndex, false, cancellationToken).ConfigureAwait(false);
                }

                await SafeCloseAsync(mailFolder, cancellationToken).ConfigureAwait(false);

                return messages;
            }
        }

        public override async Task<IReadOnlyList<Message>> GetEarlierMessagesAsync(Folder folder, int count, Message lastMessage, bool fast, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                return await GetEarlierMessagesAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await GetEarlierMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await GetEarlierMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await GetEarlierMessagesAsync().ConfigureAwait(false);
            }

            async Task<IReadOnlyList<Message>> GetEarlierMessagesAsync()
            {
                List<Message> messages = new List<Message>();

                var mailFolder = await ImapClient.GetFolderAsync(folder.FullName, cancellationToken).ConfigureAwait(false);
                await mailFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

                int startIndex = 0, endIndex = 0;
                if (lastMessage == null)
                {
                    if (count > mailFolder.Count || count == 0)
                    {
                        count = mailFolder.Count;
                    }

                    endIndex = mailFolder.Count - 1;
                }
                else
                {
                    // Try to find message with last loaded id
                    var uids = new List<UniqueId>() { new UniqueId(lastMessage.Id) };
                    var messageSummary = await mailFolder.FetchAsync(uids, MessageSummaryItems.Fast, cancellationToken).ConfigureAwait(false);
                    if (messageSummary.Count > 0)
                    {
                        // End with previous message index
                        endIndex = messageSummary[0].Index - 1;

                        if (endIndex < UniqueId.MinValue.Id)
                        {
                            // No more earlier messages were found
                            await SafeCloseAsync(mailFolder, cancellationToken).ConfigureAwait(false);
                            return messages;
                        }
                    }
                    else
                    {
                        // If failed to find message by id, try to find messages with lower id
                        // start search with small steps, then increase
                        uint step = 10;
                        bool found = false;
                        for (uint i = lastMessage.Id - 1; i >= UniqueId.MinValue.Id;)
                        {
                            uint minId = i > step ? i - step : UniqueId.MinValue.Id;
                            var uidsRange = new UniqueIdRange(new UniqueId(Math.Max(UniqueId.MinValue.Id, i)),
                                                              new UniqueId(minId));
                            messageSummary = await mailFolder.FetchAsync(uidsRange,
                                                                         MessageSummaryItems.Fast,
                                                                         cancellationToken).ConfigureAwait(false);
                            if (messageSummary.Count > 0)
                            {
                                // End with last found message index
                                endIndex = messageSummary.Last().Index;
                                found = true;
                                break;
                            }

                            if (minId == UniqueId.MinValue.Id)
                            {
                                break;
                            }

                            i = minId - 1;

                            // increase step for the case we have sparse id space, like exponent search
                            step *= 2;
                        }

                        if (found == false)
                        {
                            // No more earlier messages were found
                            await SafeCloseAsync(mailFolder, cancellationToken).ConfigureAwait(false);
                            return messages;
                        }
                    }

                    var restCount = endIndex + 1;
                    if (count == 0 || count > restCount)
                    {
                        count = restCount;
                    }
                }

                startIndex = Math.Max(0, endIndex - count + 1);

                if (endIndex >= startIndex)
                {
                    messages = await FetchMessagesAsync(mailFolder, startIndex, endIndex, fast, cancellationToken).ConfigureAwait(false);
                }

                await SafeCloseAsync(mailFolder, cancellationToken).ConfigureAwait(false);

                return messages;
            }
        }

        public override async Task<IList<Message>> CheckNewMessagesAsync(Folder folder, DateTime dateTime, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await DoCheckNewMessagesAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoCheckNewMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoCheckNewMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoCheckNewMessagesAsync().ConfigureAwait(false);
            }

            async Task<IList<Message>> DoCheckNewMessagesAsync()
            {
                var mailFolder = await ImapClient.GetFolderAsync(folder.FullName, cancellationToken).ConfigureAwait(false);
                await mailFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

                var ids = await mailFolder.SearchAsync(MailKit.Search.SearchQuery.DeliveredAfter(dateTime), cancellationToken).ConfigureAwait(false);

                this.Log().LogDebug("CheckNewMessages: found {Found} messages in {Folder}", ids.Count, folder.FullName);

                var messagesSymmarys = await mailFolder.FetchAsync(ids, MessageSummaryItems.Full | MessageSummaryItems.PreviewText | MessageSummaryItems.UniqueId, cancellationToken).ConfigureAwait(false);

                List<Message> messages = new List<Message>();
                messages.AddRange(from message in messagesSymmarys select message.ToTuviMailMessage(mailFolder.ToTuviMailFolder()));
                await SafeCloseAsync(mailFolder, cancellationToken).ConfigureAwait(false);

                return messages;
            }
        }

        public override async Task AppendSentMessageAsync(Message message, string messageId, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await AppendMessageAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await AppendMessageAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await AppendMessageAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await AppendMessageAsync().ConfigureAwait(false);
            }

            async Task AppendMessageAsync()
            {
                var sentFolder = GetSentFolder();
                try
                {
                    await sentFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

                    try
                    {
                        var uids = await sentFolder
                            .SearchAsync(MailKit.Search.SearchQuery.HeaderContains("Message-Id", $"<{messageId}>"), cancellationToken)
                            .ConfigureAwait(false);

                        if (uids.Any())
                        {
                            return;
                        }
                    }
                    catch (MailKit.Net.Imap.ImapCommandException)
                    {
                        // Ignore exception if search is not supported
                    }

                    using (var mimeMessage = message.ToMimeMessage())
                    {
                        mimeMessage.MessageId = messageId;
                        await sentFolder.AppendAsync(mimeMessage, MessageFlags.Seen, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    if (sentFolder.IsOpen)
                    {
                        await SafeCloseAsync(sentFolder, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        private IMailFolder GetSentFolder()
        {
            return GetSpecialFolder(SpecialFolder.Sent, MailKit.FolderAttributes.Sent);
        }

        private IMailFolder GetTrashFolder()
        {
            return GetSpecialFolder(SpecialFolder.Trash, MailKit.FolderAttributes.Trash);
        }

        private IMailFolder GetDraftsFolder()
        {
            return GetSpecialFolder(SpecialFolder.Drafts, MailKit.FolderAttributes.Drafts);
        }

        private IMailFolder GetSpecialFolder(SpecialFolder specialFolder, MailKit.FolderAttributes attribute)
        {
            if ((ImapClient.Capabilities & (MailKit.Net.Imap.ImapCapabilities.SpecialUse | MailKit.Net.Imap.ImapCapabilities.XList)) != 0)
            {
                return ImapClient.GetFolder(specialFolder);
            }

            var personal = ImapClient.GetFolder(ImapClient.PersonalNamespaces[0]);
            return personal.GetSubfolders(false).FirstOrDefault(x => x.Attributes.HasFlag(attribute));
        }

        public override async Task<Message> AppendDraftMessageAsync(Message message, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                return await AppendDraftMessageAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await AppendDraftMessageAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await AppendDraftMessageAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await AppendDraftMessageAsync().ConfigureAwait(false);
            }

            async Task<Message> AppendDraftMessageAsync()
            {
                if (message.Folder == null)
                {
                    message.Folder = GetDraftsFolder().ToTuviMailFolder();
                }

                var draftsFolder = await ImapClient.GetFolderAsync(message.Folder.FullName, cancellationToken).ConfigureAwait(false);

                await draftsFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
                message.Folder = draftsFolder.ToTuviMailFolder();

                using (var mimeMessage = message.ToMimeMessage())
                {
                    message.Date = DateTimeOffset.UtcNow;
                    var uid = await draftsFolder.AppendAsync(mimeMessage, MessageFlags.Draft | MessageFlags.Seen, cancellationToken).ConfigureAwait(false);
                    message.Id = uid.Value.Id;
                    message.IsMarkedAsRead = true;

                    await SafeCloseAsync(draftsFolder, cancellationToken).ConfigureAwait(false);
                }

                return message;
            }
        }

        public override async Task<Message> ReplaceDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await DoReplaceDraftMessageAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoReplaceDraftMessageAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoReplaceDraftMessageAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                return await DoReplaceDraftMessageAsync().ConfigureAwait(false);
            }

            async Task<Message> DoReplaceDraftMessageAsync()
            {
                var draftsFolder = GetDraftsFolder();
                await draftsFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

                using (var mimeMessage = message.ToMimeMessage())
                {
                    message.Date = DateTimeOffset.UtcNow;
                    var uid = await draftsFolder.ReplaceAsync(new UniqueId(id), mimeMessage, MessageFlags.Draft | MessageFlags.Seen, cancellationToken).ConfigureAwait(false);
                    message.Id = uid.Value.Id;
                    message.IsMarkedAsRead = true;

                    await SafeCloseAsync(draftsFolder, cancellationToken).ConfigureAwait(false);
                }

                return message;
            }
        }

        public override async Task MarkMessagesAsReadAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await MarkMessagesAsReadAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await MarkMessagesAsReadAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await MarkMessagesAsReadAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await MarkMessagesAsReadAsync().ConfigureAwait(false);
            }

            Task MarkMessagesAsReadAsync()
            {
                return AddFlagsAsync(ids, folderPath, MessageFlags.Seen, cancellationToken);
            }
        }

        public override async Task MarkMessagesAsUnReadAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await MarkMessagesAsUnReadAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await MarkMessagesAsUnReadAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await MarkMessagesAsUnReadAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await MarkMessagesAsUnReadAsync().ConfigureAwait(false);
            }

            Task MarkMessagesAsUnReadAsync()
            {
                return RemoveFlagsAsync(ids, folderPath, MessageFlags.Seen, cancellationToken);
            }
        }

        public override async Task FlagMessagesAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await FlagMessagesAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await FlagMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await FlagMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await FlagMessagesAsync().ConfigureAwait(false);
            }

            Task FlagMessagesAsync()
            {
                return AddFlagsAsync(ids, folderPath, MessageFlags.Flagged, cancellationToken);
            }
        }

        public override async Task UnflagMessagesAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await UnflagMessagesAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await UnflagMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await UnflagMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await UnflagMessagesAsync().ConfigureAwait(false);
            }

            Task UnflagMessagesAsync()
            {
                return RemoveFlagsAsync(ids, folderPath, MessageFlags.Flagged, cancellationToken);
            }
        }

        private async Task AddFlagsAsync(IList<uint> ids, Folder folderPath, MessageFlags flags, CancellationToken cancellationToken)
        {
            var folder = await ImapClient.GetFolderAsync(folderPath.FullName, cancellationToken).ConfigureAwait(false);
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
            await folder.AddFlagsAsync(ids.Select(id => new UniqueId(id)).ToList(), flags, false, cancellationToken).ConfigureAwait(false);
            await SafeCloseAsync(folder, cancellationToken).ConfigureAwait(false);
        }

        private async Task RemoveFlagsAsync(IList<uint> ids, Folder folderPath, MessageFlags flags, CancellationToken cancellationToken)
        {
            var folder = await ImapClient.GetFolderAsync(folderPath.FullName, cancellationToken).ConfigureAwait(false);
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
            await folder.RemoveFlagsAsync(ids.Select(id => new UniqueId(id)).ToList(), flags, false, cancellationToken).ConfigureAwait(false);
            await SafeCloseAsync(folder, cancellationToken).ConfigureAwait(false);
        }

        public override async Task DeleteMessagesAsync(IReadOnlyList<uint> ids, Folder folderPath, bool permanentDelete, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await DeleteMessagesAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await DeleteMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await DeleteMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await DeleteMessagesAsync().ConfigureAwait(false);
            }

            async Task DeleteMessagesAsync()
            {
                var folder = await ImapClient.GetFolderAsync(folderPath.FullName, cancellationToken).ConfigureAwait(false);
                await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

                var uniqueIds = new List<uint>(ids).ConvertAll(x => new UniqueId(x));

                if (!folder.Attributes.HasFlag(MailKit.FolderAttributes.Trash) && !permanentDelete)
                {
                    try
                    {
                        var trash = GetTrashFolder();
                        if (trash != null)
                        {
                            await folder.MoveToAsync(uniqueIds, trash, cancellationToken).ConfigureAwait(false);
                            await SafeCloseAsync(folder, cancellationToken).ConfigureAwait(false);
                            return;
                        }
                    }
                    catch (NotSupportedException)
                    {
                    }
                }

                await folder.AddFlagsAsync(uniqueIds, MessageFlags.Deleted, true, cancellationToken).ConfigureAwait(false);
                await folder.ExpungeAsync(uniqueIds, cancellationToken).ConfigureAwait(false);
                await SafeCloseAsync(folder, cancellationToken).ConfigureAwait(false);
            }
        }

        public override async Task MoveMessagesAsync(IReadOnlyList<uint> ids, Folder folderPath, Folder targetFolderPath, CancellationToken cancellationToken)
        {
            await EnsureConnectionAliveAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await MoveMessagesAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await MoveMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await MoveMessagesAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                await MoveMessagesAsync().ConfigureAwait(false);
            }

            async Task MoveMessagesAsync()
            {
                var folder = await ImapClient.GetFolderAsync(folderPath.FullName, cancellationToken).ConfigureAwait(false);
                await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

                var targetFolder = await ImapClient.GetFolderAsync(targetFolderPath.FullName, cancellationToken).ConfigureAwait(false);

                var uniqueIds = new List<uint>(ids).ConvertAll(x => new UniqueId(x));


                try
                {
                    await folder.MoveToAsync(uniqueIds, targetFolder, cancellationToken).ConfigureAwait(false);
                    await SafeCloseAsync(folder, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (NotSupportedException)
                {
                }


                await folder.AddFlagsAsync(uniqueIds, MessageFlags.Deleted, true, cancellationToken).ConfigureAwait(false);
                await folder.ExpungeAsync(uniqueIds, cancellationToken).ConfigureAwait(false);
                await SafeCloseAsync(folder, cancellationToken).ConfigureAwait(false);
            }
        }

        public override void Dispose()
        {
            ImapClient?.Dispose();
            _forceReconnectLock.Dispose();
        }

        /// <summary>
        /// Safely close an IMAP folder. Swallows close-time network/protocol errors (data already fetched) and restores connection if needed.
        /// </summary>
        private async Task SafeCloseAsync(IMailFolder folder, CancellationToken cancellationToken)
        {
            if (folder is null)
            {
                return;
            }

            if (!folder.IsOpen)
            {
                return;
            }

            try
            {
                await folder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
            }
            catch (System.IO.IOException ex)
            {
                this.Log().LogWarning(ex, "IMAP folder close failed (IO)");
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException ex)
            {
                this.Log().LogWarning(ex, "IMAP folder close failed (Protocol)");
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapCommandException ex)
            {
                this.Log().LogWarning(ex, "IMAP folder close failed (Command)");
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                this.Log().LogWarning(ex, "IMAP folder close timeout");
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                this.Log().LogWarning(ex, "IMAP folder close canceled/timeout");
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
