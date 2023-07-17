namespace Tuvi.Core.Entities
{
    public class ImageInfo
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Bytes { get; set; }

        public ImageInfo()
        {

        }

        public ImageInfo(int width, int height)
        {
            Width = width;
            Height = height;
            Bytes = null;
        }

        public ImageInfo(int width, int height, byte[] bytes)
        {
            Width = width;
            Height = height;
            Bytes = bytes;
        }

        [SQLite.Ignore]
        public bool IsEmpty => Bytes is null && Width == 0 && Height == 0;

        [SQLite.Ignore]
        public static ImageInfo Empty => new ImageInfo(0, 0);
    }

    public class Contact
    {
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public int Id { get; set; }

        public string FullName { get; set; }

        [SQLite.Ignore]
        public EmailAddress Email { get; set; }
        [SQLite.Indexed(Unique = true)]
        [SQLite.Unique]
        public int EmailId { get; set; }

        [SQLite.Ignore]
        public ImageInfo AvatarInfo { get; set; }

        public int AvatarInfoId { get; set; }

        [SQLite.Ignore]
        public bool HasAvatar => !AvatarInfo.IsEmpty;

        [SQLite.Ignore]
        public LastMessageData LastMessageData { get; set; }

        [SQLite.Indexed]
        public int LastMessageDataId { get; set; }

        public int UnreadCount { get; set; }

        public Contact()
        {
            AvatarInfo = ImageInfo.Empty;
        }
        public Contact(string fullName, EmailAddress email)
        {
            FullName = fullName;
            Email = email;
            AvatarInfo = ImageInfo.Empty;
        }
        public Contact(string fullName, EmailAddress email, ImageInfo avatarInfo)
        {
            FullName = fullName;
            Email = email;
            AvatarInfo = avatarInfo;
        }
    }
}
