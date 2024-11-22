using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail.Impl.Protocols
{
    abstract class ReceiverService : MailService, IDisposable
    {
        protected ReceiverService(string serverAddress, int serverPort)
            : base(serverAddress, serverPort)
        {
        }

        public abstract Folder GetDefaultInboxFolder();
        public abstract Task<IList<Folder>> GetFoldersStructureAsync(CancellationToken cancellationToken);
        public abstract Task<IReadOnlyList<Message>> GetMessagesAsync(Folder folder, int count, CancellationToken cancellationToken);
        public abstract Task<int> GetMessageCountAsync(Folder folder, CancellationToken cancellationToken);
        public abstract Task<Message> GetMessageAsync(Folder folder, uint id, CancellationToken cancellationToken);
        public abstract Task<IReadOnlyList<Message>> GetLaterMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken);
        public abstract Task<IReadOnlyList<Message>> GetEarlierMessagesAsync(Folder folder, int count, Message lastMessage, bool fast, CancellationToken cancellationToken);
        public abstract Task<IList<Message>> CheckNewMessagesAsync(Folder folder, DateTime dateTime, CancellationToken cancellationToken);
        public abstract Task AppendSentMessageAsync(Message message, string messageId, CancellationToken cancellationToken);
        public abstract Task<Message> AppendDraftMessageAsync(Message message, CancellationToken cancellationToken);
        public abstract Task<Message> ReplaceDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken);
        public abstract Task MarkMessagesAsReadAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken);
        public abstract Task MarkMessagesAsUnReadAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken);
        public abstract Task FlagMessagesAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken);
        public abstract Task UnflagMessagesAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken);
        public abstract Task DeleteMessagesAsync(IReadOnlyList<uint> ids, Folder folderPath, bool permanentDelete, CancellationToken cancellationToken);
        public abstract Task MoveMessagesAsync(IReadOnlyList<uint> ids, Folder folderPath, Folder targetFolderPath, CancellationToken cancellationToken);

        public abstract void Dispose();
    }
}
