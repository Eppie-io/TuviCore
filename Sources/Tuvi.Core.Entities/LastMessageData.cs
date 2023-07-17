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
        public int AccountEmailId { get; set; }

        public uint MessageId { get; set; }
        public DateTimeOffset Date { get; set; }

        public LastMessageData()
        {

        }

        public LastMessageData(EmailAddress accountEmail, uint messageId, DateTimeOffset date)
        {
            AccountEmail = accountEmail;
            MessageId = messageId;
            Date = date;
        }
    }
}
