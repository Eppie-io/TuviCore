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
using System.Text.Json.Serialization;

namespace Tuvi.Core.Entities
{
    [Flags]
    public enum FolderAttributes
    {
        None = 0,
        Inbox = (1 << 0),
        Draft = (1 << 1),
        Junk = (1 << 2),
        Trash = (1 << 3),
        Sent = (1 << 4),
        Important = (1 << 5),
        All = (1 << 6),
    }

    /// <summary>
    /// Class for representing folders located on the server
    /// </summary>
    public class Folder : IEquatable<Folder>
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        [SQLite.Indexed]
        public int AccountId { get; set; }

        [SQLite.Ignore]
        /// <summary>
        /// The address of account that owns this folder
        /// </summary>
        public EmailAddress AccountEmail { get; set; }

        /// <summary>
        /// Unread messages count
        /// </summary>
        [JsonIgnore]
        public int UnreadCount { get; set; }

        /// <summary>
        /// Total messages in folder
        /// </summary>
        [JsonIgnore]
        public int TotalCount { get; set; }

        /// <summary>
        /// Number of locally stored messages
        /// </summary>
        [JsonIgnore]
        public int LocalCount { get; set; }

        [SQLite.Indexed]
        /// <summary>
        /// Full folder's name 
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// Folder's attributes
        /// </summary>
        public FolderAttributes Attributes { get; set; }

        /// <summary>
        /// Attribute indicating that this is Inbox folder
        /// </summary>
        public bool IsInbox => Attributes.HasFlag(FolderAttributes.Inbox);
        /// <summary>
        /// Attribute indicating that this is Drafts folder
        /// </summary>
        public bool IsDraft => Attributes.HasFlag(FolderAttributes.Draft);
        /// <summary>
        /// Attribute indicating that this is Spam folder
        /// </summary>
        public bool IsJunk => Attributes.HasFlag(FolderAttributes.Junk);
        /// <summary>
        /// Attribute indicating that this is Trash folder
        /// </summary>
        public bool IsTrash => Attributes.HasFlag(FolderAttributes.Trash);
        /// <summary>
        /// Attribute indicating that this is Sent folder
        /// </summary>
        public bool IsSent => Attributes.HasFlag(FolderAttributes.Sent);

        /// <summary>
        /// Attribute indicating that this is Important folder
        /// </summary>
        public bool IsImportant => Attributes.HasFlag(FolderAttributes.Important);

        /// <summary>
        /// Attribute indicating that this is Important folder
        /// </summary>
        public bool IsAll => Attributes.HasFlag(FolderAttributes.All);

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="fullName">Full folder name</param>
        /// <param name="attributes">Folder attributes</param>
        public Folder(string fullName, FolderAttributes attributes)
        {
            FullName = fullName;
            Attributes = attributes;
        }

        // TODO: do we need it?
        /// <summary>
        /// Default constructor
        /// </summary>
        public Folder()
        {
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Folder);
        }

        public override int GetHashCode()
        {
            return (Id, AccountId, FullName.Length, Attributes).GetHashCode();
        }

        public bool Equals(Folder other)
        {
            if (other is null)
            {
                return false;
            }
            // if folder is not stored then we don't take into account that Ids should be equal
            return (Id == 0 || other.Id == 0 || Id == other.Id) &&
                   AccountEmail == other.AccountEmail &&
                   HasSameName(other.FullName) &&
                   Attributes == other.Attributes;
        }

        public bool HasSameName(Folder folder)
        {
            return folder != null && HasSameName(folder.FullName);
        }

        public bool HasSameName(string folderName)
        {
            return String.Equals(folderName, FullName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
