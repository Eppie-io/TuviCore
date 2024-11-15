using KeyDerivation;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Backup;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;
using Tuvi.Core.Mail;

#nullable enable

namespace Tuvi.Core.Tests
{
    internal class MySyncContext : SynchronizationContext
    {
        private int _calls;

        public MySyncContext()
        {
        }

        public int Calls { get => _calls; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            ++_calls;
            d(state);
        }
    }

    public class CoreTests
    {
        private static Mock<ISecurityManager> InitMockSecurityManager()
        {
            var securityManagerMock = new Mock<ISecurityManager>();
            var backupProtector = new Mock<IBackupProtector>();
            securityManagerMock.Setup(a => a.GetBackupProtector()).Returns(backupProtector.Object);
            return securityManagerMock;
        }

        [Test]
        public async Task GetAccountsList()
        {
            var accountsList = new List<Account>() { TestAccountInfo.GetAccount() };

            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            dataStorageMock.Setup(a => a.GetAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accountsList);

            using var core = TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"));

            var accounts = await core.GetAccountsAsync().ConfigureAwait(true);

            Assert.That(accounts.Count > 0, Is.True);
        }

        [Test]
        public async Task AddNewAccount()
        {
            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var mailBoxMock = new Mock<IMailBox>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();

            var account = TestAccountInfo.GetAccount();
            var folderStructureList = new List<Folder> { new Folder("inbox", FolderAttributes.Inbox), new Folder("spam", FolderAttributes.Junk) };
            dataStorageMock.Setup(a => a.AddAccountAsync(account, It.IsAny<CancellationToken>()));
            dataStorageMock.Setup(a => a.ExistsAccountWithEmailAddressAsync(account.Email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            dataStorageMock.Setup(a => a.GetMasterKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EncryptionTestsData.MasterKey);
            mailBoxFactoryMock.Setup(a => a.CreateMailBox(account)).Returns(mailBoxMock.Object);
            mailBoxMock.Setup(a => a.GetFoldersStructureAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folderStructureList);

            using var core = TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"));
            await core.AddAccountAsync(account, default).ConfigureAwait(true);

            Assert.Pass();
        }

        [Test]
        public void AddTwoSameAccounts()
        {
            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var mailBoxMock = new Mock<IMailBox>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();

            var account = TestAccountInfo.GetAccount();
            var folderStructureList = new List<Folder> { new Folder("inbox", FolderAttributes.Inbox), new Folder("spam", FolderAttributes.Junk) };
            dataStorageMock.Setup(a => a.AddAccountAsync(account, It.IsAny<CancellationToken>()));
            dataStorageMock.Setup(a => a.ExistsAccountWithEmailAddressAsync(account.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mailBoxFactoryMock.Setup(a => a.CreateMailBox(account)).Returns(mailBoxMock.Object);
            mailBoxMock.Setup(a => a.GetFoldersStructureAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folderStructureList);

            using var core = TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"));
            Assert.ThrowsAsync<AccountAlreadyExistInDatabaseException>(async () => await core.AddAccountAsync(account, It.IsAny<CancellationToken>()).ConfigureAwait(true));
        }

        [Test]
        public void AddAccountWithIncorrectData()
        {
            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var mailBoxMock = new Mock<IMailBox>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();

            var account = TestAccountInfo.GetAccount();
            dataStorageMock.Setup(a => a.AddAccountAsync(account, It.IsAny<CancellationToken>()));
            dataStorageMock.Setup(a => a.ExistsAccountWithEmailAddressAsync(account.Email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            dataStorageMock.Setup(a => a.GetMasterKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EncryptionTestsData.MasterKey);
            mailBoxFactoryMock.Setup(a => a.CreateMailBox(account)).Returns(mailBoxMock.Object);
            mailBoxMock.Setup(a => a.GetFoldersStructureAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new ConnectionException());

            using var core = TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"));
            Assert.ThrowsAsync<ConnectionException>(async () => await core.AddAccountAsync(account, default).ConfigureAwait(true));
        }

        [Test]
        public void SeedCompatibility()
        {
            IKeyDerivationDetailsProvider provider = new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test");

            Assert.That(
                "Test seed",
                Is.EqualTo(provider.GetSaltPhrase()),
                "TuviMail master key derivation salt phrase is altered! This will brake keys compatibility.");
        }

        [Test]
        public void ApplicationFirstStart()
        {
            var securityManagerMock = InitMockSecurityManager();
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var mailBoxMock = new Mock<IMailBox>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();

            securityManagerMock.Setup(a => a.IsNeverStartedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

            using var core = TuviCoreCreator.CreateTuviMailCore(
                mailBoxFactoryMock.Object,
                mailServerTesterMock.Object,
                dataStorageMock.Object,
                securityManagerMock.Object,
                backupManager.Object,
                credentialsManager.Object,
                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"));
            Assert.That(core.IsFirstApplicationStartAsync().Result, Is.True);

            securityManagerMock.Setup(a => a.IsNeverStartedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
            Assert.That(core.IsFirstApplicationStartAsync().Result, Is.False);
        }

        [Test]
        public void CoreInvalidArgsTest()
        {
            ITuviMail core;
            var mailBoxFactoryMock = new Mock<IMailBoxFactory>();
            var mailServerTesterMock = new Mock<IMailServerTester>();
            var dataStorageMock = new Mock<IDataStorage>();
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            var securityManagerMock = InitMockSecurityManager();

            Assert.Throws<ArgumentNullException>(() =>
            {
                core = TuviCoreCreator.CreateTuviMailCore(mailBoxFactoryMock.Object, null, null, null, null, null, null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                core = TuviCoreCreator.CreateTuviMailCore(null, mailServerTesterMock.Object, null, null, null, null, null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                core = TuviCoreCreator.CreateTuviMailCore(null, null, dataStorageMock.Object, null, null, null, null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                core = TuviCoreCreator.CreateTuviMailCore(null, null, null, securityManagerMock.Object, null, null, null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                core = TuviCoreCreator.CreateTuviMailCore(null, null, null, null, backupManager.Object, null, null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                core = TuviCoreCreator.CreateTuviMailCore(null, null, null, null, null, credentialsManager.Object, null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                core = TuviCoreCreator.CreateTuviMailCore(null, null, null, null, null, null, null);
            });
        }

        class TestServer
        {
            IReadOnlyList<string> _transcript;
            public TestServer(IReadOnlyList<string> transcript)
            {
                _transcript = transcript;
            }
            public Task StartAsync(int port, CancellationToken cancellationToken)
            {
                return Task.Run(async () =>
                {
                    try
                    {
                        var listener = new TcpListener(IPAddress.Any, port);
                        CancellationTokenRegistration ctr = default;
                        if (cancellationToken.CanBeCanceled)
                        {
                            ctr = cancellationToken.Register(listener.Stop);
                        }

                        using (ctr)
                        {
                            listener.Start();

                            while (true)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                var client = await listener.AcceptTcpClientAsync().ConfigureAwait(true);
                                await ProcessClientAsync(client, cancellationToken).ConfigureAwait(true);
                            }

                        }
                    }
                    catch (Exception) when (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }
                    catch
                    {
                        throw;
                    }
                }, cancellationToken);
            }

            private async Task ProcessClientAsync(TcpClient client, CancellationToken cancellationToken)
            {
                byte[] buffer = new byte[32];
                int readOffset = 0;
                int bytesRead = 0;
                //using (var s = new SslStream(client.GetStream(), false))
                using (var s = client.GetStream())
                {
                    bool sending = true; // state
                    foreach (var command in _transcript)
                    {
                        if (sending)
                        {
                            var bytes = Encoding.ASCII.GetBytes(command);
                            await s.WriteAsync(bytes, cancellationToken).ConfigureAwait(true);
                            sending = false;
                            continue;
                        }
                        StringBuilder sb = new StringBuilder();
                        bool commandFound = false;
                        while (!commandFound)
                        {
                            int count = 0;
                            for (count = 0; bytesRead > 1 && count < bytesRead; ++count)
                            {
                                if (buffer[readOffset + count] == '\r' &&
                                    buffer[readOffset + count + 1] == '\n')
                                {
                                    count += 2;
                                    commandFound = true;
                                    break;
                                }
                            }

                            sb.Append(Encoding.ASCII.GetString(buffer, readOffset, count));
                            readOffset = (readOffset + count) % buffer.Length;
                            bytesRead -= count; // consume read bytes
                            if (!commandFound)
                            {
                                bytesRead = await s.ReadAsync(buffer.AsMemory(readOffset, buffer.Length - readOffset), cancellationToken).ConfigureAwait(true);
                            }
                        }
                        Assert.That(sb.ToString(), Is.EqualTo(command));
                        sending = true;
                    }
                }
            }
        }

        [Test]
        [Ignore("This transcript doesn't support login, so STATUS is unavailable")]
        public async Task CoreAccountOperationTest1()
        {
            var tokenRefresher = new Mock<ITokenRefresher>();

            string dbPath = Path.Combine(Environment.CurrentDirectory, nameof(CoreAccountOperationTest1) + ".db");
            File.Delete(dbPath);
            using ITuviMail core = ComponentBuilder.Components.CreateTuviMailCore(dbPath, new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"), tokenRefresher.Object);

            bool created = false;
            bool deleted = false;
            bool updated = false;
            core.AccountAdded += (sender, args) => { created = true; };
            core.AccountDeleted += (sender, args) => { deleted = true; };
            core.AccountUpdated += (sender, args) => { updated = true; };

            IReadOnlyList<string> transcript = new List<string>()
            {
                "* OK IMAP4rev1 Service Ready\r\n",
                "A00000000 CAPABILITY\r\n",
                "* CAPABILITY IMAP4rev2 AUTH=PLAIN LOGINDISABLED\r\n" +
                "A00000000 OK CAPABILITY completed\r\n",
                "A00000001 AUTHENTICATE PLAIN\r\n",
                "A00000001 OK PLAIN authentication successful\r\n",
                "A00000002 CAPABILITY\r\n",
                "* CAPABILITY IMAP4rev2  AUTH=PLAIN LOGINDISABLED\r\n" +
                "A00000002 OK CAPABILITY completed\r\n",
                "A00000003 LIST \"\" \"\"\r\n",
                "* LIST (\\Noselect) \"/\" \"\"\r\n"+
                "A00000003 OK LIST Completed\r\n",
                "A00000004 LIST \"\" \"INBOX\"\r\n",
                "* LIST (\\HasNoChildren ) \"/\" \"INBOX\"\r\n" +
                "A00000004 OK LIST Completed\r\n",
                "A00000005 LIST \"\" \"*\"\r\n",
                "* LIST (\\All \\HasNoChildren \\Subscribed) \"/\" \"INBOX\"\r\n" +
                "* LIST (\\HasNoChildren \\Subscribed \\Trash) \"/\" \"TestTrash\"\r\n" +
                "* LIST (\\HasNoChildren \\Sent \\Subscribed) \"/\" \"TestSent\"\r\n" +
                "* LIST (\\Flagged \\HasNoChildren \\Subscribed) \"/\" \"TestFlagged\"\r\n" +
                "* LIST (\\HasNoChildren \\Junk \\Subscribed) \"/\" \"TestJunk\"\r\n" +
                "* LIST (\\Drafts \\HasNoChildren \\Subscribed) \"/\" \"TestDrafts\"\r\n" +
                "A00000005 OK LIST Completed\r\n"

            };

            var testServer = new TestServer(transcript);
            using var cts = new CancellationTokenSource();
            Task serverTask = testServer.StartAsync(143, cts.Token);
            //await Task.Delay(500); // wait 
            var account = Account.Default;
            account.Email = new EmailAddress("Test@test.test");
            account.IncomingServerAddress = "localhost";
            account.OutgoingServerAddress = "localhost";
            account.IncomingServerPort = 143;
            account.OutgoingServerPort = 143;
            account.AuthData = new BasicAuthData() { Password = "Pass123" };

            var ctx = new MySyncContext();
            SynchronizationContext.SetSynchronizationContext(ctx);
            await core.InitializeApplicationAsync("123").ConfigureAwait(true);
            Assert.That(ctx.Calls == 1, Is.True);
            await core.AddAccountAsync(account, cts.Token).ConfigureAwait(true);
            //Assert.IsTrue(ctx.Calls == 2);
            Assert.That(created, Is.True);
            Assert.That(deleted, Is.False);
            Assert.That(updated, Is.False);

            cts.Cancel();
            Assert.ThrowsAsync<OperationCanceledException>(async () => await serverTask.ConfigureAwait(true));
        }
    }
}
