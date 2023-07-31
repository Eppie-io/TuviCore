using System.Collections.Generic;
using System.Linq;

namespace Tuvi.Core.Entities
{
    public class AccountGroup
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        [SQLite.Unique]
        public string Name { get; set; }

        public AccountGroup() { }
    }

    public enum MailProtocol
    {
        SMTP,
        POP3,
        IMAP
    }

    public class Account
    {
        public static Account Default
        {
            get
            {
                return new Account()
                {
                    AuthData = default,
                    IncomingServerAddress = "",
                    IncomingServerPort = 993, // default incoming port (IMAP)
                    IncomingMailProtocol = MailProtocol.IMAP,
                    OutgoingServerAddress = "",
                    OutgoingServerPort = 465, // default outgoing port (SMTP)
                    OutgoingMailProtocol = MailProtocol.SMTP,
                    IsBackupAccountSettingsEnabled = true,
                    IsBackupAccountMessagesEnabled = false,
                    SynchronizationInterval = 10 // 10 minutes
                };
            }
        }

        public Account()
        {
        }

        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        [SQLite.Ignore]
        public EmailAddress Email { get; set; }
        [SQLite.Indexed]
        public int EmailId { get; set; }

        public int GroupId { get; set; }

        public string KeyTag { get; set; }

        public int Type { get; set; }

        [SQLite.Ignore]
        public IAuthenticationData AuthData { get; set; }

        public string IncomingServerAddress { get; set; }
        public int IncomingServerPort { get; set; }
        public MailProtocol IncomingMailProtocol { get; set; }
        public string OutgoingServerAddress { get; set; }
        public int OutgoingServerPort { get; set; }
        public MailProtocol OutgoingMailProtocol { get; set; }

        [SQLite.Ignore]
        public Folder DefaultInboxFolder { get; set; }
        public int DefaultInboxFolderId { get; set; }

        [SQLite.Ignore]
        public Folder SentFolder
        {
            get
            {
                return FoldersStructure.Find((Folder f) => f.IsSent);
            }
        }

        [SQLite.Ignore]
        public Folder DraftFolder
        {
            get
            {
                return FoldersStructure.Find((Folder f) => f.IsDraft);
            }
        }

        [SQLite.Ignore]
        public List<Folder> FoldersStructure { get; set; } = new List<Folder>();

        public bool IsBackupAccountSettingsEnabled { get; set; }
        public bool IsBackupAccountMessagesEnabled { get; set; }

        /// <summary>
        /// Gets or sets the interval, expressed in minutes, at which new messages are checked.
        /// </summary>
        public int SynchronizationInterval { get; set; } = 10; // 10 minutes

        public override bool Equals(object obj)
        {
            return obj is Account other &&
                   Id == other.Id &&
                   IsSame(Email, other.Email) &&
                   IsSame(AuthData, other.AuthData) &&
                   IncomingServerAddress == other.IncomingServerAddress &&
                   IncomingServerPort == other.IncomingServerPort &&
                   IncomingMailProtocol == other.IncomingMailProtocol &&
                   OutgoingServerAddress == other.OutgoingServerAddress &&
                   OutgoingServerPort == other.OutgoingServerPort &&
                   OutgoingMailProtocol == other.OutgoingMailProtocol &&
                   IsBackupAccountSettingsEnabled == other.IsBackupAccountSettingsEnabled &&
                   IsBackupAccountMessagesEnabled == other.IsBackupAccountMessagesEnabled &&
                   SynchronizationInterval == other.SynchronizationInterval &&
                   (DefaultInboxFolder?.Equals(other.DefaultInboxFolder) ?? other.DefaultInboxFolder == null) &&
                   (FoldersStructure?.SequenceEqual(other.FoldersStructure) ?? other.FoldersStructure == null);
        }

        private static bool IsSame(object obj, object other)
        {
            return obj?.Equals(other) ?? other == null;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
