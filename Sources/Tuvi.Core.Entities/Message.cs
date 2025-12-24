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
using System.Linq;
using System.Text.Json.Serialization;

namespace Tuvi.Core.Entities
{
    using AttachmentsCollection = List<Attachment>;
    using EmailAddressCollection = List<EmailAddress>;
    using SignaturesInfoCollection = List<SignatureInfo>;

    internal class EmailStructure
    {
        public string Name { get; private set; }
        public string PubKey { get; private set; }
        public string Domain { get; private set; }

        public EmailStructure(string address)
        {
            var parts = address.Split('@');

            if (parts.Length == 2)
            {
                Domain = parts[1];

                var nameparts = parts[0].Split('+');
                Name = nameparts[0];

                if (nameparts.Length == 2)
                {
                    var candidate = nameparts[1];
                    if (EppiePublicKeySyntax.IsValid(candidate))
                    {
                        PubKey = candidate;
                    }
                }
            }
        }
    }

    public enum NetworkType
    {
        Eppie,
        Bitcoin,
        Ethereum,
        Unsupported,
    }

    public class EmailAddress : IEquatable<EmailAddress>, IComparable<EmailAddress>
    {
        public EmailAddress(string address)
        {
            if (address is null)
            {
                throw new ArgumentNullException(nameof(address));
            }
            Address = address;
        }

        [JsonConstructor]
        public EmailAddress(string address, string name) : this(address)
        {
            Name = name;
        }

        private int _hashValue;
        public string Address { get; }

        public string Name { get; }
        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                return string.IsNullOrWhiteSpace(Name) ? Address : Name + "<" + Address + ">";
            }
        }

        public static EmailAddress Parse(string displayName)
        {
            try
            {
                System.Net.Mail.MailAddress address = new System.Net.Mail.MailAddress(displayName);
                return new EmailAddress(address.Address, address.DisplayName);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        public bool Equals(EmailAddress other)
        {
            if (other == null)
            {
                return false;
            }
            return HasSameAddress(other);// && 
                                         //Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EmailAddress);
        }
        public override string ToString()
        {
            return DisplayName;
        }

        public static bool operator ==(EmailAddress a, EmailAddress b)
        {
            if ((object)a == null || (object)b == null)
            {
                return Object.Equals(a, b);
            }
            return a.Equals(b);
        }

        public static bool operator !=(EmailAddress a, EmailAddress b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            if (_hashValue == 0)
            {
                // Although email address is case sensitive by standard, most mail apps and services treat them as case-insensitive
                _hashValue = Address.ToLowerInvariant().GetHashCode();
            }
            return _hashValue;
        }

        public bool HasSameAddress(EmailAddress email)
        {
            return HasSameAddress(email?.Address);
        }

        public bool HasSameAddress(string email)
        {
            return StringHelper.AreEmailsEqual(Address, email);
        }

        public EmailAddress MakeHybrid(string pubkey)
        {
            if (string.IsNullOrWhiteSpace(pubkey))
            {
                throw new ArgumentException("Public key is required.", nameof(pubkey));
            }

            if (!EppiePublicKeySyntax.IsValid(pubkey))
            {
                throw new ArgumentException("Invalid public key format.", nameof(pubkey));
            }

            if (IsDecentralized && !IsHybrid)
            {
                throw new NotSupportedException("Cannot create hybrid address for a decentralized network address.");
            }

            if (IsHybrid)
            {
                throw new NotSupportedException("Cannot create hybrid from an existing hybrid address.");
            }

            var parts = new EmailStructure(Address);
            var hybrid = new EmailAddress(parts.Name + '+' + pubkey + '@' + parts.Domain, Name + " (Hybrid)");

            if (!hybrid.IsHybrid)
            {
                throw new InvalidOperationException("Cannot create hybrid address with invalid public key format.");
            }

            return hybrid;
        }

        public int CompareTo(EmailAddress other)
        {
            if (other is null)
            {
                return 1; // greater
            }
            return string.Compare(Address, other.Address, StringComparison.Ordinal);
        }

        [JsonIgnore]
        public bool IsHybrid
        {
            get
            {
                return !String.IsNullOrEmpty(new EmailStructure(Address).PubKey);
            }
        }

        [JsonIgnore]
        public bool IsDecentralized
        {
            get
            {
                return Network == NetworkType.Eppie || Network == NetworkType.Bitcoin || Network == NetworkType.Ethereum || IsHybrid;
            }
        }

        [JsonIgnore]
        public string StandardAddress
        {
            get
            {
                if (IsHybrid)
                {
                    var parts = new EmailStructure(Address);

                    return parts.Name + '@' + parts.Domain;
                }
                else
                {
                    return Address;
                }
            }
        }

        [JsonIgnore]
        public EmailAddress OriginalAddress
        {
            get => new EmailAddress(StandardAddress, Name);
        }

        [JsonIgnore]
        public string DecentralizedAddress
        {
            get
            {
                return IsHybrid
                        ? new EmailStructure(Address).PubKey
                        : GetDecentralizedAddress(this);
            }
        }

        [JsonIgnore]
        public NetworkType Network
        {
            get
            {
                return GetNetworkType(this);
            }
        }

        [JsonIgnore]
        /// <summary>
        /// Gets the address to be displayed to users. 
        /// For decentralized addresses that are not hybrid and have a non-empty <c>Name</c>, 
        /// this property returns the <c>Name</c> with the Eppie network postfix (e.g., "Name@eppie").
        /// Otherwise, it returns the raw <c>Address</c>.
        /// Use this property when presenting the address in UI or logs, as it may differ from <c>Address</c> for user-friendly display.
        /// </summary>
        public string DisplayAddress
        {
            get
            {
                var res = Address;

                if (IsDecentralized && !IsHybrid && !string.IsNullOrWhiteSpace(Name))
                {
                    res = $"{Name}{EppieNetworkPostfix}";
                }

                return res;
            }
        }

        private static readonly string EppieNetworkPostfix = "@eppie";
        private static readonly string BitcoinNetworkPostfix = "@bitcoin";
        private static readonly string EthereumNetworkPostfix = "@ethereum";

        public static EmailAddress CreateDecentralizedAddress(NetworkType networkType, string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Address is required.", nameof(address));
            }

            switch (networkType)
            {
                case NetworkType.Bitcoin:
                    address = address + BitcoinNetworkPostfix;
                    break;
                case NetworkType.Eppie:
                    address = address + EppieNetworkPostfix;
                    break;
                case NetworkType.Ethereum:
                    address = address + EthereumNetworkPostfix;
                    break;
                default:
                    throw new ArgumentException("Unsupported network type", nameof(networkType));
            }

            var parts = new EmailStructure(address);
            if (!string.IsNullOrEmpty(parts.PubKey))
            {
                throw new NotSupportedException("Hybrid local-part is not allowed for decentralized network addresses.");
            }

            return new EmailAddress(address);
        }

        private static string GetDecentralizedAddress(EmailAddress email)
        {
            if (email.Address.EndsWith(EppieNetworkPostfix, StringComparison.OrdinalIgnoreCase))
            {
                return email.Address.Substring(0, email.Address.Length - EppieNetworkPostfix.Length);
            }

            if (email.Address.EndsWith(BitcoinNetworkPostfix, StringComparison.OrdinalIgnoreCase))
            {
                return email.Address.Substring(0, email.Address.Length - BitcoinNetworkPostfix.Length);
            }

            if (email.Address.EndsWith(EthereumNetworkPostfix, StringComparison.OrdinalIgnoreCase))
            {
                return email.Address.Substring(0, email.Address.Length - EthereumNetworkPostfix.Length);
            }

            return string.Empty;
        }

        private static NetworkType GetNetworkType(EmailAddress email)
        {
            if (email.Address.EndsWith(EppieNetworkPostfix, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkType.Eppie;
            }

            if (email.Address.EndsWith(BitcoinNetworkPostfix, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkType.Bitcoin;
            }

            if (email.Address.EndsWith(EthereumNetworkPostfix, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkType.Ethereum;
            }

            if (email.IsHybrid)
            {
                return NetworkType.Eppie;
            }

            return NetworkType.Unsupported;
        }

        public static bool operator <(EmailAddress left, EmailAddress right)
        {
            if (left is null)
            {
                return right != null;
            }
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(EmailAddress left, EmailAddress right)
        {
            if (right is null)
            {
                return left != null;
            }
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(EmailAddress left, EmailAddress right)
        {
            if (left is null)
            {
                return true;
            }
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(EmailAddress left, EmailAddress right)
        {
            if (right is null)
            {
                return true;
            }
            return left.CompareTo(right) >= 0;
        }
    }

    public class Attachment
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        [SQLite.Indexed]
        public int MessageId { get; set; }

        public string FileName { get; set; }
        public byte[] Data { get; set; }

        [SQLite.Ignore]
        public bool IsEmpty => Data == null || Data.Length == 0;

        public override bool Equals(object obj)
        {
            return obj is Attachment other &&
                   FileName == other.FileName &&
                   (Data?.SequenceEqual(other.Data) ?? other.Data == null);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public enum MessageProtectionType : int
    {
        None = 0,
        Signature = 1,
        Encryption = 2,
        SignatureAndEncryption = 3
    }

    public class Message
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Pk { get; set; }

        private static void LazyInit<T>(ref T c) where T : class, new()
        {
            if (c is null)
            {
                c = new T();
            }
        }

        private EmailAddressCollection _from;
        [SQLite.Ignore]
        public EmailAddressCollection From { get { LazyInit(ref _from); return _from; } }

        private EmailAddressCollection _replyTo;
        [SQLite.Ignore]
        public EmailAddressCollection ReplyTo { get { LazyInit(ref _replyTo); return _replyTo; } }

        private EmailAddressCollection _to;
        [SQLite.Ignore]
        public EmailAddressCollection To { get { LazyInit(ref _to); return _to; } }

        private EmailAddressCollection _cc;
        [SQLite.Ignore]
        public EmailAddressCollection Cc { get { LazyInit(ref _cc); return _cc; } }

        private EmailAddressCollection _bcc;
        [SQLite.Ignore]
        public EmailAddressCollection Bcc { get { LazyInit(ref _bcc); return _bcc; } }

        [SQLite.Indexed(Name = "Message_StableIndex", Order = 1)]
        public DateTimeOffset Date { get; set; }
        public string Subject { get; set; }

        public string PreviewText { get; set; }
        public string TextBody { get; set; }
        public string HtmlBody { get; set; }
        public string TextBodyProcessed { get; set; }
        public byte[] MimeBody { get; set; }

        private AttachmentsCollection _attachments;
        [SQLite.Ignore]
        public AttachmentsCollection Attachments
        {
            get { LazyInit(ref _attachments); return _attachments; }
        }

        [SQLite.Indexed(Name = "Message_UnreadCounterIndex", Order = 1)]
        public bool IsMarkedAsRead { get; set; }

        [SQLite.Indexed(Name = "Message_UnreadCounterIndex", Order = 2)]
        public string Path { get; set; }

        [SQLite.Ignore]
        public Folder Folder { get; set; }
        [SQLite.Indexed(Name = "Message_StableIndex", Order = 2)]
        public int FolderId { get; set; }

        [SQLite.Indexed(Name = "Message_StableIndex", Order = 3)]
        public uint Id { get; set; }

        public bool IsFlagged { get; set; }

        public bool IsDecentralized { get; set; }

        private ProtectionInfo _protection;
        [SQLite.Ignore]
        public ProtectionInfo Protection
        {
            get { LazyInit(ref _protection); return _protection; }
            set { _protection = value; }
        }

        public Message()
        {
        }

        public Message ShallowCopy()
        {
            return (Message)MemberwiseClone();
        }

        public bool IsFromCorrespondenceWithContact(EmailAddress accountEmail, EmailAddress contactEmail)
        {
            return GetContactEmails(accountEmail).Any(x => x == contactEmail);
        }

        public IEnumerable<EmailAddress> GetContactEmails(EmailAddress accountEmail)
        {
            var originators = From.Union(ReplyTo);
            if (originators.Any(from => accountEmail.HasSameAddress(from)))
            {
                // our account is message originator, so, collect destination addresses
                return To.Union(Cc)
                         .Union(Bcc) // TODO: should we really collect Bcc?
                         .Where(x => !accountEmail.HasSameAddress(x));
            }
            return originators;
        }

        public bool IsFromCorrespondenceWithContact(EmailAddress contactEmail)
        {
            // TODO: do we still need this?
            return From.Concat(ReplyTo).Concat(To).Concat(Cc).Concat(Bcc).Any(x => x == contactEmail);
        }

        /// <summary>
        /// Update message fields if they differ from other
        /// </summary>
        /// <param name="other"></param>
        /// <returns>true if message has been updated</returns>
        public bool TryToUpdate(Message other)
        {
            // updated only this field for a while
            if (IsFlagged == other.IsFlagged &&
                IsMarkedAsRead == other.IsMarkedAsRead)
            {
                return false;
            }
            IsFlagged = other.IsFlagged;
            IsMarkedAsRead = other.IsMarkedAsRead;
            return true;
        }

        public IEnumerable<EmailAddress> AllRecipients
        {
            get
            {
                return To.Concat(Cc).Concat(Bcc).Distinct();
            }
        }
    }

    public class ProtectionInfo
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        [SQLite.Indexed]
        public int MessageId { get; set; }

        public MessageProtectionType Type { get; set; } = MessageProtectionType.None;

        [SQLite.Ignore]
        public SignaturesInfoCollection SignaturesInfo { get; set; } = new SignaturesInfoCollection();
    }

    public class SignatureInfo
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        [SQLite.Indexed]
        public int ProtectionId { get; set; }

        public DateTime Created { get; set; }
        public string DigestAlgorithm { get; set; }
        public bool IsVerified { get; set; }
        public string SignerEmail { get; set; }
        public string SignerName { get; set; }
        public string SignerFingerprint { get; set; }
    }
}
