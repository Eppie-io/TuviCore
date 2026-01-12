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
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using Tuvi.Core.Utils;

namespace Tuvi.Core
{
    public interface ITuviMail : IDisposable
    {
        ISecurityManager GetSecurityManager();
        IBackupManager GetBackupManager();
        ICredentialsManager CredentialsManager { get; }
        ITextUtils GetTextUtils();
        IAIAgentsStorage GetAIAgentsStorage();

        event EventHandler<MessagesReceivedEventArgs> MessagesReceived;
        event EventHandler<UnreadMessagesReceivedEventArgs> UnreadMessagesReceived;
        event EventHandler<MessageDeletedEventArgs> MessageDeleted;
        event EventHandler<MessagesAttributeChangedEventArgs> MessagesIsReadChanged;
        event EventHandler<MessagesAttributeChangedEventArgs> MessagesIsFlaggedChanged;
        event EventHandler<AccountEventArgs> AccountAdded;
        event EventHandler<AccountEventArgs> AccountUpdated;
        event EventHandler<AccountEventArgs> AccountDeleted;
        event EventHandler<ExceptionEventArgs> ExceptionOccurred;
        event EventHandler<ContactAddedEventArgs> ContactAdded;
        event EventHandler<ContactChangedEventArgs> ContactChanged;
        event EventHandler<ContactDeletedEventArgs> ContactDeleted;
        event EventHandler<EventArgs> WipeAllDataNeeded;

        Task TestMailServerAsync(string serverAddress, int serverPort, MailProtocol protocol, ICredentialsProvider credentialsProvider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check application password and initialize application.
        /// </summary>
        /// <returns>True if password was accepted.</returns>
        Task<bool> InitializeApplicationAsync(string password, CancellationToken cancellationToken = default);

        /// <summary>
        /// Let to check if application is started first time and has never been initialized previously.
        /// </summary>
        Task<bool> IsFirstApplicationStartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reset application and erase all its data.
        /// </summary>
        /// <exception cref="DataBaseException" />
        Task ResetApplicationAsync();

        /// <summary>
        /// Change application password.
        /// </summary>
        Task<bool> ChangeApplicationPasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);

        Task<bool> ExistsAccountWithEmailAddressAsync(EmailAddress email, CancellationToken cancellationToken = default);
        Task<Account> GetAccountAsync(EmailAddress email, CancellationToken cancellationToken = default);
        Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CompositeAccount>> GetCompositeAccountsAsync(CancellationToken cancellationToken = default);
        Task<IAccountService> GetAccountServiceAsync(EmailAddress email, CancellationToken cancellationToken = default);
        Task AddAccountAsync(Account account, CancellationToken cancellationToken = default);
        Task DeleteAccountAsync(Account account, CancellationToken cancellationToken = default);
        Task UpdateAccountAsync(Account account, CancellationToken cancellationToken = default);
        Task CreateHybridAccountAsync(Account account, CancellationToken cancellationToken = default);

        Task CheckForNewMessagesInFolderAsync(CompositeFolder folder, CancellationToken cancellationToken = default);
        Task CheckForNewInboxMessagesAsync(CancellationToken cancellationToken = default);

        Task<IEnumerable<Contact>> GetContactsAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Contact>> GetContactsAsync(int count, Contact lastContact, ContactsSortOrder sortOrder, CancellationToken cancellationToken = default);

        Task SetContactNameAsync(EmailAddress contactEmail, string newName, CancellationToken cancellationToken = default);

        Task SetContactAvatarAsync(EmailAddress contactEmail, byte[] avatarBytes, int avatarWidth, int avatarHeight, CancellationToken cancellationToken = default);

        Task RemoveContactAsync(EmailAddress contactEmail, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Message>> GetAllEarlierMessagesAsync(int count, Message lastMessage, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Message>> GetContactEarlierMessagesAsync(EmailAddress contactEmail, int count, Message lastMessage, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Message>> GetFolderEarlierMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Message>> GetFolderEarlierMessagesAsync(CompositeFolder folder, int count, Message lastMessage, CancellationToken cancellationToken = default);

        Task<int> GetUnreadCountForAllAccountsInboxAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get number of unread stored messages for each email the list of <paramref name="contacts"/>.
        /// </summary>
        /// <param name="contacts"></param>
        /// <returns>Dictionary where the key is contact email an the value in the number of the stored unread messages</returns>/>
        Task<IReadOnlyDictionary<EmailAddress, int>> GetUnreadMessagesCountByContactAsync(CancellationToken cancellationToken = default);

        Task DeleteMessagesAsync(IReadOnlyList<Message> messages, CancellationToken cancellationToken = default);

        Task RestoreFromBackupIfNeededAsync(Uri downloadUri);

        Task MarkMessagesAsReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);
        Task MarkMessagesAsUnReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);
        Task FlagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);
        Task UnflagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);
        Task<Message> GetMessageBodyAsync(Message message, CancellationToken cancellationToken = default);
        Task<Message> GetMessageBodyHighPriorityAsync(Message message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send message in ordinary way without any additional protection.
        /// </summary>
        Task SendMessageAsync(Message message, bool encrypt, bool sign, CancellationToken cancellationToken = default);

        Task<Message> CreateDraftMessageAsync(Message message, CancellationToken cancellationToken = default);
        Task<Message> UpdateDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken = default);

        Task MoveMessagesAsync(IReadOnlyList<Message> messages, CompositeFolder targetFolder, CancellationToken cancellationToken = default);
        Task UpdateMessageProcessingResultAsync(Message message, string result, CancellationToken cancellationToken = default);

        Task<bool> ClaimDecentralizedNameAsync(string name, EmailAddress address, CancellationToken cancellationToken = default);
    }
}
