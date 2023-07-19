using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;
using TuviPgpLib;

namespace Tuvi.Core.DataStorage
{
    public class DecMessageRaw
    {
        public string From { get; set; }
        public string To { get; set; }

        public DateTimeOffset Date { get; set; }
        public string Subject { get; set; }

        public string HtmlBody { get; set; }

        public string TextBody { get; set; }

#pragma warning disable CA1819 // Properties should not return arrays
        public byte[] MimeBody { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays

        public MessageProtectionType ProtectionType { get; set; } = MessageProtectionType.None;

        public DecMessageRaw() { }

        public DecMessageRaw(Message message)
        {
            if (message == null)
                return;

            From = message.From[0].Address;
            To = String.Join(";", message.To.ConvertAll(x => x.Address));
            Date = message.Date;
            Subject = message.Subject;
            HtmlBody = message.HtmlBody;
            TextBody = message.TextBody;
            MimeBody = message.MimeBody;
            ProtectionType = message.Protection.Type;
        }

        public virtual Message ToMessage()
        {
            var message = new Message();

            message.From.Add(new EmailAddress(From));
            message.To.AddRange(To.Split(';').Select(x => new EmailAddress(x)));
            message.Date = Date;
            message.Subject = Subject;
            message.HtmlBody = HtmlBody;
            message.TextBody = TextBody;
            message.MimeBody = MimeBody;
            message.Protection.Type = ProtectionType;

            return message;
        }
    }

    public class DecMessage : DecMessageRaw
    {
        public string Hash { get; set; }

        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public uint Id { get; set; }

        public bool IsMarkedAsRead { get; set; }

        public string Path { get; set; }

        public string FolderName { get; set; }
        public FolderAttributes FolderAttributes { get; set; }

        public bool IsFlagged { get; set; }

        public override Message ToMessage()
        {
            var message = base.ToMessage();

            message.Id = Id;
            message.IsMarkedAsRead = IsMarkedAsRead;
            message.IsFlagged = IsFlagged;
            message.Folder = new Folder(FolderName, FolderAttributes);
            message.IsDecentralized = true;

            return message;
        }

        public DecMessage() { }

        public DecMessage(string hash, Message message) : base(message)
        {
            if (message == null)
                return;

            IsMarkedAsRead = message.IsMarkedAsRead;
            IsFlagged = message.IsFlagged;
            FolderName = message.Folder.FullName;
            FolderAttributes = message.Folder.Attributes;

            Hash = hash;
        }

        public override bool Equals(object obj)
        {
            var otherMessage = obj as DecMessage;
            if (otherMessage == null)
            {
                return false;
            }
            return string.Equals(Hash, otherMessage.Hash, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return Hash.GetHashCode();
        }
    }

    public interface IDecStorage : IKeyStorage
    {
        Task<DecMessage> AddDecMessageAsync(EmailAddress email, DecMessage message, CancellationToken cancellationToken = default);
        Task<bool> IsDecMessageExistsAsync(EmailAddress email, string folder, string hash, CancellationToken cancellationToken = default);
        Task<List<DecMessage>> GetDecMessagesAsync(EmailAddress email, Folder folder, int count, CancellationToken cancellationToken = default);
        Task<DecMessage> GetDecMessageAsync(EmailAddress email, Folder folder, uint id, CancellationToken cancellationToken = default);
        Task<DecMessage> UpdateDecMessageAsync(EmailAddress email, DecMessage message, CancellationToken cancellationToken = default);
        Task DeleteDecMessageAsync(EmailAddress email, Folder folder, uint id, CancellationToken cancellationToken = default);
    }
}
