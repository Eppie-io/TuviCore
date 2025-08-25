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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SQLite;
using Tuvi.Core.Entities;
using Tuvi.Core.Logging;

namespace Tuvi.Core.DataStorage.Impl
{
    #region Internal data
    class EmailAddressData
    {
        public EmailAddressData()
        {

        }

        public EmailAddressData(EmailAddress email)
        {
            if (email is null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            Address = email.Address;
            Name = email.Name;
        }

        [PrimaryKey]
        [AutoIncrement]
        public int Id { get; set; }

        [Indexed(Unique = true)]
        [Collation("NOCASE")]
        public string Address { get; set; }

        public string Name { get; set; }

        public EmailAddress ToEmailAddress()
        {
            return new EmailAddress(Address, Name);
        }

        public void UpdateValue(EmailAddress email)
        {
            if (!(email?.Name is null))
            {
                Name = email.Name;
            }
        }
    }

    class MessageEmailAddress
    {
        [PrimaryKey]
        [AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int Message { get; set; }

        [Indexed]
        public int Email { get; set; }

        public enum EmailType { From, ReplyTo, To, Cc, Bcc };
        public EmailType Type { get; set; }
    }

    class MessageContact
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public int MessageId { get; set; }
        [Indexed]
        public int ContactId { get; set; }
    }

    class ProtonMessageIdV2
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public string MessageId { get; set; }
        [Indexed]
        public int AccountId { get; set; }
    }

    class ProtonLabelV2
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public string LabelId { get; set; }
        [Indexed]
        public int AccountId { get; set; }
    }

    class ProtonMessageLabel
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public int MessageId { get; set; }
        [Indexed]
        public int LabelId { get; set; }
        [Indexed]
        public int AccountId { get; set; }
    }

    #endregion

    static class EnumerableExtension
    {
        public static List<T> ToList<T>(this IEnumerable<T> collection, CancellationToken cancellationToken)
        {
            var col = collection as ICollection<T>;
            var list = new List<T>(col != null ? col.Count : 16);
            foreach (var item in collection)
            {
                cancellationToken.ThrowIfCancellationRequested();
                list.Add(item);
            }
            return list;
        }
    }

    class ChangeState
    {
        public enum Change
        {
            Add,
            Update,
            Delete
        }

        List<KeyValuePair<Change, object>> _changes;
        DataStorage _storage;

        public ChangeState(DataStorage storage)
        {
            _changes = new List<KeyValuePair<Change, object>>();
            _storage = storage;
        }

        public void Add(object o)
        {
            AddChange(Change.Add, o);
        }

        public void Update(object o)
        {
            AddChange(Change.Update, o);
        }

        public void Delete(object o)
        {
            AddChange(Change.Delete, o);
        }

        private void AddChange(Change c, object o)
        {
            _changes.Add(new KeyValuePair<Change, object>(c, o));
        }

        public void RaiseEvents()
        {
            foreach (var change in _changes)
            {
                if (change.Value is Contact)
                {
                    var c = change.Value as Contact;
                    switch (change.Key)
                    {
                        case Change.Add:
                            _storage.RaiseContactAddedEvent(c);
                            break;
                        case Change.Update:
                            _storage.RaiseContactChangedEvent(c);
                            break;
                        case Change.Delete:
                            _storage.RaiseContactDeletedEvent(c);
                            break;
                    }
                }
            }
        }


    }

    struct DbConnection
    {
        public SQLiteConnection Connection { get; private set; }

        public ChangeState ChangeState { get; private set; }

        public DbConnection(SQLiteConnection connection, ChangeState changeState)
        {
            Connection = connection;
            ChangeState = changeState;
        }
    }

    internal class DataStorage : KeyStorage, IDataStorage, Proton.IStorage
    {
        private static readonly ILogger Logger = LoggingExtension.Log<DataStorage>();

        public event EventHandler<ContactAddedEventArgs> ContactAdded;
        public event EventHandler<ContactChangedEventArgs> ContactChanged;
        public event EventHandler<ContactDeletedEventArgs> ContactDeleted;

        public DataStorage(string filePath)
            : base(filePath)
        {

        }

        ~DataStorage()
        {
            Dispose(false);
        }

        public void RaiseContactAddedEvent(Contact contact)
        {
            ContactAdded?.Invoke(this, new ContactAddedEventArgs(contact));
        }

        public void RaiseContactChangedEvent(Contact contact)
        {
            ContactChanged?.Invoke(this, new ContactChangedEventArgs(contact));
        }

        public void RaiseContactDeletedEvent(Contact contact)
        {
            ContactDeleted?.Invoke(this, new ContactDeletedEventArgs(contact.Email));
        }

        private static int GetLastRowId(SQLiteConnection c)
        {
            return c.ExecuteScalar<int>("select last_insert_rowid();");
        }

        private static void AddAccountAuthData(SQLiteConnection connection, int accountId, IAuthenticationData authData)
        {
            if (authData is null)
            {
                return;
            }
            DeleteAccountAuthData(connection, accountId);
            switch (authData.Type)
            {
                case AuthenticationType.Basic:
                    {
                        var data = (BasicAuthData)authData;
                        data.AccountId = accountId;
                        connection.Insert(data);
                    }
                    break;
                case AuthenticationType.OAuth2:
                    {
                        var data = (OAuth2Data)authData;
                        data.AccountId = accountId;
                        connection.Insert(data);
                    }
                    break;
                case AuthenticationType.Proton:
                    {
                        var data = (ProtonAuthData)authData;
                        data.AccountId = accountId;
                        connection.Insert(data);
                    }
                    break;
            }
        }

        private void AddAccountFolders(DbConnection db, int accountId, Account account, CancellationToken cancellationToken)
        {
            var connection = db.Connection;
            account.FoldersStructure.ForEach(x =>
            {
                x.AccountId = accountId;
                x.AccountEmail = account.Email;
            });

            var prev = connection.Table<Folder>().Where(x => x.AccountId == accountId).ToList(cancellationToken);
            prev.ForEach(x =>
            {
                x.AccountEmail = account.Email;
            });

            foreach (var folder in prev)
            {
                var existingFolder = account.FoldersStructure.Find(y => y.Equals(folder));
                if (existingFolder != null)
                {
                    folder.UnreadCount = existingFolder.UnreadCount;
                    folder.TotalCount = existingFolder.TotalCount;
                    connection.Update(folder);
                }
                else
                {
                    var messages = connection.Table<Entities.Message>().Where(m => m.FolderId == folder.Id).ToList(cancellationToken);
                    messages.ForEach(m => DeleteMessage(db, m, updateUnreadAndTotal: true, cancellationToken));
                    connection.Delete(folder);
                }
            }

            var toAdd = account.FoldersStructure.Where(x => !prev.Exists(y => y.Equals(x))).ToList();
            foreach (var folder in toAdd)
            {
                // explicitly zero this counter, because user can pass any value from the external code.
                // this value should be changed only in storage code, and saved in db
                folder.LocalCount = 0;
            }
            connection.InsertAll(toAdd);

            if (account.DefaultInboxFolder != null)
            {
                account.DefaultInboxFolderId = connection.Find<Folder>(x => x.AccountId == accountId && x.FullName == account.DefaultInboxFolder.FullName).Id;
            }
        }

        public async Task AddAccountAsync(Account accountData, CancellationToken cancellationToken)
        {
            if (await ExistsAccountWithEmailAddressAsync(accountData.Email, cancellationToken).ConfigureAwait(false))
            {
                throw new AccountAlreadyExistInDatabaseException();
            }

            await WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                connection.Insert(accountData);
                int accountId = GetLastRowId(connection);

                AddAccountAuthData(connection, accountId, accountData.AuthData);
                AddAccountFolders(db, accountId, accountData, ct);

                if (accountData.DefaultInboxFolder != null)
                {
                    connection.Update(accountData);
                }

            }, cancellationToken).ConfigureAwait(false);
        }

        private int InsertOrUpdateEmailAddress(SQLiteConnection connection, EmailAddress email)
        {
            var emailData = FindEmailAddress(connection, email);
            if (emailData != null)
            {
                emailData.UpdateValue(email);
                connection.Update(emailData);
                InvalidateEmailAddressCache(emailData.Id);
                return emailData.Id;
            }
            connection.Insert(new EmailAddressData(email));
            return GetLastRowId(connection);
        }

        public Task DeleteAccountAsync(Account accountData, CancellationToken cancellationToken)
        {
            return DeleteAccountByEmailAsync(accountData.Email, cancellationToken);
        }

        public Task DeleteAccountByEmailAsync(EmailAddress accountEmail, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var item = FindAccountStrict(connection, accountEmail);

                connection.Table<Folder>().Delete(x => x.AccountId == item.Id);
                DeleteAccountAuthData(connection, item.Id);

                connection.Delete(item);
            }, cancellationToken);
        }

        private static void DeleteAccountAuthData(SQLiteConnection connection, int accountId)
        {
            connection.Table<BasicAuthData>().Delete(x => x.AccountId == accountId);
            connection.Table<OAuth2Data>().Delete(x => x.AccountId == accountId);
            connection.Table<ProtonAuthData>().Delete(x => x.AccountId == accountId);
        }

        public Task UpdateAccountAuthAsync(Account account, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var item = FindAccount(connection, account.Email);
                if (item == null)
                {
                    return;
                }
                account.Id = item.Id;

                AddAccountAuthData(connection, account.Id, account.AuthData);

                connection.Update(account);
            }, cancellationToken);
        }

        public Task UpdateAccountAsync(Account account, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var item = FindAccount(connection, account.Email);
                if (item == null)
                {
                    return;
                }
                account.Id = item.Id;
                connection.Update(account);
            }, cancellationToken);
        }

        public Task UpdateAccountFolderStructureAsync(Account account, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var item = FindAccount(connection, account.Email);
                if (item == null)
                {
                    return;
                }
                AddAccountFolders(db, item.Id, account, ct);
                connection.Update(item);
            }, cancellationToken);
        }


        private static void BuildAccountAuthData(SQLiteConnection connection, Account account)
        {
            account.AuthData = account.AuthData ?? connection.Find<BasicAuthData>(x => x.AccountId == account.Id);
            account.AuthData = account.AuthData ?? connection.Find<OAuth2Data>(x => x.AccountId == account.Id);
            account.AuthData = account.AuthData ?? connection.Find<ProtonAuthData>(x => x.AccountId == account.Id);
        }

        private static void BuildAccountFolders(SQLiteConnection connection, Account account)
        {
            var folders = connection.Table<Folder>().Where(x => x.AccountId == account.Id).ToList();
            foreach (var folder in folders)
            {
                folder.AccountEmail = account.Email;
            }
            account.FoldersStructure = folders;
            account.DefaultInboxFolder = folders.FirstOrDefault(x => x.Id == account.DefaultInboxFolderId);
        }

        public Task<Account> GetAccountAsync(EmailAddress accountEmail, CancellationToken cancellationToken)
        {
            Debug.Assert(accountEmail != null);
            return ReadDatabaseAsync((connection, ct) =>
            {
                var account = FindAccountStrict(connection, accountEmail);
                BuildAccountAuthData(connection, account);
                BuildAccountFolders(connection, account);
                return account;
            }, cancellationToken);
        }

        public Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var accounts = connection.Table<Account>().ToList(ct);
                foreach (var account in accounts)
                {
                    ct.ThrowIfCancellationRequested();
                    BuildAccountAuthData(connection, account);
                    BuildAccountFolders(connection, account);
                }
                return accounts;
            }, cancellationToken);
        }

        private void DeleteMessage(DbConnection db, Entities.Message message, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            var connection = db.Connection;
            connection.Delete(message);
            UpdateFolderCounters(connection, null, message, updateUnreadAndTotal);
            UpdateContactsUnreadCount(connection, null, message);
            DeleteMessageContact(db, message, cancellationToken);
            DeleteAttachments(connection, message);
            DeleteMessageEmails(connection, message);
            DeleteProtection(connection, message);
        }

        private void DeleteMessageContact(DbConnection db, Entities.Message message, CancellationToken cancellationToken)
        {
            var connection = db.Connection;
            var messageToContact = connection.Table<MessageContact>()
                                             .Where(x => x.MessageId == message.Pk)
                                             .Select(x => x.ContactId);
            var contacts = connection.Table<Contact>().Where(x => messageToContact.Contains(x.Id));
            foreach (var contact in contacts)
            {
                BuildContact(connection, contact, cancellationToken);
                var messages = GetEarlierContactMessages(connection, contact.Email, 1, null, cancellationToken);
                if (messages.Count > 0)
                {
                    contact.LastMessageData = CreateLastMessageData(messages[0].Folder.AccountEmail, messages[0]);
                    UpdateContact(db, contact);
                }
            }

            connection.Table<MessageContact>().Delete(x => x.MessageId == message.Pk);

        }

        private static void DeleteAttachments(SQLiteConnection connection, Entities.Message message)
        {
            connection.Table<Attachment>().Delete(x => x.MessageId == message.Pk);
        }

        private static void InitMessageFolder(SQLiteConnection connection, EmailAddress accountEmail, string folderPath, Entities.Message message, Entities.Message oldMessage = null)
        {
            if (String.IsNullOrEmpty(folderPath))
            {
                throw new ArgumentException($"message folder path should be specified");
            }
            var folder = FindAccountFolderStrict(connection, accountEmail, folderPath);
            message.FolderId = folder.Id;
            message.Folder = folder;
        }

        private static Folder FindAccountFolderStrict(SQLiteConnection connection, EmailAddress accountEmail, string folderName)
        {
            var folder = FindAccountFolder(connection, accountEmail, folderName);
            if (folder is null)
            {
                throw new DataBaseException($"Folder '{folderName}' doesn't exist");
            }
            folder.AccountEmail = accountEmail;
            return folder;
        }

        private static Folder FindAccountFolder(SQLiteConnection connection, EmailAddress accountEmail, string folderName)
        {
            var account = FindAccountStrict(connection, accountEmail);
            var folder = connection.Find<Folder>(x => x.AccountId == account.Id &&
                                                      x.FullName == folderName);
            return folder;
        }

        private static void UpdateFolderCounters(SQLiteConnection connection,
                                                 Entities.Message newMessage,
                                                 Entities.Message oldMessage = null,
                                                 bool updateUnreadAndTotal = true)
        {
            int folderId = (newMessage ?? oldMessage).FolderId;
            var folder = connection.Find<Folder>(folderId);
            if (folder is null)
            {
                return;
            }
            int delta = 0;
            if (oldMessage is null)
            {
                delta = 1;
            }
            else if (newMessage is null)
            {
                delta = -1;
            }
            if (updateUnreadAndTotal)
            {
                UpdateFolderUnreadCount(connection, folder, newMessage, oldMessage);
                folder.TotalCount += delta;
                Debug.Assert(folder.UnreadCount <= folder.TotalCount);
            }
            folder.LocalCount += delta;
            Debug.Assert(!updateUnreadAndTotal || updateUnreadAndTotal && folder.LocalCount <= folder.TotalCount);

            connection.Update(folder);
        }

        private static void UpdateFolderUnreadCount(SQLiteConnection connection,
                                                    Folder folder,
                                                    Entities.Message newMessage,
                                                    Entities.Message oldMessage)
        {
            Debug.Assert(folder != null);
            var delta = GetUnreadChanges(newMessage, oldMessage);
            if (delta == 0)
            {
                return;
            }
            folder.UnreadCount += delta;
            Debug.Assert(folder.UnreadCount >= 0);
        }

        private static void UpdateContactsUnreadCount(SQLiteConnection connection, Entities.Message newMessage, Entities.Message oldMessage = null)
        {
            if (newMessage is null && oldMessage is null)
            {
                return;
            }

            int folderId = (newMessage ?? oldMessage).FolderId;
            var folder = connection.Find<Folder>(folderId);
            if (folder is null || (!folder.IsInbox && !folder.IsSent))
            {
                // Actually, only INBOX and SENT messages are taken into account when we collect contacts
                return;
            }

            var delta = GetUnreadChanges(newMessage, oldMessage);
            if (delta == 0)
            {
                return;
            }

            // message should be in database
            int messageId = (newMessage ?? oldMessage).Pk;
            var messageContactPair = connection.Table<MessageContact>()
                                               .Where(x => x.MessageId == messageId)
                                               .Deferred()
                                               .Select(x => x.ContactId).ToList();
            var contacts = connection.Table<Contact>()
                                     .Where(x => messageContactPair.Contains(x.Id))
                                     .Deferred();
            foreach (var contact in contacts)
            {
                contact.UnreadCount += delta;
                connection.Update(contact);

                Debug.Assert(contact.UnreadCount >= 0);
            }
        }

        private static int GetUnreadChanges(Entities.Message newMessage, Entities.Message oldMessage)
        {
            if (oldMessage != null &&
                newMessage != null &&
                newMessage.IsMarkedAsRead == oldMessage.IsMarkedAsRead)
            {
                return 0;
            }
            if (newMessage != null && !newMessage.IsMarkedAsRead)
            {
                return 1;
            }
            if (oldMessage != null && !oldMessage.IsMarkedAsRead)
            {
                return -1;
            }
            return 0;
        }

        private static void InsertAttachments(SQLiteConnection connection, Entities.Message message)
        {
            DeleteAttachments(connection, message);

            message.Attachments.ForEach(x => x.MessageId = message.Pk);
            connection.InsertAll(message.Attachments);
        }

        private static void DeleteMessageEmails(SQLiteConnection connection, Entities.Message message)
        {
            connection.Table<MessageEmailAddress>().Delete(x => x.Message == message.Pk);
        }

        private void InsertMessageEmail(SQLiteConnection connection, MessageEmailAddress.EmailType type, EmailAddress email, int messagePk)
        {
            connection.Insert(new MessageEmailAddress
            {
                Type = type,
                Email = InsertOrUpdateEmailAddress(connection, email),
                Message = messagePk,
            });
        }

        private void InsertMessageEmails(SQLiteConnection connection, Entities.Message message)
        {
            DeleteMessageEmails(connection, message);

            message.From.ForEach(x => InsertMessageEmail(connection, MessageEmailAddress.EmailType.From, x, message.Pk));
            message.ReplyTo.ForEach(x => InsertMessageEmail(connection, MessageEmailAddress.EmailType.ReplyTo, x, message.Pk));
            message.To.ForEach(x => InsertMessageEmail(connection, MessageEmailAddress.EmailType.To, x, message.Pk));
            message.Cc.ForEach(x => InsertMessageEmail(connection, MessageEmailAddress.EmailType.Cc, x, message.Pk));
            message.Bcc.ForEach(x => InsertMessageEmail(connection, MessageEmailAddress.EmailType.Bcc, x, message.Pk));
        }

        private static void DeleteProtection(SQLiteConnection connection, Entities.Message message)
        {
            connection.Table<ProtectionInfo>().Where(x => x.MessageId == message.Pk).ToList().ForEach(x =>
            {
                connection.Table<SignatureInfo>().Delete(y => y.ProtectionId == x.Id);
                connection.Delete(x);
            });
        }

        private static void InsertProtection(SQLiteConnection connection, Entities.Message message)
        {
            DeleteProtection(connection, message);

            if (message.Protection != null)
            {
                message.Protection.MessageId = message.Pk;
                connection.Insert(message.Protection);
                int protectionId = GetLastRowId(connection);

                if (message.Protection.SignaturesInfo != null)
                {
                    message.Protection.SignaturesInfo.ForEach(x => { x.ProtectionId = protectionId; connection.Insert(x); });
                }
            }
        }

        private void InsertMessageContact(DbConnection db, EmailAddress accountEmail, Entities.Message message, CancellationToken cancellationToken)
        {
            var connection = db.Connection;
            var contactEmails = message.GetContactEmails(accountEmail);

            foreach (var contactEmail in contactEmails)
            {
                var contact = FindContactByEmail(connection, contactEmail);
                if (contact is null)
                {
                    contact = new Contact(contactEmail.Name, contactEmail);
                    contact.LastMessageData = CreateLastMessageData(accountEmail, message);
                    InsertContact(db, contact, cancellationToken);
                }
                else
                {
                    BuildContact(connection, contact, cancellationToken);
                    if (contact.LastMessageData is null ||
                        message.Date > contact.LastMessageData.Date)
                    {
                        contact.LastMessageData = CreateLastMessageData(accountEmail, message);
                        UpdateContact(db, contact);
                    }
                }

                connection.Insert(new MessageContact() { MessageId = message.Pk, ContactId = contact.Id });
            }
        }

        private static LastMessageData CreateLastMessageData(EmailAddress accountEmail, Message message)
        {
            return new LastMessageData(message.Folder.AccountId, accountEmail, message.Id, message.Date);
        }

        public async Task<List<DecMessage>> GetDecMessagesAsync(EmailAddress email, Folder folder, int count, CancellationToken cancellationToken)
        {
            try
            {
                var path = CreatePath(email, folder.FullName);
                var query = Database.Table<DecMessage>().Where(x => x.Path == path)
                                                        .OrderByDescending(x => x.Id);
                if (count != 0)
                {
                    query = query.Take(count);
                }
                return await query.ToListAsync().ConfigureAwait(false);
            }
            catch (SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }

        public async Task<DecMessage> GetDecMessageAsync(EmailAddress email, Folder folder, uint id, CancellationToken cancellationToken)
        {
            try
            {
                var path = CreatePath(email, folder.FullName);

                return await Database.FindAsync<DecMessage>(x => x.Path == path && x.Id == id).ConfigureAwait(false);
            }
            catch (SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }

        public async Task<DecMessage> UpdateDecMessageAsync(EmailAddress email, DecMessage message, CancellationToken cancellationToken)
        {
            try
            {
                message.Path = CreatePath(email, message.FolderName);

                await Database.UpdateAsync(message).ConfigureAwait(false);

                return message;
            }
            catch (SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }

        public async Task DeleteDecMessageAsync(EmailAddress email, Folder folder, uint id, CancellationToken cancellationToken)
        {
            try
            {
                var path = CreatePath(email, folder.FullName);
                var message = await Database.FindAsync<DecMessage>(x => x.Path == path && x.Id == id).ConfigureAwait(false);
                await Database.DeleteAsync(message).ConfigureAwait(false);
            }
            catch (SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }

        public async Task<bool> IsDecMessageExistsAsync(EmailAddress email, string folder, string hash, CancellationToken cancellationToken)
        {
            try
            {
                var path = CreatePath(email, folder);

                return await Database.FindAsync<DecMessage>(x => x.Path == path && x.Hash == hash).ConfigureAwait(false) != null;
            }
            catch (SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }

        public async Task<DecMessage> AddDecMessageAsync(EmailAddress email, DecMessage message, CancellationToken cancellationToken)
        {
            await WriteDatabaseAsync((db, ct) =>
            {
                var t = db.Connection;
                message.Path = CreatePath(email, message.FolderName);

                t.Insert(message);
                message.Id = (uint)GetLastRowId(t);

            }, cancellationToken).ConfigureAwait(false);
            return message;
        }

        public async Task AddMessageAsync(EmailAddress accountEmail, Entities.Message message, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            string folderPath = message.Folder.FullName;
            if (await IsMessageExistAsync(accountEmail, folderPath, message.Id, cancellationToken).ConfigureAwait(false))
            {
                throw new MessageAlreadyExistInDatabaseException();
            }

            await WriteDatabaseAsync((db, ct) =>
            {
                message.Path = CreatePath(accountEmail, folderPath);
                AddMessage(db, accountEmail, folderPath, message, updateUnreadAndTotal, ct);

            }, cancellationToken).ConfigureAwait(false);
        }

        private void AddMessage(DbConnection db, EmailAddress accountEmail, string folder, Entities.Message message, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var connection = db.Connection;
            InitMessageFolder(connection, accountEmail, folder, message);
            connection.Insert(message);
            message.Pk = GetLastRowId(connection);

            InsertAttachments(connection, message);
            InsertMessageEmails(connection, message);
            InsertProtection(connection, message);
            UpdateFolderCounters(connection, message, null, updateUnreadAndTotal);

            if (message.Folder.IsInbox ||
                message.Folder.IsSent)
            {
                // Actually, only INBOX and SENT messages are taken into account when we collect contacts
                InsertMessageContact(db, accountEmail, message, cancellationToken);
                UpdateContactsUnreadCount(connection, message);
            }
        }

        public Task UpdateMessageAsync(EmailAddress accountEmail, Entities.Message message, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            Debug.Assert(message != null);
            Debug.Assert(message.Pk != 0);
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                message.Path = CreatePath(accountEmail, message.Folder.FullName);

                var item = connection.Find<Entities.Message>(message.Pk);
                if (item is null)
                {
                    throw new DataBaseException($"There is no message with primary key: {message.Pk}");
                }
                InitMessageFolder(connection, accountEmail, message.Folder.FullName, message, item);

                UpdateMessageTextFields(message, item);

                connection.Update(message);

                InsertAttachments(connection, message);
                InsertMessageEmails(connection, message);
                InsertProtection(connection, message);
                UpdateFolderCounters(connection, message, item, updateUnreadAndTotal);
                UpdateContactsUnreadCount(connection, message, item);

            }, cancellationToken);
        }

        private static void UpdateMessageTextFields(Message message, Message item)
        {
            if (string.IsNullOrEmpty(message.PreviewText))
            {
                message.PreviewText = item.PreviewText;
            }

            if (string.IsNullOrEmpty(message.TextBodyProcessed))
            {
                message.TextBodyProcessed = item.TextBodyProcessed;
            }
        }

        public async Task UpdateMessagesAsync(EmailAddress email, IEnumerable<Entities.Message> messages, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            foreach (var message in messages)
            {
                await UpdateMessageAsync(email, message, updateUnreadAndTotal, cancellationToken).ConfigureAwait(false);
            }
        }

        public Task UpdateMessagesFlagsAsync(EmailAddress email, IEnumerable<Entities.Message> messages, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                foreach (var message in messages)
                {
                    ct.ThrowIfCancellationRequested();
                    var connection = db.Connection;
                    var item = connection.Find<Entities.Message>(x => x.Path == message.Path && x.Id == message.Id);
                    if (item is null)
                    {
                        continue;
                    }
                    UpdateFolderCounters(connection, message, item, updateUnreadAndTotal);
                    UpdateContactsUnreadCount(connection, message, item);
                    item.IsFlagged = message.IsFlagged;
                    item.IsMarkedAsRead = message.IsMarkedAsRead;
                    connection.Update(message);
                }
            }, cancellationToken);
        }

        public Task AddMessageListAsync(EmailAddress email, string folder, IReadOnlyList<Entities.Message> messages, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            if (messages.Count == 0)
            {
                return Task.CompletedTask;
            }
            return WriteDatabaseAsync((db, ct) =>
            {
                var path = CreatePath(email, folder);
                var connection = db.Connection;

                foreach (var message in messages)
                {
                    ct.ThrowIfCancellationRequested();

                    message.Path = path;
                    var exists = connection.Find<Entities.Message>(x => x.Path == path && x.Id == message.Id);
                    if (exists is null)
                    {
                        AddMessage(db, email, folder, message, updateUnreadAndTotal, ct);
                    }
                    else
                    {
                        // TODO: fix this situation (this appears when synchronization and get earlier messages are running simultaneously)
                        // Collision: message already stored – ignore second copy
                        Logger.LogWarning("Collision: message already stored – ignore second copy.");
                        continue;
                    }
                }
            }, cancellationToken);
        }

        private Entities.Message BuildMessage(SQLiteConnection connection, Entities.Message message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadMessageEmailAddresses(connection, message, cancellationToken);
            return BuildMessageWithoutEmailAddresses(connection, message, cancellationToken);
        }
        private static Entities.Message BuildMessageWithoutEmailAddresses(SQLiteConnection connection, Entities.Message message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            message.Attachments.AddRange(connection.Table<Attachment>().Where(x => x.MessageId == message.Pk));
            cancellationToken.ThrowIfCancellationRequested();
            message.Folder = connection.Find<Folder>(message.FolderId);
            if (message.Folder != null)
            {
                var account = connection.Find<Account>(message.Folder.AccountId);
                message.Folder.AccountEmail = account.Email;
            }
            cancellationToken.ThrowIfCancellationRequested();
            message.Protection = connection.Find<ProtectionInfo>(x => x.MessageId == message.Pk);
            cancellationToken.ThrowIfCancellationRequested();
            if (message.Protection != null)
            {
                message.Protection.SignaturesInfo = connection.Table<SignatureInfo>()
                                                              .Where(x => x.ProtectionId == message.Protection.Id)
                                                              .ToList(cancellationToken);
            }
            return message;
        }

        private Dictionary<int, EmailAddressData> _emailAddressDataCache = new Dictionary<int, EmailAddressData>();
        private EmailAddressData GetEmailAddressData(SQLiteConnection connection, int emailId)
        {
            lock (_emailAddressDataCache)
            {
                EmailAddressData emailData = null;
                if (!_emailAddressDataCache.TryGetValue(emailId, out emailData))
                {
                    emailData = connection.Find<EmailAddressData>(emailId);
                    if (emailData != null)
                    {
                        _emailAddressDataCache.Add(emailId, emailData);
                    }
                }
                return emailData;
            }
        }

        private void InvalidateEmailAddressCache(int emailId)
        {
            lock (_emailAddressDataCache)
            {
                _emailAddressDataCache.Remove(emailId);
            }
        }

        private Entities.Message LoadMessageEmailAddresses(SQLiteConnection connection, Entities.Message message, CancellationToken cancellationToken)
        {
            var emails = connection.Table<MessageEmailAddress>()
                                   .Where(x => x.Message == message.Pk);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var x in emails)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var email = GetEmailAddressData(connection, x.Email).ToEmailAddress();
                switch (x.Type)
                {
                    case MessageEmailAddress.EmailType.From:
                        message.From.Add(email);
                        break;
                    case MessageEmailAddress.EmailType.ReplyTo:
                        message.ReplyTo.Add(email);
                        break;
                    case MessageEmailAddress.EmailType.To:
                        message.To.Add(email);
                        break;
                    case MessageEmailAddress.EmailType.Cc:
                        message.Cc.Add(email);
                        break;
                    case MessageEmailAddress.EmailType.Bcc:
                        message.Bcc.Add(email);
                        break;
                }
            }
            return message;
        }

        private List<Entities.Message> BuildMessages(SQLiteConnection connection, IEnumerable<Entities.Message> items, CancellationToken cancellationToken)
        {
            var messages = new List<Entities.Message>();
            foreach (var message in items)
            {
                messages.Add(BuildMessage(connection, message, cancellationToken));
            }
            return messages;
        }

        public Task<Entities.Message> GetMessageAsync(EmailAddress email, string folder, uint id, bool fast, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var path = CreatePath(email, folder);
                var message = connection.Find<Entities.Message>(x => x.Path == path && x.Id == id);
                if (message is null)
                {
                    return null;
                }
                if (fast)
                {
                    return message;
                }
                return BuildMessage(connection, message, ct);
            }, cancellationToken);
        }

        public Task<IReadOnlyList<Entities.Message>> GetMessageListAsync(EmailAddress email, string folder, uint count, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var path = CreatePath(email, folder);

                var items = (count == 0)
                    ? connection.Table<Entities.Message>().Where(x => x.Path == path)
                    : connection.Table<Entities.Message>().Where(x => x.Path == path)
                                .OrderByDescending(x => x.Id)
                                .Take((int)count);
                return (IReadOnlyList<Entities.Message>)BuildMessages(connection, items, ct);
            }, cancellationToken);
        }

        public Task<IReadOnlyList<Entities.Message>> GetMessageListAsync(EmailAddress email, string folder, uint fromUid, uint count, bool fast, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var path = CreatePath(email, folder);

                var items = connection.Table<Entities.Message>()
                                      .Where(x => x.Path == path && x.Id < fromUid)
                                      .OrderByDescending(x => x.Id)
                                      .Take((int)count)
                                      .ToList(ct);

                if (fast)
                {
                    return (IReadOnlyList<Entities.Message>)items;
                }
                return (IReadOnlyList<Entities.Message>)BuildMessages(connection, items, ct);
            }, cancellationToken);
        }

        public Task<IReadOnlyList<Entities.Message>> GetMessageListAsync(EmailAddress email, string folder, (uint, uint) range, bool fast, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                uint min = Math.Min(range.Item1, range.Item2);
                uint max = Math.Max(range.Item1, range.Item2);
                var path = CreatePath(email, folder);
                var items = connection.Table<Entities.Message>()
                                      .Where(x => x.Path == path && x.Id < max && x.Id >= min)
                                      .OrderByDescending(x => x.Id)
                                      .Deferred();
                if (fast)
                {
                    return (IReadOnlyList<Entities.Message>)items.ToList(ct);
                }

                var messages = new List<Entities.Message>();
                foreach (var item in items)
                {
                    messages.Add(BuildMessage(connection, item, ct));
                }
                return (IReadOnlyList<Entities.Message>)messages;
            }, cancellationToken);
        }

        public async Task<uint> GetMessagesCountAsync(EmailAddress email, string folder, CancellationToken cancellationToken = default)
        {
            try
            {
                var path = CreatePath(email, folder);
                return (uint)await Database.Table<Entities.Message>().Where(x => x.Path == path).CountAsync().ConfigureAwait(false);
            }
            catch (SQLiteException exp)
            {
                throw new DataBaseException(exp.Message, exp);
            }
        }

        public Task<Entities.Message> GetLatestMessageAsync(EmailAddress email, string folder, CancellationToken cancellationToken = default)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var path = CreatePath(email, folder);
                var item = connection.Table<Entities.Message>()
                                     .Where(x => x.Path == path)
                                     .ThenByDescending(x => x.Id)
                                     .FirstOrDefault();
                return item is null ? null : BuildMessage(connection, item, ct);
            }, cancellationToken);
        }

        public Task<Entities.Message> GetEarliestMessageAsync(EmailAddress email, string folder, CancellationToken cancellationToken = default)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var path = CreatePath(email, folder);
                var item = connection.Table<Entities.Message>()
                                     .Where(x => x.Path == path)
                                     .ThenBy(x => x.Id)
                                     .FirstOrDefault();
                return item is null ? null : BuildMessage(connection, item, ct);
            }, cancellationToken);
        }

        public Task<Entities.Message> GetEarliestMessageAsync(Folder folder, CancellationToken cancellationToken = default)
        {
            Debug.Assert(folder.Id > 0);
            return ReadDatabaseAsync((connection, ct) =>
            {
                var item = connection.Table<Entities.Message>()
                                     .Where(x => x.FolderId == folder.Id)
                                     .ThenBy(x => x.Id)
                                     .FirstOrDefault();
                return item is null ? null : BuildMessage(connection, item, ct);
            }, cancellationToken);
        }

        public Task<IReadOnlyList<Entities.Message>> GetEarlierMessagesInFoldersAsync(IEnumerable<Folder> folders,
                                                                             int count,
                                                                             Entities.Message lastMessage,
                                                                             CancellationToken cancellationToken)
        {
            var folderIds = folders.Select(x => x.Id).ToList(cancellationToken);
            if (folderIds.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<Entities.Message>>(new List<Entities.Message>());
            }
            return ReadDatabaseAsync((connection, ct) =>
            {

                Func<IEnumerable<Entities.Message>> getItemsQuery = () =>
                {
                    if (folderIds.Count == 1)
                    {
                        int folderId = folderIds[0];
                        Expression<Func<Entities.Message, bool>> filter = x => x.FolderId == folderId;
                        if (lastMessage != null)
                        {
                            filter = x => x.FolderId == folderId &&
                            (x.Date < lastMessage.Date || (x.Date == lastMessage.Date && x.Id < lastMessage.Id));
                        }
                        return connection.Table<Entities.Message>()
                                         .Where(filter)
                                         .OrderByDescending(x => x.Date)
                                         .ThenByDescending(x => x.Id)
                                         .Take(count).Deferred();
                    }
                    string folderIdsStr = string.Join(",", folderIds.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray());
                    // We need to join Message and Folder tables here,
                    // but SQLite.Net doesn't support joins, so we do them manually
                    // Here we get the latest from the earliest messages in INBOX folders which are not fully loaded
                    string messageQuery = $@"select * from (
select *, min(Date) from Message 
INNER JOIN Folder ON Message.FolderId=Folder.Id
WHERE Folder.Id in ({folderIdsStr}) and Folder.LocalCount < Folder.TotalCount
GROUP BY FolderId
) ORDER BY Date DESC";

                    var earliestFolderMessages = connection.Query<Entities.Message>(messageQuery).FirstOrDefault();
                    if (earliestFolderMessages is null)
                    {
                        // Everything is loaded, so we don't limit messages

                        // We need to join Message and Folder tables here,
                        // but SQLite.Net doesn't support joins, so we do them manually
                        // Note, different folder may contain the same Id,
                        // to keep order stable and deterministic, first we sort by Date, THEN by folder id and only afterwards by message id

                        if (lastMessage is null)
                        {
                            // Join tables and query without message filtering
                            string firstQuery = $@"select Message.*, Folder.Attributes from Message 
INNER JOIN Folder ON Message.FolderId=Folder.Id
WHERE Folder.Id in ({folderIdsStr})
ORDER BY Date DESC, FolderId ASC, Message.Id DESC LIMIT ?1";
                            return connection.DeferredQuery<Entities.Message>(firstQuery, count);
                        }
                        // Join tables and filter by message Date and Id
                        string nextQuery = $@"select Message.*, Folder.Attributes from Message 
INNER JOIN Folder ON Message.FolderId=Folder.Id
WHERE Folder.Id in ({folderIdsStr}) and (Date < ?1 or (Date = ?1 and ((FolderId=?2 and Message.Id < ?3) or FolderId > ?2 )))
ORDER BY Date DESC, FolderId ASC, Message.Id DESC LIMIT ?4";
                        return connection.DeferredQuery<Entities.Message>(nextQuery,
                                                                 lastMessage.Date,
                                                                 lastMessage.FolderId,
                                                                 lastMessage.Id,
                                                                 count);
                    }

                    // We have to get the messages before the latest from the earliest loaded messages

                    // We need to join Message and Folder tables here,
                    // but SQLite.Net doesn't support joins, so we do them manually
                    // Note, different folder may contain the same Id,
                    // to keep order stable and deterministic, first we sort by Date, THEN by folder id and only afterwards by message id

                    if (lastMessage is null)
                    {
                        // Join tables and query without message filtering
                        string query = $@"select Message.*, Folder.Attributes from Message 
INNER JOIN Folder ON Message.FolderId=Folder.Id
WHERE Folder.Id in ({folderIdsStr})  AND Date >= ?2
ORDER BY Date DESC, FolderId ASC, Message.Id DESC LIMIT ?1";
                        return connection.DeferredQuery<Entities.Message>(query, count, earliestFolderMessages.Date);
                    }
                    // Join tables and filter by message Date and Id
                    string query2 = $@"select Message.*, Folder.Attributes from Message 
INNER JOIN Folder ON Message.FolderId=Folder.Id
WHERE Folder.Id in ({folderIdsStr}) and (Date < ?1 or (Date = ?1 and ((FolderId=?2 and Message.Id < ?3) or FolderId > ?2 ))) AND Date >= ?5
ORDER BY Date DESC, FolderId ASC, Message.Id DESC LIMIT ?4";
                    return connection.DeferredQuery<Entities.Message>(query2,
                                                             lastMessage.Date,
                                                             lastMessage.FolderId,
                                                             lastMessage.Id,
                                                             count,
                                                             earliestFolderMessages.Date);
                };
                var items = getItemsQuery();
                var messages = new List<Entities.Message>(count);
                foreach (var item in items)
                {
                    ct.ThrowIfCancellationRequested();
                    messages.Add(BuildMessage(connection, item, ct));
                }
                return (IReadOnlyList<Entities.Message>)messages;
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<Entities.Message>> GetEarlierMessagesAsync(Folder folder,
                                                                          int count,
                                                                          Entities.Message lastMessage,
                                                                          CancellationToken cancellationToken)
        {
            var folders = new List<Folder>();
            if (folder is null)
            {
                folders.AddRange(await ReadDatabaseAsync<IReadOnlyList<Folder>>((connection, ct) =>
                {
                    return connection.Table<Folder>()
                                     .Where(x => x.Attributes == FolderAttributes.Inbox)
                                     .ToList(ct);
                }, cancellationToken).ConfigureAwait(false));
            }
            else
            {
                folders.Add(folder);
            }
            return await GetEarlierMessagesInFoldersAsync(folders, count, lastMessage, cancellationToken).ConfigureAwait(false);
        }

        public Task<IReadOnlyList<Entities.Message>> GetEarlierContactMessagesAsync(EmailAddress contactEmail,
                                                                                    int count,
                                                                                    Entities.Message lastMessage,
                                                                                    CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                return GetEarlierContactMessages(connection, contactEmail, count, lastMessage, ct);
            }, cancellationToken);
        }

        private IReadOnlyList<Entities.Message> GetEarlierContactMessages(SQLiteConnection connection,
                                                                          EmailAddress contactEmail,
                                                                          int count,
                                                                          Entities.Message lastMessage,
                                                                          CancellationToken cancellationToken)
        {
            var messages = new List<Entities.Message>(count);
            var contact = FindContactByEmail(connection, contactEmail);
            if (contact is null)
            {
                return messages;
            }

            Func<IEnumerable<Entities.Message>> getItemsQuery = () =>
            {
                // We need to join Message and Folder tables here,
                // but SQLite.Net doesn't support joins, so we do them manually
                // Note, different folder may contain the same Id,
                // to keep order stable and deterministic, first we sort by Date, THEN by folder id and only afterwards by message id
                if (lastMessage is null)
                {
                    // Join tables and query without message filtering
                    const string query = @"select Message.* from MessageContact 
INNER JOIN Message ON Message.Pk=MessageContact.MessageId
WHERE ContactId = ?1
ORDER BY Date DESC, FolderId ASC, Message.Id DESC ";
                    return connection.DeferredQuery<Entities.Message>(query, contact.Id);
                }
                // Join tables and filter by message Date and Id
                const string query2 = @"select Message.* from MessageContact 
INNER JOIN Message ON Message.Pk=MessageContact.MessageId
WHERE ContactId = ?1 and (Date < ?2 or (Date = ?2 and ((FolderId=?3 and Message.Id < ?4) or FolderId > ?3))) 
ORDER BY Date DESC, FolderId ASC, Message.Id DESC";
                return connection.DeferredQuery<Entities.Message>(query2, contact.Id, lastMessage.Date, lastMessage.FolderId, lastMessage.Id);
            };

            foreach (var item in getItemsQuery())
            {
                cancellationToken.ThrowIfCancellationRequested();
                LoadMessageEmailAddresses(connection, item, cancellationToken);

                Debug.Assert(item.IsFromCorrespondenceWithContact(contactEmail), "This shouldn't happen, IsFromCorrespondenceWithContact is equivalent of SQL query");

                var message = BuildMessageWithoutEmailAddresses(connection, item, cancellationToken);
                if (message.Folder != null &&
                    (message.Folder.IsJunk || message.Folder.IsTrash))
                {
                    continue;
                }
                messages.Add(message);
                if (messages.Count >= count)
                {
                    break;
                }
            }

            return messages;
        }

        public Task<Entities.Message> GetContactLastMessageAsync(EmailAddress email, string folder, EmailAddress contactEmail, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var path = CreatePath(email, folder);
                var items = connection.Table<Entities.Message>().Where(x => x.Path == path).OrderByDescending(x => x.Id);
                var messages = BuildMessages(connection, items, ct).Where(message => message.IsFromCorrespondenceWithContact(email, contactEmail))
                                                                   .OrderByDescending(message => message.Date);

                return messages.Any() ? messages.First() : null;
            }, cancellationToken);
        }

        public Task<bool> IsMessageExistAsync(EmailAddress email, string folderName, uint uid, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var path = CreatePath(email, folderName);
                var message = connection.Find<Entities.Message>(x => x.Path == path && x.Id == uid);

                return message != null;
            }, cancellationToken);
        }

        public Task DeleteFolderAsync(EmailAddress email, string folder, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var path = CreatePath(email, folder);
                var messages = connection.Table<Entities.Message>().Where(x => x.Path == path);

                foreach (var message in messages)
                {
                    DeleteMessage(db, message, updateUnreadAndTotal: true, ct);
                }
            }, cancellationToken);
        }

        public Task DeleteMessageAsync(EmailAddress email, string folder, uint uid, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            return DeleteMessagesAsync(email, folder, new List<uint> { uid }, updateUnreadAndTotal, cancellationToken);
        }

        public Task DeleteMessagesAsync(EmailAddress email, string folder, IEnumerable<uint> uids, bool updateUnreadAndTotal, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var path = CreatePath(email, folder);

                foreach (var uid in uids)
                {
                    var message = connection.Find<Entities.Message>(x => x.Path == path && x.Id == uid);
                    if (message != null)
                    {
                        DeleteMessage(db, message, updateUnreadAndTotal, ct);
                    }
                }
            }, cancellationToken);
        }

        public Task<bool> ExistsAccountWithEmailAddressAsync(EmailAddress email, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                return ExistsAccountWithEmailAddress(connection, email);
            }, cancellationToken);
        }

        private static bool ExistsAccountWithEmailAddress(SQLiteConnection connection, EmailAddress accountEmail)
        {
            return FindAccount(connection, accountEmail) != null;
        }

        private static string CreatePath(EmailAddress email, string folder)
        {
            return email.Address + "\\" + folder;
        }

        public Task AddAccountGroupAsync(AccountGroup group, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                connection.Insert(group);
            }, cancellationToken);
        }

        public Task<IReadOnlyList<AccountGroup>> GetAccountGroupsAsync(CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                return connection.Table<AccountGroup>().ToList(ct) as IReadOnlyList<AccountGroup>;
            }, cancellationToken);
        }

        public Task DeleteAccountGroupAsync(AccountGroup group, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                connection.Delete(group);
            }, cancellationToken);
        }

        public Task<int> GetUnreadMessagesCountAsync(EmailAddress accountEmail, string folder, CancellationToken cancellationToken)
        {
            if (_isDisposed)
            {
                return Task.FromResult(0);
            }
            return ReadDatabaseAsync((connection, ct) =>
            {
                var res = FindAccountFolder(connection, accountEmail, folder);
                if (res is null)
                {
                    return 0; // it seems that folder has been deleted
                }
                return res.UnreadCount;
            }, cancellationToken);
        }

        private void BuildContact(SQLiteConnection connection, Contact contact, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            contact.AvatarInfo = connection.Find<ImageInfo>(contact.AvatarInfoId) ?? ImageInfo.Empty;
            cancellationToken.ThrowIfCancellationRequested();
            contact.LastMessageData = connection.Find<LastMessageData>(contact.LastMessageDataId) ?? contact.LastMessageData;
            if (contact.LastMessageData != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var account = connection.Find<Account>(contact.LastMessageData.AccountId);
                if (account is null)
                {
                    contact.LastMessageData = null; // Account has been deleted, so we remove LastMessageData
                }
                else
                {
                    contact.LastMessageData.AccountEmail = account.Email;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            contact.Email = GetEmailAddressData(connection, contact.EmailId).ToEmailAddress();
        }

        public Task<IEnumerable<Contact>> GetContactsAsync(CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var items = connection.Table<Contact>().ToList(ct);

                foreach (var item in items)
                {
                    BuildContact(connection, item, ct);
                }

                return items.AsEnumerable<Contact>();
            }, cancellationToken);
        }

        public Task<Contact> GetContactAsync(EmailAddress contactEmail, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var item = FindContactByEmail(connection, contactEmail);
                if (item is null)
                {
                    throw new DataBaseException("Invalid email contact request");
                }
                BuildContact(connection, item, ct);
                return item;
            }, cancellationToken);
        }

        private void InsertContactData(SQLiteConnection connection, Contact contact)
        {
            contact.EmailId = InsertOrUpdateEmailAddress(connection, contact.Email);
            if (!contact.AvatarInfo.IsEmpty)
            {
                connection.Insert(contact.AvatarInfo);
                contact.AvatarInfoId = GetLastRowId(connection);
            }

            if (contact.LastMessageData != null)
            {
                var account = FindAccountStrict(connection, contact.LastMessageData.AccountEmail);
                contact.LastMessageData.AccountId = account.Id;
                connection.Insert(contact.LastMessageData);
                contact.LastMessageDataId = GetLastRowId(connection);
            }
        }

        public Task AddContactAsync(Contact contact, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                if (contact is null || contact.Email is null)
                {
                    throw new DataBaseException("Attempt to add invalid contact");
                }
                InsertContact(db, contact, ct);
            }, cancellationToken);
        }

        private void InsertContact(DbConnection db, Contact contact, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var connection = db.Connection;
            InsertContactData(connection, contact);
            connection.Insert(contact);
            db.ChangeState.Add(contact);
        }

        private void UpdateContact(DbConnection db, Contact contact)
        {
            var connection = db.Connection;
            InsertContactData(connection, contact);
            connection.Update(contact);
            db.ChangeState.Update(contact);
        }

        public Task<bool> ExistsContactWithEmailAddressAsync(EmailAddress email, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                return ExistsContactWithEmailAddress(connection, email);
            }, cancellationToken);
        }

        private bool ExistsContactWithEmailAddress(SQLiteConnection connection, EmailAddress email)
        {
            return FindContactByEmail(connection, email) != null;
        }

        private Contact FindContactByEmail(SQLiteConnection connection, EmailAddress email)
        {
            var emailData = FindEmailAddress(connection, email);
            if (emailData is null)
            {
                return null;
            }
            return connection.Find<Contact>(x => x.EmailId == emailData.Id);
        }

        public async Task<bool> TryAddContactAsync(Contact contact, CancellationToken cancellationToken)
        {
            try
            {
                bool res = false;
                await WriteDatabaseAsync((db, ct) =>
                {
                    if (contact is null || contact.Email is null)
                    {
                        // invalid
                        return;
                    }
                    var connection = db.Connection;
                    if (ExistsContactWithEmailAddress(connection, contact.Email))
                    {
                        // already exists
                        return;
                    }
                    InsertContact(db, contact, ct);
                    res = true;
                }, cancellationToken).ConfigureAwait(false);
                return res;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
            }

            return false;
        }

        public Task SetContactAvatarAsync(EmailAddress contactEmail, byte[] avatarBytes, int avatarWidth, int avatarHeight, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var item = FindContactByEmail(connection, contactEmail);

                if (item != null)
                {
                    connection.Insert(new ImageInfo(avatarWidth, avatarHeight, avatarBytes));
                    item.AvatarInfoId = GetLastRowId(connection);

                    connection.Update(item);
                }
            }, cancellationToken);
        }

        public Task UpdateContactAsync(Contact contact, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                contact.Id = connection.Find<Contact>(x => x.EmailId == contact.EmailId).Id;
                UpdateContact(db, contact);
            }, cancellationToken);
        }

        public Task RemoveContactAsync(EmailAddress contactEmail, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var item = FindContactByEmail(connection, contactEmail);
                connection.Delete<ImageInfo>(item.AvatarInfoId);
                connection.Delete<LastMessageData>(item.LastMessageDataId);
                connection.Delete(item);
            }, cancellationToken);
        }

        private Dictionary<EmailAddress, EmailAddressData> _emailAddressesCache2 = new Dictionary<EmailAddress, EmailAddressData>();
        private EmailAddressData FindEmailAddress(SQLiteConnection connection, EmailAddress email)
        {
            lock (_emailAddressesCache2)
            {
                EmailAddressData emailData = null;
                if (_emailAddressesCache2.TryGetValue(email, out emailData))
                {
                    return emailData;
                }
                emailData = connection.Find<EmailAddressData>(y => y.Address == email.Address);
                if (emailData is null)
                {
                    return null;
                }
                _emailAddressesCache2.Add(email, emailData);
                return emailData;
            }
        }

        private EmailAddressData FindEmailAddressStrict(SQLiteConnection connection, EmailAddress email)
        {
            var emailData = FindEmailAddress(connection, email);
            if (emailData == null)
            {
                throw new DataBaseException("Unknown email requested");
            }
            return emailData;
        }

        private static Account FindAccount(SQLiteConnection connection, EmailAddress accountEmail)
        {
#pragma warning disable CS0618 // Only for SQLite and internal using
            return connection.Find<Account>(x => x.EmailAddress == accountEmail.Address);
#pragma warning restore CS0618 // Only for SQLite and internal using
        }

        private static Account FindAccountStrict(SQLiteConnection connection, EmailAddress accountEmail)
        {
            var account = FindAccount(connection, accountEmail);
            if (account == null)
            {
                throw new AccountIsNotExistInDatabaseException();
            }
            return account;
        }

        public Task RemoveContactAvatarAsync(EmailAddress contactEmail, CancellationToken cancellationToken)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var item = FindContactByEmail(connection, contactEmail);

                connection.Delete<ImageInfo>(item.AvatarInfoId);

                item.AvatarInfoId = 0;
                connection.Update(item);
            }, cancellationToken);
        }

        public Task<int> GetContactUnreadMessagesCountAsync(EmailAddress contactEmail, CancellationToken cancellationToken)
        {
            if (_isDisposed)
            {
                return Task.FromResult(0);
            }
            return ReadDatabaseAsync((connection, ct) =>
            {
                var contact = FindContactByEmail(connection, contactEmail);
                if (contact is null)
                {
                    return 0;
                }
                return contact.UnreadCount;
            }, cancellationToken);
        }

        public Task<IReadOnlyDictionary<EmailAddress, int>> GetUnreadMessagesCountByContactAsync(CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var counts = connection.Table<Contact>()
                                       .Deferred()
                                       .Select(x => new { Email = GetEmailAddressData(connection, x.EmailId).ToEmailAddress(), x.UnreadCount })
                                       .ToDictionary(x => x.Email, x => x.UnreadCount);

                return (IReadOnlyDictionary<EmailAddress, int>)counts;
            }, cancellationToken);
        }

        private Task WriteDatabaseAsync(Action<DbConnection, CancellationToken> writeFunc, CancellationToken cancellationToken)
        {
            CheckDisposed();
            return DoCancellableTaskAsync(async (ct) =>
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var changeContext = new ChangeState(this);
                    await Database.RunInTransactionAsync((connection) =>
                    {
                        CheckDisposed();
                        ct.ThrowIfCancellationRequested();
                        var dbConnection = new DbConnection(connection, changeContext);
                        writeFunc(dbConnection, ct);
                    }).ConfigureAwait(false);
                    changeContext.RaiseEvents();
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch (SQLiteException exp)
                {
                    throw new DataBaseException(exp.Message, exp);
                }
            }, cancellationToken);
        }

        private Task<T> ReadDatabaseAsync<T>(Func<SQLiteConnection, CancellationToken, T> readFunc, CancellationToken cancellationToken)
        {
            CheckDisposed();
            return DoCancellableTaskAsync(async (ct) =>
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (Database is null)
                    {
                        throw new DataBaseException("Database is not opened");
                    }
                    T result = default;
                    await Database.RunInTransactionAsync((connection) =>
                    {
                        CheckDisposed();
                        ct.ThrowIfCancellationRequested();
                        result = readFunc(connection, ct);
                    }).ConfigureAwait(false);
                    return result;
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch (SQLiteException exp)
                {
                    throw new DataBaseException(exp.Message, exp);
                }
            }, cancellationToken);
        }

        public async Task<Settings> GetSettingsAsync(CancellationToken cancellationToken)
        {
            var settings = new Settings();

            foreach (var prop in settings.GetType().GetProperties())
            {
                var option = await Database.FindAsync<SettingsTable>(x => x.Key == prop.Name).ConfigureAwait(false);
                prop.SetValue(settings, option != null ? option.Value : 0);
            }

            return settings;
        }

        public async Task SetSettingsAsync(Settings settings, CancellationToken cancellationToken)
        {
            foreach (var prop in settings.GetType().GetProperties())
            {
                var option = new SettingsTable { Key = prop.Name, Value = (int)prop.GetValue(settings) };
                await Database.InsertOrReplaceAsync(option).ConfigureAwait(false);
            }
        }

        public async Task UpdateAccountEmailAsync(EmailAddress prev, EmailAddress email, CancellationToken cancellationToken)
        {
            await WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var messages = connection.Table<Entities.Message>()
                                         .Where(x => x.Path.Contains(prev.Address))
                                         .ToList(ct);
                messages.ForEach(x => x.Path = x.Path.Replace(prev.Address, email.Address));
                connection.UpdateAll(messages);

                var emailData = FindEmailAddress(connection, prev);
                emailData.Address = email.Address;
                connection.Update(emailData);

            }, cancellationToken).ConfigureAwait(false);
        }

        public Task AddOrUpdateMessagesAsync(int accountId, IReadOnlyList<Proton.Message> messages, CancellationToken cancellationToken)
        {
            if (messages.Count == 0)
            {
                return Task.CompletedTask;
            }
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;

                var labelIds = messages.SelectMany(x => x.LabelIds)
                                       .Distinct()
                                       .OrderBy(x => x)
                                       .ToList(ct);
                var storedLabels = connection.Table<ProtonLabelV2>().Where(x => x.AccountId == accountId)
                                                                  .Select(x => x.LabelId)
                                                                  .OrderBy(x => x)
                                                                  .ToList(ct);
                var newLabels = labelIds.Except(storedLabels);
                foreach (var label in newLabels.Select(x => new ProtonLabelV2() { LabelId = x, AccountId = accountId }))
                {
                    ct.ThrowIfCancellationRequested();
                    connection.Insert(label);
                }

                var labelLookup = connection.Table<ProtonLabelV2>().Where(x => x.AccountId == accountId).ToDictionary(x => x.LabelId);

                foreach (var message in messages)
                {
                    int messageId = 0;
                    ct.ThrowIfCancellationRequested();
                    var existingMessage = connection.Table<Proton.Message>().Where(m => m.AccountId == accountId && m.MessageId == message.MessageId).FirstOrDefault();
                    if (existingMessage is null)
                    {
                        message.AccountId = accountId;
                        connection.Insert(message);
                        messageId = GetLastRowId(connection);
                    }
                    else
                    {
                        message.Id = existingMessage.Id;
                        message.AccountId = existingMessage.AccountId;
                        messageId = existingMessage.Id;
                        connection.Update(message);
                        // remove existing labels
                        connection.Table<ProtonMessageLabel>().Delete(x => x.AccountId == accountId && x.MessageId == existingMessage.Id);
                    }
                    foreach (var label in message.LabelIds.Select(x => new ProtonMessageLabel() { MessageId = messageId, LabelId = labelLookup[x].Id, AccountId = accountId }))
                    {
                        connection.Insert(label);
                    }
                }
            }, cancellationToken);
        }

        public Task<IReadOnlyList<Proton.Message>> GetMessagesAsync(int accountId, string labelId, uint knownId, bool getEarlier, int count, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var label = connection.Table<ProtonLabelV2>().Where(x => x.AccountId == accountId && x.LabelId == labelId).FirstOrDefault();
                if (label is null)
                {
                    return new List<Proton.Message>();
                }
                var labeledMessages = connection.Table<ProtonMessageLabel>()
                                                .Where(x => x.AccountId == accountId && x.LabelId == label.Id)
                                                .Select(x => x.MessageId);
                var query = connection.Table<Proton.Message>()
                                      .Where(x => x.AccountId == accountId && labeledMessages.Contains(x.Id))
                                      .OrderByDescending(x => x.Id);
                if (knownId > 0)
                {
                    if (getEarlier)
                    {
                        query = query.Where(x => x.Id < knownId);
                    }
                    else
                    {
                        query = query.Where(x => x.Id > knownId);
                    }
                }

                return GetMessagesImpl(count, ref query, ct);
            }, cancellationToken);
        }

        private static IReadOnlyList<Proton.Message> GetMessagesImpl(int count, ref TableQuery<Proton.Message> query, CancellationToken cancellationToken)
        {
            if (count > 0)
            {
                query = query.Take(count); // NOTE: if count == 0 we return all items
            }
            return (IReadOnlyList<Proton.Message>)query.ToList(cancellationToken);
        }

        public Task<Proton.Message> GetMessageAsync(int accountId, string labelId, uint id, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var label = connection.Table<ProtonLabelV2>().Where(x => x.AccountId == accountId && x.LabelId == labelId).First();
                var labeledMessage = connection.Table<ProtonMessageLabel>()
                                               .Where(x => x.AccountId == accountId && x.LabelId == label.Id && x.MessageId == id)
                                               .FirstOrDefault();
                if (labeledMessage is null)
                {
                    return null;
                }
                return connection.Table<Proton.Message>()
                                 .Where(x => x.AccountId == accountId && x.Id == id)
                                 .FirstOrDefault();
            }, cancellationToken);
        }

        public Task<IReadOnlyList<Proton.Message>> GetMessagesAsync(int accountId, IReadOnlyList<uint> ids, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                return (IReadOnlyList<Proton.Message>)connection.Table<Proton.Message>()
                                                                .Where(x => x.AccountId == accountId && ids.Contains((uint)x.Id))
                                                                .ToList(ct);
            }, cancellationToken);
        }

        public Task AddMessageIDs(int accountId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
            {
                return Task.CompletedTask;
            }
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var table = connection.Table<ProtonMessageIdV2>();
                foreach (var id in ids)
                {
                    ct.ThrowIfCancellationRequested();
                    connection.Insert(new ProtonMessageIdV2() { MessageId = id, AccountId = accountId });
                }
            }, cancellationToken);
        }

        public Task<IReadOnlyList<KeyValuePair<string, uint>>> LoadMessageIDsAsync(int accountId, CancellationToken cancellationToken)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                return (IReadOnlyList<KeyValuePair<string, uint>>)connection.Table<ProtonMessageIdV2>()
                                                                            .Where(x => x.AccountId == accountId)
                                                                            .OrderBy(x => x.Id)
                                                                            .Select(x => new KeyValuePair<string, uint>(x.MessageId, (uint)x.Id))
                                                                            .ToList(ct);
            }, cancellationToken);
        }

        public Task DeleteMessageByMessageIdsAsync(int accountId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
            {
                return Task.CompletedTask;
            }
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                foreach (var id in ids)
                {
                    ct.ThrowIfCancellationRequested();
                    connection.Table<ProtonMessageIdV2>().Delete(x => x.AccountId == accountId && x.MessageId == id);
                    connection.Table<Proton.Message>().Delete(x => x.AccountId == accountId && x.MessageId == id);
                }
            }, cancellationToken);
        }

        public Task DeleteMessagesByIds(int accountId, IReadOnlyList<uint> ids, string labelId, CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
            {
                return Task.CompletedTask;
            }
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var label = connection.Table<ProtonLabelV2>().Where(x => x.AccountId == accountId && x.LabelId == labelId).First();
                foreach (var id in ids)
                {
                    ct.ThrowIfCancellationRequested();
                    connection.Table<ProtonMessageLabel>().Delete(x => x.AccountId == accountId && x.MessageId == id && x.LabelId == label.Id);
                }
            }, cancellationToken);
        }

        public Task AddAIAgentAsync(LocalAIAgent agent, CancellationToken cancellationToken = default)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                if (agent.Account != null)
                {
                    agent.AccountId = agent.Account.Id;
                }
                if (agent.PreProcessorAgent != null)
                {
                    agent.PreProcessorAgentId = agent.PreProcessorAgent.Id;
                }
                if (agent.PostProcessorAgent != null)
                {
                    agent.PostProcessorAgentId = agent.PostProcessorAgent.Id;
                }
                connection.Insert(agent);
            }, cancellationToken);
        }

        private static void LoadAgentDetails(SQLiteConnection connection, LocalAIAgent agent)
        {
            agent.Account = agent.AccountId > 0 ? connection.Find<Account>(agent.AccountId) : null;
            agent.PreProcessorAgent = agent.PreProcessorAgentId > 0 ? connection.Find<LocalAIAgent>(agent.PreProcessorAgentId) : null;
            agent.PostProcessorAgent = agent.PostProcessorAgentId > 0 ? connection.Find<LocalAIAgent>(agent.PostProcessorAgentId) : null;
        }

        public Task<IReadOnlyList<LocalAIAgent>> GetAIAgentsAsync(CancellationToken cancellationToken = default)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var agents = connection.Table<LocalAIAgent>().ToList(ct);
                foreach (var agent in agents)
                {
                    LoadAgentDetails(connection, agent);
                }
                return agents as IReadOnlyList<LocalAIAgent>;
            }, cancellationToken);
        }

        public Task<LocalAIAgent> GetAIAgentAsync(uint id, CancellationToken cancellationToken = default)
        {
            return ReadDatabaseAsync((connection, ct) =>
            {
                var agent = connection.Find<LocalAIAgent>(id);
                if (agent != null)
                {
                    LoadAgentDetails(connection, agent);
                }
                return agent;
            }, cancellationToken);
        }

        public Task UpdateAIAgentAsync(LocalAIAgent agent, CancellationToken cancellationToken = default)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;

                agent.PreProcessorAgentId = agent.PreProcessorAgent is null ? 0 : agent.PreProcessorAgent.Id;
                agent.PostProcessorAgentId = agent.PostProcessorAgent is null ? 0 : agent.PostProcessorAgent.Id;

                connection.Update(agent);
            }, cancellationToken);
        }

        public Task DeleteAIAgentAsync(uint id, CancellationToken cancellationToken = default)
        {
            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                connection.Delete<LocalAIAgent>(id);
            }, cancellationToken);
        }

        public Task UpdateMessageProcessingResultAsync(Message message, string result, CancellationToken cancellationToken = default)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            return WriteDatabaseAsync((db, ct) =>
            {
                var connection = db.Connection;
                var item = connection.Find<Message>(message.Pk) ?? throw new MessageIsNotExistException();

                item.TextBodyProcessed = result;
                connection.Update(item);
            }, cancellationToken);
        }
    }
}
