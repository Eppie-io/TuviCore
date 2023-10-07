using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail
{
    public interface IMailBox : IDisposable
    {
        /// <summary>
        /// Returns true if a folder in this mailbox cant report TotalCount and UnreadCount
        /// </summary>
        bool HasFolderCounters { get; }

        /// <summary>
        /// Get default inbox folder information.
        /// </summary>
        Task<Folder> GetDefaultInboxFolderAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get existing mailbox folders.
        /// </summary>
        /// <returns>List of folders</returns>.
        Task<IList<Folder>> GetFoldersStructureAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get limited <paramref name="count"/> of messages
        /// from specified <paramref name="folder"/>.
        /// </summary>
        /// <returns>List of messages</returns>
        Task<IReadOnlyList<Message>> GetMessagesAsync(Folder folder, int count, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send <paramref name="message"/>.
        /// </summary>
        /// <exception cref="MailServiceIsNotConnectedException"/>
        /// <exception cref="MailServiceIsNotAuthenticatedException"/>
        Task SendMessageAsync(Message message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get by <paramref name="id"/> message from <paramref name="folder"/>.
        /// </summary>
        /// <returns>Message</returns>
        /// <exception cref="MessageIsNotExistException"/>
        Task<Message> GetMessageByIDAsync(Folder folder, uint id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get limited <paramref name="count"/> of messages from <paramref name="folder"/>
        /// which delivered later than <paramref name="lastMessage"/>.
        /// </summary>
        /// <returns>List of messages</returns>
        Task<IReadOnlyList<Message>> GetLaterMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get limited <paramref name="count"/> of messages from <paramref name="folder"/>
        /// which delivered earlier than <paramref name="lastMessage"/>.
        /// </summary>
        /// <returns>List of messages</returns>
        Task<IReadOnlyList<Message>> GetEarlierMessagesAsync(Folder folder, int count, Message lastMessag, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get limited <paramref name="count"/> of messages from <paramref name="folder"/>
        /// which delivered earlier than <paramref name="lastMessage"/>.
        /// </summary>
        /// <returns>List of messages</returns>
        Task<IReadOnlyList<Message>> GetEarlierMessagesForSynchronizationAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken = default);

        /// <summary>
        /// Append draft <paramref name="message"/> into Drafts folder.
        /// </summary>
        /// <param name="message">Message to append.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Appended message.</returns>
        Task<Message> AppendDraftMessageAsync(Message message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replace draft <paramref name="message"/> with <paramref name="id"/> in Drafts folder.
        /// </summary>
        /// /// <param name="id">Message id for replace.</param>
        /// <param name="message">Message to replace.</param>
        /// <param name="cancellationToken">Cancellation token.</param>   
        /// <returns>Replaced message.</returns>
        Task<Message> ReplaceDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mark <paramref name="messages"/> as read.
        /// </summary>
        Task MarkMessagesAsReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mark <paramref name="messages"/> as unread.
        /// </summary>
        Task MarkMessagesAsUnReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mark <paramref name="messages"/> as flagged.
        /// </summary>
        Task FlagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mark <paramref name="messages"/> as unflagged.
        /// </summary>
        Task UnflagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete messages with <paramref name="ids"/> list in <paramref name="folderPath"/> to trash folder if <paramref name="permanentDelete"/> is false.
        /// </summary>
        Task DeleteMessagesAsync(IReadOnlyList<uint> ids, Folder folder, bool permanentDelete = false, CancellationToken cancellationToken = default);
    }
}
