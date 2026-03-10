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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;
using Tuvi.Core.Mail;

namespace Tuvi.Core.Tests
{
    [TestFixture]
    public class AccountServiceRestoreTests
    {
        [Test]
        public async Task RestoreMessagesAsyncUsesMailBoxRestorerBeforeAddingMessagesToDataStorage()
        {
            var account = TestAccountInfo.GetAccount();
            var folder = new Folder("Inbox", FolderAttributes.Inbox) { Account = account };
            account.FoldersStructure = new List<Folder> { folder };

            var messages = new List<Message>
            {
                new Message
                {
                    Id = 1,
                    Subject = "Restored message",
                    IsMarkedAsRead = true,
                    IsDecentralized = true,
                }
            };

            var dataStorageMock = new Mock<IDataStorage>(MockBehavior.Strict);
            var mailBoxMock = new Mock<IMailBox>(MockBehavior.Strict);
            var mailBoxRestorerMock = mailBoxMock.As<IMailBoxMessagesRestorer>();
            var messageProtectorMock = new Mock<IMessageProtector>(MockBehavior.Strict);

            var sequence = new MockSequence();
            mailBoxRestorerMock.InSequence(sequence)
                .Setup(x => x.RestoreMessagesAsync(folder, messages, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            dataStorageMock.InSequence(sequence)
                .Setup(x => x.AddMessageListAsync(account.Email, folder.FullName, messages, true, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var accountService = new AccountService(account, dataStorageMock.Object, mailBoxMock.Object, messageProtectorMock.Object);

            await accountService.RestoreMessagesAsync(folder, messages, CancellationToken.None).ConfigureAwait(true);

            mailBoxRestorerMock.Verify(x => x.RestoreMessagesAsync(folder, messages, It.IsAny<CancellationToken>()), Times.Once);
            dataStorageMock.Verify(x => x.AddMessageListAsync(account.Email, folder.FullName, messages, true, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
