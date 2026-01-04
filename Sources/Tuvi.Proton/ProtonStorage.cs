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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Proton
{
    [SQLite.Table("ProtonMessage")]
    public class Message : IEquatable<Message>
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }
        [SQLite.Indexed(Name = "ProtonMessage_Index", Order = 2, Unique = true)]
        public string MessageId { get; set; }
        public string Subject { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Cc { get; set; }
        public string Bcc { get; set; }
        public bool Unread { get; set; }
        public bool IsFlagged { get; set; }
        public long Flags { get; set; }
        public DateTimeOffset Time { get; set; }
        public int NumAttachments { get; set; }
        [SQLite.Indexed(Name = "ProtonMessage_Index", Order = 1, Unique = true)]
        public int AccountId { get; set; }
        [SQLite.Ignore]
        public IReadOnlyList<string> LabelIds { get; set; }

        public override bool Equals(object obj)
        {
            return true;
        }

        public bool Equals(Message other)
        {
            return other != null &&
                MessageId == other.MessageId &&
                Subject == other.Subject &&
                From == other.From &&
                To == other.To &&
                Cc == other.Cc &&
                Bcc == other.Bcc &&
                Unread == other.Unread &&
                Flags == other.Flags &&
                Time == other.Time &&
                NumAttachments == other.NumAttachments;
        }

        public override int GetHashCode()
        {
            // we don't have planes to store it in hash table
            Debug.Assert(false);
            return base.GetHashCode();
        }


        //    public bool IsEncrypted => true;// (Flags & (long)MessageFlags.E2E) != 0;
        //      public bool IsSigned => true;// (Flags & (long)MessageFlags.Sign) != 0;
    }

    public interface IStorage : IDisposable
    {
        Task AddMessageIDs(int accountId, IReadOnlyList<string> ids, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<KeyValuePair<string, uint>>> LoadMessageIDsAsync(int accountId, CancellationToken cancellationToken = default);
        Task AddOrUpdateMessagesAsync(int accountId, IReadOnlyList<Message> messages, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Message>> GetMessagesAsync(int accountId, string labelId, uint knownId, bool getEarlier, int count, CancellationToken cancellationToken = default);
        Task<Message> GetMessageAsync(int accountId, string labelId, uint id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Message>> GetMessagesAsync(int accountId, IReadOnlyList<uint> ids, CancellationToken cancellationToken = default);
        Task DeleteMessageByMessageIdsAsync(int accountId, IReadOnlyList<string> ids, CancellationToken cancellationToken = default);
        Task DeleteMessagesByIds(int accountId, IReadOnlyList<uint> ids, string labelId, CancellationToken cancellationToken = default);
    }
}
