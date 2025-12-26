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
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail.Impl.Protocols
{
    abstract class ReceiverService : MailService, IDisposable
    {
        protected ReceiverService(string serverAddress, int serverPort, ICredentialsProvider credentialsProvider)
            : base(serverAddress, serverPort, credentialsProvider)
        {
        }

        public abstract Folder GetDefaultInboxFolder();
        public abstract Task<IList<Folder>> GetFoldersStructureAsync(CancellationToken cancellationToken);
        public abstract Task<IReadOnlyList<Message>> GetMessagesAsync(Folder folder, int count, CancellationToken cancellationToken);
        public abstract Task<int> GetMessageCountAsync(Folder folder, CancellationToken cancellationToken);
        public abstract Task<Message> GetMessageAsync(Folder folder, uint id, CancellationToken cancellationToken);
        public abstract Task<IReadOnlyList<Message>> GetLaterMessagesAsync(Folder folder, int count, Message lastMessage, CancellationToken cancellationToken);
        public abstract Task<IReadOnlyList<Message>> GetEarlierMessagesAsync(Folder folder, int count, Message lastMessage, bool fast, CancellationToken cancellationToken);
        public abstract Task<IList<Message>> CheckNewMessagesAsync(Folder folder, DateTime dateTime, CancellationToken cancellationToken);
        public abstract Task AppendSentMessageAsync(Message message, string messageId, CancellationToken cancellationToken);
        public abstract Task<Message> AppendDraftMessageAsync(Message message, CancellationToken cancellationToken);
        public abstract Task<Message> ReplaceDraftMessageAsync(uint id, Message message, CancellationToken cancellationToken);
        public abstract Task MarkMessagesAsReadAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken);
        public abstract Task MarkMessagesAsUnReadAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken);
        public abstract Task FlagMessagesAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken);
        public abstract Task UnflagMessagesAsync(IList<uint> ids, Folder folderPath, CancellationToken cancellationToken);
        public abstract Task DeleteMessagesAsync(IReadOnlyList<uint> ids, Folder folderPath, bool permanentDelete, CancellationToken cancellationToken);
        public abstract Task MoveMessagesAsync(IReadOnlyList<uint> ids, Folder folderPath, Folder targetFolderPath, CancellationToken cancellationToken);

        public abstract void Dispose();
    }
}
