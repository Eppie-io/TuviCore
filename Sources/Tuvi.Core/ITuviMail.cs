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
        Task<Account> NewDecentralizedAccountAsync(CancellationToken cancellationToken = default);
        Task AddAccountAsync(Account account, CancellationToken cancellationToken = default);
        Task DeleteAccountAsync(Account account, CancellationToken cancellationToken = default);
        Task UpdateAccountAsync(Account account, CancellationToken cancellationToken = default);
        Task CreateHybridEmailAsync(EmailAddress email, CancellationToken cancellationToken = default);

        Task CheckForNewMessagesInFolderAsync(CompositeFolder folder, CancellationToken cancellationToken = default);
        Task CheckForNewInboxMessagesAsync(CancellationToken cancellationToken = default);

        Task<IEnumerable<Contact>> GetContactsAsync(CancellationToken cancellationToken = default);

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
        /// <returns>List of pairs where the key is contact email an the value in the number of the stored unread messages</returns>/>
        Task<IEnumerable<KeyValuePair<EmailAddress, int>>> GetUnreadMessagesCountByContactAsync(CancellationToken cancellationToken = default);

        Task DeleteMessagesAsync(IReadOnlyList<Message> messages, CancellationToken cancellationToken = default);

        Task RestoreFromBackupIfNeededAsync(Uri downloadUri);

        Task MarkMessagesAsReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);
        Task MarkMessagesAsUnReadAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);
        Task FlagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);
        Task UnflagMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);
        Task<Message> GetMessageBodyAsync(Message message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send message in ordinary way without any additional protection.
        /// </summary>
        Task SendMessageAsync(Message message, bool encrypt, bool sign, CancellationToken cancellationToken = default);

        Task<Message> CreateDraftMessageAsync(Message message, CancellationToken cancellationToken = default);
        Task<Message> UpdateDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken = default);

        Task MoveMessagesAsync(IReadOnlyList<Message> messages, CompositeFolder targetFolder, CancellationToken cancellationToken = default);
    }
}
