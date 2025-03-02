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

        public IMAPMailService(string serverAddress, int serverPort)
            : base(serverAddress, serverPort)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            ImapClient = new MailKit.Net.Imap.ImapClient(new IMAPLogger());
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        // this constructor need for tests
        internal IMAPMailService(MailKit.Net.Imap.ImapClient client, string serverAddress, int serverPort)
            : base(serverAddress, serverPort)
        {
            ImapClient = client;
        }

        protected override MailKit.MailService Service { get => ImapClient; }

        public override async Task<IList<Folder>> GetFoldersStructureAsync(CancellationToken cancellationToken)
        {
            async Task<IList<Folder>> GetFoldersAsync()
            {
                var folders = await ImapClient.GetFoldersAsync(ImapClient.PersonalNamespaces[0], StatusItems.Unread | StatusItems.Count, cancellationToken: cancellationToken).ConfigureAwait(false);
                return new List<Folder>(from folder in folders where folder.Exists select folder.ToTuviMailFolder());
            }

            IList<Folder> foldersStructure = null;
            try
            {
                foldersStructure = await GetFoldersAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                // after exception connection may be lost
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                // retry once
                foldersStructure = await GetFoldersAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                // after exception connection may be lost
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                // retry once
                foldersStructure = await GetFoldersAsync().ConfigureAwait(false);
            }

            return foldersStructure;
        }

        public override Folder GetDefaultInboxFolder()
        {
            var folder = ImapClient.Inbox.ToTuviMailFolder();
            return folder;
        }

        public override async Task<IReadOnlyList<Message>> GetMessagesAsync(Folder folder, int count, CancellationToken cancellationToken)
        {
            var mailFolder = await ImapClient.GetFolderAsync(folder.FullName, cancellationToken).ConfigureAwait(false);
            await mailFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

            if (mailFolder.Count == 0)
            {
                await mailFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
                return new List<Message>();
            }

            if (count > mailFolder.Count || count == 0)
            {
                count = mailFolder.Count;
            }

            List<Message> messages = await FetchMessagesAsync(mailFolder,
                                                              mailFolder.Count - count,
                                                              mailFolder.Count - 1, // this range should include border, for zero-base indecies we should substruct 1
                                                              false,
                                                              cancellationToken).ConfigureAwait(false);
            await mailFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);

            return messages;
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
            catch (MailKit.Net.Imap.ImapProtocolException)
            {
                // after exception connection may be lost
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
            }

            mailFolder = await ImapClient.GetFolderAsync(mailFolder.FullName, cancellationToken).ConfigureAwait(false);
            await mailFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

            List<Message> messages = new List<Message>();

            var messageSummaries = await mailFolder.FetchAsync(startIndex, endIndex, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags, cancellationToken).ConfigureAwait(false);
            foreach (var messageSummary in messageSummaries)
            {
                var mimeMessage = await mailFolder.GetMessageAsync(messageSummary.UniqueId, cancellationToken).ConfigureAwait(false);
                var message = mimeMessage.ToTuviMailMessage(mailFolder.ToTuviMailFolder(), cancellationToken);
                message.Id = messageSummary.UniqueId.Id;
                message.IsMarkedAsRead = messageSummary.Flags.Value.HasFlag(MessageFlags.Seen);
                message.IsFlagged = messageSummary.Flags.Value.HasFlag(MessageFlags.Flagged);
                messages.Add(message);
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
            var mailFolder = await ImapClient.GetFolderAsync(folder.FullName, cancellationToken).ConfigureAwait(false);

            if (!mailFolder.Exists)
            {
                throw new FolderIsNotExistException(folder.FullName);
            }

            await mailFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
            int count = mailFolder.Count;
            await mailFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);

            return count;
        }

        public override async Task<Message> GetMessageAsync(Folder folder, uint id, CancellationToken cancellationToken)
        {
            try
            {
                return await GetMessageAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                // after exception connection may be lost
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
                // retry once
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

                    await imapFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);

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
                        await mailFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
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

            await mailFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);

            return messages;
        }

        public override async Task<IReadOnlyList<Message>> GetEarlierMessagesAsync(Folder folder, int count, Message lastMessage, bool fast, CancellationToken cancellationToken)
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
                        await mailFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
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
                        await mailFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
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

            await mailFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);

            return messages;
        }

        public override async Task<IList<Message>> CheckNewMessagesAsync(Folder folder, DateTime dateTime, CancellationToken cancellationToken)
        {
            var mailFolder = await ImapClient.GetFolderAsync(folder.FullName, cancellationToken).ConfigureAwait(false);
            await mailFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

            var ids = await mailFolder.SearchAsync(MailKit.Search.SearchQuery.DeliveredAfter(dateTime), cancellationToken).ConfigureAwait(false);
            var messagesSymmarys = await mailFolder.FetchAsync(ids, MessageSummaryItems.Full | MessageSummaryItems.PreviewText | MessageSummaryItems.UniqueId, cancellationToken).ConfigureAwait(false);

            List<Message> messages = new List<Message>();
            messages.AddRange(from message in messagesSymmarys select message.ToTuviMailMessage(mailFolder.ToTuviMailFolder()));
            await mailFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);

            return messages;
        }

        public override async Task AppendSentMessageAsync(Message message, string messageId, CancellationToken cancellationToken)
        {
            var sentFolder = GetSentFolder();

            try
            {
                await sentFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
                var uids = await sentFolder.SearchAsync(MailKit.Search.SearchQuery.HeaderContains("Message-Id", $"<{messageId}>"), cancellationToken).ConfigureAwait(false);
                await sentFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);

                if (uids.Any())
                {
                    return;
                }
            }
            catch (MailKit.Net.Imap.ImapCommandException)
            {
                if (sentFolder.IsOpen)
                {
                    await sentFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
                }
            }

            await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);
            sentFolder = GetSentFolder();
            await sentFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
            using (var mimeMessage = message.ToMimeMessage())
            {
                mimeMessage.MessageId = messageId;
                var result = await sentFolder.AppendAsync(mimeMessage, MessageFlags.Seen, cancellationToken).ConfigureAwait(false);
                await sentFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
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

                await draftsFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
            }

            return message;
        }

        public override async Task<Message> ReplaceDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken)
        {
            var draftsFolder = GetDraftsFolder();
            await draftsFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

            using (var mimeMessage = message.ToMimeMessage())
            {
                message.Date = DateTimeOffset.UtcNow;
                var uid = await draftsFolder.ReplaceAsync(new UniqueId(id), mimeMessage, MessageFlags.Draft | MessageFlags.Seen, cancellationToken).ConfigureAwait(false);
                message.Id = uid.Value.Id;
                message.IsMarkedAsRead = true;

                await draftsFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
            }

            return message;
        }

        public override async Task MarkMessagesAsReadAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken)
        {
            var folder = await ImapClient.GetFolderAsync(folderPath.FullName, cancellationToken).ConfigureAwait(false);
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
            await folder.AddFlagsAsync(ids.Select(id => new UniqueId(id)).ToList(), MessageFlags.Seen, false, cancellationToken).ConfigureAwait(false);
            await folder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
        }

        public override async Task MarkMessagesAsUnReadAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken)
        {
            var folder = await ImapClient.GetFolderAsync(folderPath.FullName, cancellationToken).ConfigureAwait(false);
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
            await folder.RemoveFlagsAsync(ids.Select(id => new UniqueId(id)).ToList(), MessageFlags.Seen, false, cancellationToken).ConfigureAwait(false);
            await folder.CloseAsync(false, cancellationToken).ConfigureAwait(false);

            return;
        }

        public override async Task FlagMessagesAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken)
        {
            var folder = await ImapClient.GetFolderAsync(folderPath.FullName, cancellationToken).ConfigureAwait(false);
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
            await folder.AddFlagsAsync(ids.Select(id => new UniqueId(id)).ToList(), MessageFlags.Flagged, false, cancellationToken).ConfigureAwait(false);
            await folder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
        }

        public override async Task UnflagMessagesAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken)
        {
            var folder = await ImapClient.GetFolderAsync(folderPath.FullName, cancellationToken).ConfigureAwait(false);
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
            await folder.RemoveFlagsAsync(ids.Select(id => new UniqueId(id)).ToList(), MessageFlags.Flagged, false, cancellationToken).ConfigureAwait(false);
            await folder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
        }

        public override async Task DeleteMessagesAsync(IReadOnlyList<uint> ids, Folder folderPath, bool permanentDelete, CancellationToken cancellationToken)
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
                        await folder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                }
                catch (NotSupportedException)
                {
                }
            }

            await folder.AddFlagsAsync(uniqueIds, MessageFlags.Deleted, true, cancellationToken).ConfigureAwait(false);
            await folder.ExpungeAsync(uniqueIds, cancellationToken).ConfigureAwait(false);
            await folder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
        }

        public override async Task MoveMessagesAsync(IReadOnlyList<uint> ids, Folder folderPath, Folder targetFolderPath, CancellationToken cancellationToken)
        {
            var folder = await ImapClient.GetFolderAsync(folderPath.FullName, cancellationToken).ConfigureAwait(false);
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

            var targetFolder = await ImapClient.GetFolderAsync(targetFolderPath.FullName, cancellationToken).ConfigureAwait(false);

            var uniqueIds = new List<uint>(ids).ConvertAll(x => new UniqueId(x));


            try
            {
                await folder.MoveToAsync(uniqueIds, targetFolder, cancellationToken).ConfigureAwait(false);
                await folder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (NotSupportedException)
            {
            }
           

            await folder.AddFlagsAsync(uniqueIds, MessageFlags.Deleted, true, cancellationToken).ConfigureAwait(false);
            await folder.ExpungeAsync(uniqueIds, cancellationToken).ConfigureAwait(false);
            await folder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
        }

        public override void Dispose()
        {
            ImapClient?.Dispose();
        }
    }
}
