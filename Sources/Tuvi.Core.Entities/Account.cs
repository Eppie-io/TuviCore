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
using System.Text.Json.Serialization;

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

    public enum MailBoxType : int
    {
        Email,
        Dec,
        Hybrid,
        Proton
    }

    public enum ExternalContentPolicy : int
    {
        AlwaysAllow = 0,
        AskEachTime,
        Block
    }

    public class Account : IEquatable<Account>
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
                    SynchronizationInterval = 10, // 10 minutes
                    IsMessageFooterEnabled = true,
                    ExternalContentPolicy = ExternalContentPolicy.AlwaysAllow
                };
            }
        }

        public Account()
        {
        }

        [JsonIgnore]
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        [JsonIgnore]
        [SQLite.Ignore]
        public EmailAddress Email
        {
            get
            {
#pragma warning disable CS0618 // Only for SQLite and internal using
                if (_Email is null && EmailAddress != null)
                {
                    _Email = new EmailAddress(EmailAddress, EmailName);
                }
                return _Email;
            }
            set
            {
                EmailAddress = value?.Address;
                EmailName = value?.Name;
                _Email = null;
#pragma warning restore CS0618 // Only for SQLite and internal using
            }
        }
        private EmailAddress _Email;

        /// <summary>
        /// Gets the address displayed to users.
        /// For decentralized addresses in the Eppie network with a non-empty <c>DecentralizedName</c>,
        /// this property returns an email composed of the <c>DecentralizedName</c> and the Eppie
        /// network postfix (e.g., "DecentralizedName@eppie").
        /// Otherwise, it returns the raw <c>Email</c>.
        /// Use this property when presenting the address in the UI or logs, as it may differ
        /// from <c>Email</c> for user-friendly display.
        /// </summary>
        [JsonIgnore]
        [SQLite.Ignore]
        public EmailAddress DisplayEmail
        {
            get
            {
                var email = Email;
                if (email is null)
                {
                    return null;
                }
#pragma warning disable CS0618 // Only for SQLite and internal using
                if (email.Network == NetworkType.Eppie && !string.IsNullOrWhiteSpace(DecentralizedName))
                {
                    return Entities.EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, DecentralizedName);
                }
#pragma warning restore CS0618 // Only for SQLite and internal using

                return email;
            }
        }

        [Obsolete("only for SQLite and internal using")]
        public string DecentralizedName { get; set; }
        public void SetDecentralizedName(string value)
        {
            if (DecentralizedAccountIndex < 0 || Email.Network != NetworkType.Eppie)
            {
                throw new ArgumentException("DecentralizedName cannot be assigned.", nameof(value));
            }
#pragma warning disable CS0618 // Only for SQLite and internal using
            DecentralizedName = value;
#pragma warning restore CS0618 // Only for SQLite and internal using
        }

        public string GetDecentralizedName()
        {
#pragma warning disable CS0618 // Only for SQLite and internal using
            return DecentralizedName;
#pragma warning restore CS0618 // Only for SQLite and internal using
        }

        [Obsolete("only for SQLite and internal using")]
        public string EmailAddress { get; set; }
        [Obsolete("only for SQLite internal using")]
        public string EmailName { get; set; }

        // TODO: remove this property after migration (18.05.2025)
        [JsonIgnore]
        [Obsolete("use property Email")]
        public int EmailId { get; set; }

        [JsonIgnore]
        public int GroupId { get; set; }

        public int DecentralizedAccountIndex { get; set; } = -1; // -1 means that account is not decentralized

        public MailBoxType Type { get; set; }

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

        [JsonIgnore]
        public int DefaultInboxFolderId { get; set; }

        [JsonIgnore]
        [SQLite.Ignore]
        public Folder SentFolder
        {
            get
            {
                return FoldersStructure.Find((Folder f) => f.IsSent);
            }
        }

        [JsonIgnore]
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

        public string MessageFooter { get; set; }
        public bool IsMessageFooterEnabled { get; set; }

        /// <summary>
        /// Gets or sets the policy for handling external content in email messages.
        /// </summary>
        public ExternalContentPolicy ExternalContentPolicy { get; set; } = ExternalContentPolicy.AlwaysAllow;

        public static bool operator ==(Account left, Account right)
        {
            return object.Equals(left, right);
        }

        public static bool operator !=(Account left, Account right)
        {
            return !object.Equals(left, right);
        }

        public bool Equals(Account other)
        {
            if (other is null)
            {
                return false;
            }

            if (Id > 0 || other.Id > 0)
            {
                return Id > 0 && other.Id > 0 && Id == other.Id;
            }

            return Email == other.Email;
        }

        public bool DeepEquals(Account other)
        {
            if (other is null)
            {
                return false;
            }

            return Email == other.Email
                && string.Equals(GetDecentralizedName(), other.GetDecentralizedName(), StringComparison.Ordinal)
                && DecentralizedAccountIndex == other.DecentralizedAccountIndex
                && Type == other.Type
                && object.Equals(AuthData, other.AuthData)
                && string.Equals(IncomingServerAddress, other.IncomingServerAddress, StringComparison.Ordinal)
                && IncomingServerPort == other.IncomingServerPort
                && IncomingMailProtocol == other.IncomingMailProtocol
                && string.Equals(OutgoingServerAddress, other.OutgoingServerAddress, StringComparison.Ordinal)
                && OutgoingServerPort == other.OutgoingServerPort
                && OutgoingMailProtocol == other.OutgoingMailProtocol
                && IsBackupAccountSettingsEnabled == other.IsBackupAccountSettingsEnabled
                && IsBackupAccountMessagesEnabled == other.IsBackupAccountMessagesEnabled
                && SynchronizationInterval == other.SynchronizationInterval
                && string.Equals(MessageFooter, other.MessageFooter, StringComparison.Ordinal)
                && IsMessageFooterEnabled == other.IsMessageFooterEnabled
                && ExternalContentPolicy == other.ExternalContentPolicy
                && HaveSameFolders(FoldersStructure, other.FoldersStructure);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Account);
        }

        public override int GetHashCode()
        {
            return Id > 0 ? Id : Email?.GetHashCode() ?? 0;
        }

        private static bool HaveSameFolders(IList<Folder> left, IList<Folder> right)
        {
            if (object.ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!object.Equals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
