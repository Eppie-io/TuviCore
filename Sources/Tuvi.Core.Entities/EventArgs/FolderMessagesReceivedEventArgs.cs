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

namespace Tuvi.Core.Entities
{
    public class FolderMessagesReceivedEventArgs : EventArgs
    {
        public EmailAddress AccountEmail { get; }
        public Folder Folder { get; }
        public IReadOnlyList<Message> Messages { get; }


        public FolderMessagesReceivedEventArgs(EmailAddress accountEmail, Folder folder, IReadOnlyList<Message> messages)
        {
            AccountEmail = accountEmail;
            Folder = folder;
            Messages = messages;
        }
    }
}
