using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core
{
    public interface IAccountService
    {
        event EventHandler<MessageDeletedEventArgs> MessageDeleted;
        event EventHandler<MessagesAttributeChangedEventArgs> MessagesIsReadChanged;
        event EventHandler<MessagesAttributeChangedEventArgs> MessagesIsFlaggedChanged;
        event EventHandler<UnreadMessagesReceivedEventArgs> UnreadMessagesReceived;
        event EventHandler<FolderMessagesReceivedEventArgs> MessagesReceived;

        /// <summary>
        /// Send <paramref name="message"/> via given service.
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="encrypt">protect message by encryption</param>
        /// <param name="sign">protect content from being tampered with digital signature</param> 
        /// <exception cref="NoSecretKeyException"/>
        /// <exception cref="NoPublicKeyException"/>
        /// <exception cref="MessageEncryptionException"/>
        Task SendMessageAsync(Message message, bool encrypt, bool sign, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mark <paramref name="message"/> as read
        /// </summary>
        /// <param name="message">Message to mark</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task MarkMessageAsReadAsync(Message message, CancellationToken cancellationToken = default);
        /// <summary>
        /// Mark <paramref name="messages"/> as read
        /// </summary>
        /// <param name="messages">Messages to mark</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task MarkMessagesAsReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);
        /// <summary>
        /// Mark <paramref name="message"/> as unread
        /// </summary>
        /// <param name="message">Message to mark</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task MarkMessageAsUnReadAsync(Message message, CancellationToken cancellationToken = default);
        /// <summary>
        /// Mark <paramref name="messages"/> as unread
        /// </summary>
        /// <param name="messages">Messages to mark</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task MarkMessagesAsUnReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks <paramref name="message"/> as flagged
        /// </summary>
        /// <param name="message">Message to flag</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task FlagMessageAsync(Message message, CancellationToken cancellationToken = default);
        /// <summary>
        /// Mark <paramref name="messages"/> as flagged
        /// </summary>
        /// <param name="messages">Messages to mark</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task FlagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);
        /// <summary>
        /// Marks <paramref name="message"/> as unflagged
        /// </summary>
        /// <param name="message">Message to unflag</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task UnflagMessageAsync(Message message, CancellationToken cancellationToken = default);
        /// <summary>
        /// Mark <paramref name="messages"/> as unflagged
        /// </summary>
        /// <param name="messages">Messages to mark</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task UnflagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get body of message. Message content will be loaded and decrypted if it's necessary.
        /// </summary>
        /// <param name="message">Message to get body for</param>
        /// <returns>Fully loaded and processed message</returns>
        /// <exception cref="NoSecretKeyException"/>
        /// <exception cref="MessageDecryptionException"/>
        /// <exception cref="MessageSignatureVerificationException"/>
        Task<Message> GetMessageBodyAsync(Message message, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Message>> ReceiveNewMessagesInFolderAsync(Folder folder, CancellationToken cancellationToken = default);

        Task DeleteMessageAsync(Message message, CancellationToken cancellationToken = default);
        Task DeleteMessagesAsync(Folder folder, IReadOnlyList<Message> messages, CancellationToken cancellationToken = default);

        Task PermanentDeleteMessageAsync(Message message, CancellationToken cancellationToken = default);

        Task<Message> CreateDraftMessageAsync(Message message, CancellationToken cancellationToken = default);

        Task<Message> UpdateDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken = default);

        Task AddMessagesToDataStorageAsync(Folder folder, IReadOnlyList<Message> messageList, CancellationToken cancellationToken);

        Task<int> GetUnreadMessagesCountAsync(CancellationToken cancellationToken = default);
        Task<int> GetUnreadMessagesCountInFolderAsync(Folder folder, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Message>> ReceiveEarlierMessagesAsync(Folder folder, int count, CancellationToken cancellationToken);

        Task UpdateFolderStructureAsync(CancellationToken cancellationToken);

        Task SynchronizeAsync(bool full, CancellationToken cancellationToken);

        Task SynchronizeFolderAsync(Folder folder, bool full, CancellationToken cancellationToken);
    }
}
