using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Tests
{
    public class CompositeAccountTests : CoreTestBase
    {
        [SetUp]
        public void Setup()
        {
            DeleteStorage();
        }

        [Test]
        public void CompositeFolderCreationTest()
        {
            var accountService = new Mock<IAccountService>();
            var folders = new List<Folder>()
            {
                new Folder("Inbox", FolderAttributes.Inbox){ UnreadCount = 10, TotalCount =20 },
                new Folder("Folder", FolderAttributes.None){ UnreadCount = 2, TotalCount = 23}
            };
            var compositeFolder = new CompositeFolder(folders, x => accountService.Object);

            Assert.That(compositeFolder.UnreadCount, Is.EqualTo(12));
            Assert.That(compositeFolder.FullName, Is.EqualTo("Inbox;Folder"));
        }

        [Test]
        public void CompositeAccountCreationTest()
        {
            var address1 = new EmailAddress("address1@mail.box");
            var address2 = new EmailAddress("address2@mail.box");
            var accountService1 = new Mock<IAccountService>();
            var accountService2 = new Mock<IAccountService>();

            var compositeFolders = new List<CompositeFolder>()
            {
                new CompositeFolder(new List<Folder>()
                {
                    new Folder("Inbox", FolderAttributes.Inbox){Id = 1},
                    new Folder("Inbox", FolderAttributes.Inbox){Id = 2},
                }, x=>
                {
                    switch (x.Id)
                    {
                        case 1: return accountService1.Object;
                        case 2: return accountService2.Object;
                        default: return null; // exception will be thrown
                    }
                }),
                new CompositeFolder(new List<Folder>()
                {
                    new Folder("Folder", FolderAttributes.None){Id = 3},
                    new Folder("Folder2", FolderAttributes.None){Id = 4},
                    new Folder("Folder", FolderAttributes.None){Id = 5},
                }, x=>
                {
                    switch (x.Id)
                    {
                        case 3:
                        case 4: return accountService1.Object;
                        case 5: return accountService2.Object;
                        default: return null; // exception will be thrown
                    }
                }),
            };

            var defaultFolder = compositeFolders[0];
            var addresses = new List<EmailAddress>()
            {
                address1,
                address2
            };

            var account = new CompositeAccount(compositeFolders, addresses, defaultFolder);

            Assert.That(account.Addresses, Is.EqualTo(addresses));
            Assert.That(account.Email, Is.EqualTo(address1));
            Assert.That(account.FoldersStructure.Count, Is.EqualTo(2));
            Assert.That(account.FoldersStructure, Is.EqualTo(compositeFolders));
            Assert.That(account.DefaultInboxFolder, Is.EqualTo(defaultFolder));
        }

        [Test]
        public async Task GetCompositeAccountsPerformace()
        {
            await CreateDataStorageAsync().ConfigureAwait(true);
            using var dataStorage = await OpenDataStorageAsync().ConfigureAwait(true);
            using var core = CreateCore(dataStorage);
            for (int i = 0; i < 500; ++i)
            {
                await core.AddAccountAsync(GetTestEmailAccount(new EmailAddress($"account{i}@address.tld"))).ConfigureAwait(true);
            }
            var sw = new Stopwatch();
            sw.Start();
            var accounts = await core.GetCompositeAccountsAsync(default).ConfigureAwait(true);
            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds < 1000, Is.True);
            Assert.That(accounts.Count, Is.EqualTo(500));
        }

        [Test]
        public async Task CompositeFolderReceiveEarlierMessages()
        {
            var folders = new List<Folder>()
            {
                new Folder("Inbox", FolderAttributes.Inbox){ UnreadCount = 10, TotalCount =20, Id = 1},
                new Folder("Folder", FolderAttributes.None){ UnreadCount = 2, TotalCount = 23, Id = 2}
            };
            var accountService1 = new Mock<IAccountService>();
            accountService1.Setup(x => x.ReceiveEarlierMessagesAsync(folders[0], It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Message>() { CreateMessage(3) });
            var accountService2 = new Mock<IAccountService>();
            accountService2.Setup(x => x.ReceiveEarlierMessagesAsync(folders[1], It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Message>() { CreateMessage(1), CreateMessage(5) });

            var compositeFolder = new CompositeFolder(folders, x =>
            {
                switch (x.Id)
                {
                    case 1: return accountService1.Object;
                    case 2: return accountService2.Object;
                    default: return null;
                }
            });

            var messages = await compositeFolder.ReceiveEarlierMessagesAsync(100).ConfigureAwait(true);
            accountService1.Verify(x => x.ReceiveEarlierMessagesAsync(folders[0], 100, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            accountService1.Verify(x => x.ReceiveEarlierMessagesAsync(folders[1], 100, It.IsAny<CancellationToken>()), Times.Never);
            accountService2.Verify(x => x.ReceiveEarlierMessagesAsync(folders[0], 100, It.IsAny<CancellationToken>()), Times.Never);
            accountService2.Verify(x => x.ReceiveEarlierMessagesAsync(folders[1], 100, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            Assert.That(messages.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task CompositeFoldeSynchronizeAsync()
        {
            var folders = new List<Folder>()
            {
                new Folder("Inbox", FolderAttributes.Inbox){ UnreadCount = 10, TotalCount =20, Id = 1},
                new Folder("Folder", FolderAttributes.None){ UnreadCount = 2, TotalCount = 23, Id = 2}
            };
            var accountService1 = new Mock<IAccountService>();
            var accountService2 = new Mock<IAccountService>();

            var compositeFolder = new CompositeFolder(folders, x =>
            {
                switch (x.Id)
                {
                    case 1: return accountService1.Object;
                    case 2: return accountService2.Object;
                    default: return null;
                }
            });

            await compositeFolder.SynchronizeAsync(true, default).ConfigureAwait(true);
            accountService1.Verify(x => x.SynchronizeFolderAsync(folders[0], true, default), Times.AtLeastOnce);
            accountService2.Verify(x => x.SynchronizeFolderAsync(folders[1], true, default), Times.AtLeastOnce);
            accountService1.Verify(x => x.SynchronizeFolderAsync(folders[1], It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            accountService2.Verify(x => x.SynchronizeFolderAsync(folders[0], It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task CompositeFoldeReceiveNewMessagesAsync()
        {
            var folders = new List<Folder>()
            {
                new Folder("Inbox", FolderAttributes.Inbox){ UnreadCount = 10, TotalCount =20, Id = 1},
                new Folder("Folder", FolderAttributes.None){ UnreadCount = 2, TotalCount = 23, Id = 2}
            };
            var accountService1 = new Mock<IAccountService>();
            var accountService2 = new Mock<IAccountService>();

            accountService1.Setup(x => x.ReceiveNewMessagesInFolderAsync(folders[0], It.IsAny<CancellationToken>())).ReturnsAsync(new List<Message>() { CreateMessage(3) });
            accountService2.Setup(x => x.ReceiveNewMessagesInFolderAsync(folders[1], It.IsAny<CancellationToken>())).ReturnsAsync(new List<Message>() { CreateMessage(1), CreateMessage(5) });

            var compositeFolder = new CompositeFolder(folders, x =>
            {
                switch (x.Id)
                {
                    case 1: return accountService1.Object;
                    case 2: return accountService2.Object;
                    default: return null;
                }
            });

            var ct = new CancellationToken();

            await compositeFolder.ReceiveNewMessagesAsync(ct).ConfigureAwait(true);
            accountService1.Verify(x => x.ReceiveNewMessagesInFolderAsync(folders[0], ct), Times.AtLeastOnce);
            accountService2.Verify(x => x.ReceiveNewMessagesInFolderAsync(folders[1], ct), Times.AtLeastOnce);
            accountService1.Verify(x => x.ReceiveNewMessagesInFolderAsync(folders[1], It.IsAny<CancellationToken>()), Times.Never);
            accountService2.Verify(x => x.ReceiveNewMessagesInFolderAsync(folders[0], It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task CompositeFoldeGetUnreadMessagesCountAsync()
        {
            var folders = new List<Folder>()
            {
                new Folder("Inbox", FolderAttributes.Inbox){ UnreadCount = 10, TotalCount =20, Id = 1},
                new Folder("Folder", FolderAttributes.None){ UnreadCount = 2, TotalCount = 23, Id = 2}
            };
            var accountService1 = new Mock<IAccountService>();
            var accountService2 = new Mock<IAccountService>();

            accountService1.Setup(x => x.GetUnreadMessagesCountInFolderAsync(folders[0], It.IsAny<CancellationToken>())).ReturnsAsync(234);
            accountService2.Setup(x => x.GetUnreadMessagesCountInFolderAsync(folders[1], It.IsAny<CancellationToken>())).ReturnsAsync(4);

            var compositeFolder = new CompositeFolder(folders, x =>
            {
                switch (x.Id)
                {
                    case 1: return accountService1.Object;
                    case 2: return accountService2.Object;
                    default: return null;
                }
            });

            var ct = new CancellationToken();

            int unreadCount = await compositeFolder.GetUnreadMessagesCountAsync(ct).ConfigureAwait(true);
            accountService1.Verify(x => x.GetUnreadMessagesCountInFolderAsync(folders[0], ct), Times.AtLeastOnce);
            accountService2.Verify(x => x.GetUnreadMessagesCountInFolderAsync(folders[1], ct), Times.AtLeastOnce);
            accountService1.Verify(x => x.GetUnreadMessagesCountInFolderAsync(folders[1], It.IsAny<CancellationToken>()), Times.Never);
            accountService2.Verify(x => x.GetUnreadMessagesCountInFolderAsync(folders[0], It.IsAny<CancellationToken>()), Times.Never);
            Assert.That(unreadCount, Is.EqualTo(238));
        }

        [Test]
        public async Task CompositeFoldeUpdateFolderStructureAsync()
        {
            var folders = new List<Folder>()
            {
                new Folder("Inbox", FolderAttributes.Inbox){ UnreadCount = 10, TotalCount =20, Id = 1},
                new Folder("Folder", FolderAttributes.None){ UnreadCount = 2, TotalCount = 23, Id = 2}
            };
            var accountService1 = new Mock<IAccountService>();
            var accountService2 = new Mock<IAccountService>();

            accountService1.Setup(x => x.UpdateFolderStructureAsync(It.IsAny<CancellationToken>()));
            accountService2.Setup(x => x.UpdateFolderStructureAsync(It.IsAny<CancellationToken>()));

            var compositeFolder = new CompositeFolder(folders, x =>
            {
                switch (x.Id)
                {
                    case 1: return accountService1.Object;
                    case 2: return accountService2.Object;
                    default: return null;
                }
            });

            var ct = new CancellationToken();

            await compositeFolder.UpdateFolderStructureAsync(ct).ConfigureAwait(true);
            accountService1.Verify(x => x.UpdateFolderStructureAsync(ct), Times.AtLeastOnce);
            accountService2.Verify(x => x.UpdateFolderStructureAsync(ct), Times.AtLeastOnce);
        }

    }
}
