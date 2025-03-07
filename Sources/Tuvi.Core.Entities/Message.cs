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
                    PubKey = nameparts[1];
                }
            }
        }
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

        private int _hashValue = 0;
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
            var parts = new EmailStructure(Address);
            return new EmailAddress(parts.Name + '+' + pubkey + '@' + parts.Domain, Name + " (Hybrid)");
        }

        public int CompareTo(EmailAddress other)
        {
            if (other is null)
            {
                return 1; // greater
            }
            return Address.CompareTo(other.Address);
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
        public string StandardAddress
        {
            get
            {
                var parts = new EmailStructure(Address);

                return parts.Name + '@' + parts.Domain;
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
                        : StringHelper.GetDecentralizedAddress(this);
            }
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
