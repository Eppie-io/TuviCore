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
