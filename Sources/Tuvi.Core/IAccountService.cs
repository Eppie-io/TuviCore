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

        /// <summary>
        /// Get body of message with high priority. Message content will be loaded and decrypted if it's necessary.
        /// </summary>
        /// <param name="message">Message to get body for</param>
        /// <returns>Fully loaded and processed message</returns>
        /// <exception cref="NoSecretKeyException"/>
        /// <exception cref="MessageDecryptionException"/>
        /// <exception cref="MessageSignatureVerificationException"/>
        Task<Message> GetMessageBodyHighPriorityAsync(Message message, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Message>> ReceiveNewMessagesInFolderAsync(Folder folder, CancellationToken cancellationToken = default);

        Task DeleteMessageAsync(Message message, CancellationToken cancellationToken = default);
        Task DeleteMessagesAsync(Folder folder, IReadOnlyList<Message> messages, CancellationToken cancellationToken = default);

        Task PermanentDeleteMessageAsync(Message message, CancellationToken cancellationToken = default);

        Task<Message> CreateDraftMessageAsync(Message message, CancellationToken cancellationToken = default);

        Task<Message> UpdateDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken = default);

        Task AddMessagesToDataStorageAsync(Folder folder, IReadOnlyList<Message> messageList, CancellationToken cancellationToken);

        Task<int> GetUnreadMessagesCountInFolderAsync(Folder folder, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Message>> ReceiveEarlierMessagesAsync(Folder folder, int count, CancellationToken cancellationToken);

        Task UpdateFolderStructureAsync(CancellationToken cancellationToken);

        Task SynchronizeAsync(bool full, CancellationToken cancellationToken);

        Task SynchronizeFolderAsync(Folder folder, bool full, CancellationToken cancellationToken);

        Task MoveMessagesAsync(Folder folder, Folder targetFolder, IReadOnlyList<Message> messages, CancellationToken cancellationToken);
    }
}
