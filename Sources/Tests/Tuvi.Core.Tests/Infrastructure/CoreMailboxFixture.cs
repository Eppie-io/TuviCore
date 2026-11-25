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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Tuvi.Core.DataStorage;
using Tuvi.Core.DataStorage.Impl;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;
using Tuvi.Core.Mail;
using static Tuvi.Core.Tests.Infrastructure.CoreTestSupport;

namespace Tuvi.Core.Tests.Infrastructure
{
    internal sealed class CoreMailboxFixture : IDisposable
    {
        public string FilePath { get; } = Path.Combine(Path.GetTempPath(), "TuviCore-lifecycle-" + Guid.NewGuid().ToString("N") + ".db");
        public IDataStorage Native { get; }
        public Mock<IDataStorage> Storage { get; } = new Mock<IDataStorage>();
        public Mock<IMailBox> Mailbox { get; } = new Mock<IMailBox>();
        public Mock<ISecurityManager> Security { get; }
        public Mock<ICredentialsManager> Credentials { get; }
        public TuviMail Core { get; }
        public Func<Folder, CancellationToken, Task<IReadOnlyList<Message>>> Receive { get; set; } =
            (_, _) => Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());

        public CoreMailboxFixture(Func<Func<CancellationToken, Task>, double, SchedulerWithTimer> schedulerFactory = null)
        {
            Native = DataStorageProvider.GetDataStorage(FilePath);
            Storage.Setup(x => x.GetAccountsAsync(It.IsAny<CancellationToken>())).Returns(Native.GetAccountsAsync);
            Storage.Setup(x => x.GetAccountGroupsAsync(It.IsAny<CancellationToken>())).Returns(Native.GetAccountGroupsAsync);
            Storage.Setup(x => x.GetAccountAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>())).Returns(Native.GetAccountAsync);
            Storage.Setup(x => x.GetLatestMessageAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Native.GetLatestMessageAsync);
            Storage.Setup(x => x.GetEarliestMessageAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((EmailAddress email, string folder, CancellationToken token) => Native.GetEarliestMessageAsync(email, folder, token));
            Storage.Setup(x => x.IsMessageExistAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<CancellationToken>())).Returns(Native.IsMessageExistAsync);
            Storage.Setup(x => x.AddMessageListAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<Message>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Native.AddMessageListAsync);
            Storage.Setup(x => x.UpdateAccountAuthAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Native.UpdateAccountAuthAsync);
            Storage.Setup(x => x.DeleteFolderAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Native.DeleteFolderAsync);
            Storage.Setup(x => x.DeleteAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Native.DeleteAccountAsync);
            Storage.Setup(x => x.Dispose()).Callback(Native.Dispose);
            Mailbox.Setup(x => x.GetMessagesAsync(It.IsAny<Folder>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns((Folder folder, int count, CancellationToken token) => Receive(folder, token));
            Mailbox.Setup(x => x.GetLaterMessagesAsync(It.IsAny<Folder>(), It.IsAny<int>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()))
                .Returns((Folder folder, int count, Message last, CancellationToken token) => Receive(folder, token));
            Core = CoreTestBase.CreateCore(Storage.Object, Mailbox.Object, null, schedulerFactory);
            Security = Mock.Get(Core.GetSecurityManager());
            Credentials = Mock.Get(Core.CredentialsManager);
            Security.Setup(x => x.ResetAsync()).Returns(Native.ResetAsync);
        }

        public async Task InitializeAsync(params Account[] accounts)
        {
            await Native.CreateAsync("account-removal").WaitAsync(TestTimeout).ConfigureAwait(false);
            foreach (var account in accounts) await Native.AddAccountAsync(account).WaitAsync(TestTimeout).ConfigureAwait(false);
            await Core.InitializeApplicationAsync("account-removal", CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false);
        }

        public void Dispose()
        {
            try { Core.Dispose(); }
            finally
            {
                Native.Dispose();
                foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" }) File.Delete(FilePath + suffix);
            }
        }
    }
}
