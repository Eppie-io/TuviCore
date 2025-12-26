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
