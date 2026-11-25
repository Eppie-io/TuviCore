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
    /// <summary>
    /// Provides mail operations and owns the resources used by the core.
    /// </summary>
    /// <remarks>
    /// <see cref="IDisposable.Dispose"/> waits for scheduled checks and resource cleanup.
    /// Repeated external calls observe the same cleanup result. If reset was accepted first,
    /// Dispose waits for that reset before releasing resources; reset errors remain on the
    /// reset task. If Dispose was accepted first, the database is preserved and reset is rejected.
    /// An active check, its message handlers and its synchronous cancellation callbacks must
    /// forward initialization, account deletion, reset or disposal requests to the owner and
    /// return. Direct calls on the same core throw InvalidOperationException before starting
    /// an operation or joining its completion.
    /// Requests for security, backup, AI storage and credentials throw ObjectDisposedException
    /// once shutdown is accepted. Previously issued references are not automatically revoked.
    /// The application must complete Dispose on the old core, including after a failed reset,
    /// before creating the next owner of the same database file.
    /// </remarks>
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
        event EventHandler<FolderCreatedEventArgs> FolderCreated;
        event EventHandler<FolderDeletedEventArgs> FolderDeleted;
        event EventHandler<FolderRenamedEventArgs> FolderRenamed;
        event EventHandler<ExceptionEventArgs> ExceptionOccurred;
        event EventHandler<ContactAddedEventArgs> ContactAdded;
        event EventHandler<ContactChangedEventArgs> ContactChanged;
        event EventHandler<ContactDeletedEventArgs> ContactDeleted;

        Task TestMailServerAsync(string serverAddress, int serverPort, MailProtocol protocol, ICredentialsProvider credentialsProvider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check application password and initialize application.
        /// </summary>
        /// <returns>True if password was accepted.</returns>
        /// <remarks>
        /// Failures while stopping previous checks propagate. A closed core rejects initialization.
        /// This core's active checks, message handlers and synchronous cancellation callbacks must
        /// forward initialization requests to the core owner and return from the handler.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Called from this core's active check, message handler or cancellation callback.</exception>
        Task<bool> InitializeApplicationAsync(string password, CancellationToken cancellationToken = default);

        /// <summary>
        /// Let to check if application is started first time and has never been initialized previously.
        /// </summary>
        Task<bool> IsFirstApplicationStartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reset application and erase all its data.
        /// </summary>
        /// <remarks>
        /// The first accepted call immediately closes access to this core's profile and
        /// registration of account schedulers, including when no database file exists.
        /// It stops every owned account check before calling SecurityManager.ResetAsync once.
        /// A stop failure preserves the database. This core remains terminal on every outcome.
        /// Concurrent and subsequent reset calls share the first task and its final success
        /// or failure; they never make another storage reset attempt.
        /// Dispose the old core before creating new core, storage and security manager instances.
        /// Dispose waits for an already accepted reset even before it reaches storage. If
        /// disposal was accepted first, reset throws ObjectDisposedException and preserves the file.
        /// Calls from this core's active check, its message handlers or its synchronous
        /// cancellation callbacks throw InvalidOperationException even if reset is in progress.
        /// Those handlers must forward the request to the owner and return.
        /// New contact notifications are stopped, including between subscribers. Already
        /// admitted callbacks may finish after reset; reset does not wait for contact callbacks.
        /// </remarks>
        /// <exception cref="DataBaseException" />
        /// <exception cref="InvalidOperationException">Called from this core's check or cancellation callback.</exception>
        /// <exception cref="ObjectDisposedException">Ordinary disposal was accepted before reset.</exception>
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
        /// <summary>Stop synchronization and delete the account and its folders.</summary>
        /// <remarks>
        /// Cancellation is observed before synchronization is stopped. Once accepted, deletion
        /// finishes without caller cancellation. Scheduler stop and storage failures propagate.
        /// A folder deletion failure attempts to restore synchronization for the surviving account,
        /// unless a later deletion or shutdown has taken over. Committed folder changes are not
        /// rolled back, and the original deletion error propagates even if restoration fails.
        /// This core's active checks, message handlers and synchronous cancellation callbacks must
        /// forward deletion requests to the core owner and return, regardless of the target account.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Called from this core's active check, message handler or cancellation callback.</exception>
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

        Task<Message> CreateDraftMessageAsync(Account account, Message message, CancellationToken cancellationToken = default);
        Task<Message> UpdateDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken = default);

        Task MoveMessagesAsync(IReadOnlyList<Message> messages, CompositeFolder targetFolder, CancellationToken cancellationToken = default);
        Task UpdateMessageProcessingResultAsync(Message message, string result, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a new folder in the specified mailbox
        /// </summary>
        /// <param name="accountEmail">Email address of the account</param>
        /// <param name="folderName">Name of the folder to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created folder</returns>
        Task<Folder> CreateFolderAsync(EmailAddress accountEmail, string folderName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a folder from an account's mailbox
        /// </summary>
        /// <param name="accountEmail">Email address of the account</param>
        /// <param name="folder">Folder to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteFolderAsync(EmailAddress accountEmail, Folder folder, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rename a folder in an account's mailbox
        /// </summary>
        /// <param name="accountEmail">Email address of the account</param>
        /// <param name="folder">Folder to rename</param>
        /// <param name="newName">New name for the folder</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Renamed folder</returns>
        Task<Folder> RenameFolderAsync(EmailAddress accountEmail, Folder folder, string newName, CancellationToken cancellationToken = default);

        Task<string> ClaimDecentralizedNameAsync(string name, Account account, CancellationToken cancellationToken = default);
    }
}
