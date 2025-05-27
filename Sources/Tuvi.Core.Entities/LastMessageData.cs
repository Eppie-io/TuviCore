using System;

namespace Tuvi.Core.Entities
{
    public class LastMessageData
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        [SQLite.Ignore]
        public EmailAddress AccountEmail { get; set; }
        public int AccountId { get; set; }

        // TODO: remove this property after migration (18.05.2025)
        [Obsolete("use property AccountId")]
        public int AccountEmailId { get; set; }

        public uint MessageId { get; set; }
        public DateTimeOffset Date { get; set; }

        public LastMessageData()
        {
        }

        public LastMessageData(int accountId, EmailAddress accountEmail, uint messageId, DateTimeOffset date)
        {
            AccountId = accountId;
            AccountEmail = accountEmail;
            MessageId = messageId;
            Date = date;
        }
    }
}
