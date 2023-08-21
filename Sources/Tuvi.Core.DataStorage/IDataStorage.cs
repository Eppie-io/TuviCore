using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;
using TuviPgpLib;

namespace Tuvi.Core.DataStorage
{
    public interface IDataStorage : IPasswordProtectedStorage, IKeyStorage, IDecStorage, IDisposable
    {
        event EventHandler<ContactAddedEventArgs> ContactAdded;
        event EventHandler<ContactChangedEventArgs> ContactChanged;
        event EventHandler<ContactDeletedEventArgs> ContactDeleted;
        /// <summary>
        /// Add account to storage.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="AccountAlreadyExistInDatabaseException"/>
        Task AddAccountAsync(Account accountData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete account from storage.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="NoCollectionException"/>
        Task DeleteAccountAsync(Account accountData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete account with <paramref name="email"/> from storage.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="NoCollectionException"/>
        /// <exception cref="NoCollectionException"/>
        Task DeleteAccountByEmailAsync(EmailAddress accountEmail, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update account in storage if it exist.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        Task UpdateAccountAsync(Account accountData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get account by email address from storage.
        /// </summary>
        /// <returns>Account</returns>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="AccountIsNotExistInDatabaseException"/>
        Task<Account> GetAccountAsync(EmailAddress accountEmail, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all accounts from storage.
        /// </summary>
        /// <returns>Accounts list</returns>
        Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if account with <paramref name="email"/> exist in storage.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        Task<bool> ExistsAccountWithEmailAddressAsync(EmailAddress accountEmail, CancellationToken cancellationToken = default);

        /// <summary>
        /// Add new account group with unique name to the storage
        /// </summary>
        /// <param name="group"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task AddAccountGroupAsync(AccountGroup group, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retuens the list of all account groups 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IReadOnlyList<AccountGroup>> GetAccountGroupsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes given group from the storage
        /// </summary>
        /// <param name="group"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task DeleteAccountGroupAsync(AccountGroup group, CancellationToken cancellationToken = default);

        /// <summary>
        /// Add message to storage. Throws exception if message already exists in storage.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="MessageAlreadyExistInDatabaseException"/>
        Task AddMessageAsync(EmailAddress accountEmail, Message message, bool updateUnreadAndTotal = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update <paramref name="message"/> in storage.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="NoCollectionException"/>
        Task UpdateMessageAsync(EmailAddress accountEmail, Message message, bool updateUnreadAndTotal = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update <paramref name="messages"/> in storage.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="NoCollectionException"/>
        Task UpdateMessagesAsync(EmailAddress accountEmail, IEnumerable<Message> messages, bool updateUnreadAndTotal = true, CancellationToken cancellationToken = default);

        Task UpdateMessagesFlagsAsync(EmailAddress accountEmail, IEnumerable<Message> messages, bool updateUnreadAndTotal = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Add list of messages to storage. Messages existed in storage are updated.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        Task AddMessageListAsync(EmailAddress accountEmail, string folder, IReadOnlyList<Message> messages, bool updateUnreadAndTotal = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get message by <paramref name="id"/> from storage.
        /// </summary>
        /// <returns>Null if message with <paramref name="id"/> doesn't exist.</returns>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="NoCollectionException"/>
        Task<Message> GetMessageAsync(EmailAddress accountEmail, string folder, uint id, bool fast = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get messages from storage. If <paramref name="count"/> is 0 - get all existing messages
        /// </summary>
        /// <param name="count">Maximum amount of messages returned ordered by Message.Id descending. If 0 - get all existing messages</param>
        /// <returns>Messages list</returns>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="NoCollectionException"/>
        Task<IReadOnlyList<Message>> GetMessageListAsync(EmailAddress accountEmail, string folder, uint count, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get messages  from storage.
        /// </summary>
        /// <param name="fromUid">Only messages starting with Id</param>
        /// <param name="count">Maximum amount of messages returned.</param>
        /// <returns>Messages list ordered by Id descending.<returns>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="NoCollectionException"/>
        Task<IReadOnlyList<Message>> GetMessageListAsync(EmailAddress accountEmail, string folder, uint fromUid, uint count, bool fast = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get messages from storage by range.
        /// </summary>
        /// <param name="range">Only messages with Id from the given range</param>
        /// <returns>Messages list ordered by Id descending.<returns>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="NoCollectionException"/>
        Task<IReadOnlyList<Message>> GetMessageListAsync(EmailAddress accountEmail, string folder, (uint, uint) range, bool fast = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get amount of messages stored.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="NoCollectionException"/>
        Task<uint> GetMessagesCountAsync(EmailAddress accountEmail, string folder, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the latest stored message.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="folder"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="NoCollectionException"/>
        Task<Message> GetLatestMessageAsync(EmailAddress accountEmail, string folder, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the earliest stored message in the folder
        /// </summary>
        /// <param name="accountEmail"></param>
        /// <param name="folder"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Message> GetEarliestMessageAsync(EmailAddress accountEmail, string folder, CancellationToken cancellationToken = default);
        Task<Message> GetEarliestMessageAsync(Folder folder, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the list of stored messages earlier that last message
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="count"></param>
        /// <param name="lastMessage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IReadOnlyList<Message>> GetEarlierMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken);


        /// <summary>
        /// Gets the list of stored messages earlier that last message from given <paramref name="folders"/>
        /// </summary>
        /// <param name="folders"></param>
        /// <param name="count"></param>
        /// <param name="lastMessage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IReadOnlyList<Message>> GetEarlierMessagesInFoldersAsync(IEnumerable<Folder> folders, int count, Message lastMessage, CancellationToken cancellationToken);


        /// <summary>
        /// Check if message with <paramref name="uid"/> exists in storage.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        Task<bool> IsMessageExistAsync(EmailAddress accountEmail, string folder, uint uid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete mailbox folder with all its content.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        Task DeleteFolderAsync(EmailAddress accountEmail, string folder, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete message with <paramref name="uid"/> from storage.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="NoCollectionException"/>
        Task DeleteMessageAsync(EmailAddress accountEmail, string folder, uint uid, bool updateUnreadAndTotal = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete messages list from storage.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        /// <exception cref="NoCollectionException"/>
        Task DeleteMessagesAsync(EmailAddress accountEmail, string folder, IEnumerable<uint> messages, bool updateUnreadAndTotal = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get list of stored messages earlier than lastMessage containing contactEmail as sender or recipient
        /// </summary>
        /// <exception cref="DataBaseException"/>
        Task<IReadOnlyList<Message>> GetEarlierContactMessagesAsync(EmailAddress contactEmail, int count, Message lastMessage, CancellationToken cancellationToken);

        /// <summary>
        /// Get number of unread stored messages
        /// </summary>
        /// <exception cref="DataBaseException"/>
        Task<int> GetUnreadMessagesCountAsync(EmailAddress accountEmail, string folder, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get list of contacts in address book
        /// </summary>
        /// <exception cref="DataBaseException"/>
        Task<IEnumerable<Contact>> GetContactsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets contact with <paramref name="contactEmail"/>
        /// </summary>
        /// <returns>Contact with <paramref name="contactEmail"/> if exists, otherwise null</returns>
        /// <exception cref="DataBaseException"/>
        Task<Contact> GetContactAsync(EmailAddress contactEmail, CancellationToken cancellationToken);

        /// <summary>
        /// Add contact to address book if contact doesn't exist there. 
        /// </summary>
        /// <exception cref="DataBaseException"/>
        Task AddContactAsync(Contact contact, CancellationToken cancellationToken);

        /// <summary>
        /// Try to add contact to address book
        /// </summary>
        /// <param name="contact">Contact to add</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if <paramref name="contact"/> was successfully added and False if <paramref name="contact"/> already exists or exception occured</returns>
        Task<bool> TryAddContactAsync(Contact contact, CancellationToken cancellationToken);

        /// <summary>
        /// Check if contact with <paramref name="email"/> exist in storage.
        /// </summary>
        /// <exception cref="DataBaseException"/>
        Task<bool> ExistsContactWithEmailAddressAsync(EmailAddress accountEmail, CancellationToken cancellationToken);

        /// <summary>
        /// Sets contact's avatar
        /// </summary>
        /// <param name="contactEmail">The email of contact whose avatar will be set</param>
        /// <param name="avatarBytes">Array of bytes with image data</param>
        /// <param name="avatarWidth">Avatar width in pixels</param>
        /// <param name="avatarHeight">Avatar height in pixels</param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="DataBaseException"/>
        Task SetContactAvatarAsync(EmailAddress contactEmail, byte[] avatarBytes, int avatarWidth, int avatarHeight, CancellationToken cancellationToken);

        /// <summary>
        /// Updates contact
        /// </summary>
        /// <exception cref="DataBaseException"/>
        Task UpdateContactAsync(Contact contact, CancellationToken cancellationToken);

        /// <summary>
        /// Removes contact from address book
        /// </summary>
        /// <param name="contactEmail">Email of the contact to remove</param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="DataBaseException"/>
        Task RemoveContactAsync(EmailAddress contactEmail, CancellationToken cancellationToken);

        /// <summary>
        /// Removes avatar info from the contact and file with avatar from FileStorage
        /// </summary>
        /// <param name="contactEmail">Email of the contact whose avatar need to remove</param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="DataBaseException"/>
        Task RemoveContactAvatarAsync(EmailAddress contactEmail, CancellationToken cancellationToken);

        /// <summary>
        /// Gets all contacts whose last message id is <paramref name="messageId"/> and this message is from account with email <paramref name="accountEmail"/>
        /// </summary>
        /// <param name="accountEmail"></param>
        /// <param name="messageId"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="DataBaseException"/>
        Task<IEnumerable<Contact>> GetContactsWithLastMessageIdAsync(EmailAddress accountEmail, uint messageId, CancellationToken cancellationToken);

        /// <summary>
        /// Tries to find the last message that refers to contact <paramref name="contactEmail"/> in folder <paramref name="folder"/> of the account with email <paramref name="email"/>. If no message is found, returns null
        /// </summary>
        /// <param name="email"></param>
        /// <param name="folder"></param>
        /// <param name="contactEmail"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="DataBaseException"/>
        Task<Message> GetContactLastMessageAsync(EmailAddress accountEmail, string folder, EmailAddress contactEmail, CancellationToken cancellationToken);

        /// <summary>
        /// Get number of unread stored messages for contact with <paramref name="contactEmail"/> in the <paramref name="accountEmail"/>.
        /// </summary>
        /// <param name="contactEmail"></param>
        /// <exception cref="DataBaseException"/>
        Task<int> GetContactUnreadMessagesCountAsync(EmailAddress contactEmail, CancellationToken cancellationToken);

        /// <summary>
        /// Get number of unread stored messages for each email the list of <paramref name="contacts"/> in the <paramref name="accountEmail"/>.
        /// </summary>
        /// <param name="accountEmail"></param>
        /// <param name="contacts"></param>
        /// <exception cref="DataBaseException"/>
        Task<IEnumerable<KeyValuePair<EmailAddress, int>>> GetUnreadMessagesCountByContactAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Get all the settings.
        /// </summary>
        Task<Settings> GetSettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Save all the settings.
        /// </summary>
        Task SetSettingsAsync(Settings settings, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update account email to a new one.
        /// </summary>
        Task UpdateAccountEmailAsync(EmailAddress prev, EmailAddress email, CancellationToken cancellationToken = default);
    }
}
