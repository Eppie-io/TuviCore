using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KeyDerivation;
using KeyDerivation.Keys;
using KeyDerivationLib;
using Moq;
using Tuvi.Core.DataStorage;
using Tuvi.Core.DataStorage.Tests;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;
using Tuvi.Core.Mail;

namespace Tuvi.Core.Tests
{
    class TestAccountInfo
    {
        public const string Name = "Mail Test";
        public const string Email = "mail@mail.box";

        public const string IncomingServerAddress = "imap.mail.box";
        public const int IncomingServerPort = 993;
        public const MailProtocol IncomingMailProtocol = MailProtocol.IMAP;

        public const string OutgoingServerAddress = "smtp.mail.box";
        public const int OutgoingServerPort = 465;
        public const MailProtocol OutgoingMailProtocol = MailProtocol.SMTP;

        public const string Password = "Pass123";
        public const string DataBasePassword = "123456";

        public static readonly System.Net.NetworkCredential Credentials = new System.Net.NetworkCredential(Email, Password);

        public static Account GetAccount()
        {
            var account = new Account();
            account.Email = new EmailAddress(Email, Name);

            account.IncomingServerAddress = IncomingServerAddress;
            account.IncomingServerPort = IncomingServerPort;
            account.IncomingMailProtocol = IncomingMailProtocol;

            account.OutgoingServerAddress = OutgoingServerAddress;
            account.OutgoingServerPort = OutgoingServerPort;
            account.OutgoingMailProtocol = OutgoingMailProtocol;

            account.AuthData = new BasicAuthData() { Password = Password };

            return account;
        }
    }

    internal class EncryptionTestsData
    {
        internal class TestKeyDerivationDetailsProvider : IKeyDerivationDetailsProvider
        {
            public string GetSaltPhrase()
            {
                return "Bla-bla";
            }

            public int GetSeedPhraseLength()
            {
                return 12;
            }

            public Dictionary<SpecialPgpKeyType, string> GetSpecialPgpKeyIdentities()
            {
                throw new NotImplementedException();
            }
        }

        public static readonly string[] TestSeedPhrase = {
            "abandon", "abandon", "abandon", "abandon",
            "abandon", "abandon", "abandon", "abandon",
            "abandon", "abandon", "abandon", "abandon"
        };

        public static readonly MasterKey MasterKey = CreateMasterKey(TestSeedPhrase);

        private static MasterKey CreateMasterKey(string[] seedPhrase)
        {
            MasterKeyFactory factory = new MasterKeyFactory(new TestKeyDerivationDetailsProvider());
            factory.RestoreSeedPhrase(seedPhrase);
            return factory.GetMasterKey();
        }
    }

    public class CoreTestBase : TestWithStorageBase
    {
        public static TuviMail CreateCore(IDataStorage dataStorage, IMailBox externalMailBox = null, IMessageProtector externalProtector = null)
        {
            var mailBox = GetMailBox(externalMailBox);
            var mailBoxFactory = new Mock<IMailBoxFactory>();
            var mailMailTester = new Mock<IMailServerTester>();
            mailBoxFactory.Setup(x => x.CreateMailBox(It.IsAny<Account>()))
                          .Returns(mailBox);
            var securityManager = new Mock<ISecurityManager>();
            securityManager.Setup(x => x.GetMessageProtector()).Returns(GetMessageProtector(externalProtector));
            var backupManager = new Mock<IBackupManager>();
            var credentialsManager = new Mock<ICredentialsManager>();
            credentialsManager.Setup(x => x.CreateCredentialsProvider(It.IsAny<Account>()))
                              .Returns(new Mock<ICredentialsProvider>().Object);
            
            return new TuviMail(mailBoxFactory.Object,
                                mailMailTester.Object,
                                dataStorage,
                                securityManager.Object,
                                backupManager.Object,
                                credentialsManager.Object,
                                new ImplementationDetailsProvider("Test seed", "Test.Package", "backup@test"));

            IMailBox GetMailBox(IMailBox external)
            {
                if (external is null)
                {
                    var mailBox = new Mock<IMailBox>();
                    mailBox.Setup(x => x.GetEarlierMessagesAsync(It.IsAny<Folder>(),
                                                                 It.IsAny<int>(),
                                                                 It.IsAny<Message>(),
                                                                 It.IsAny<CancellationToken>()))
                           .ReturnsAsync(new List<Message>());
                    mailBox.Setup(x => x.GetFoldersStructureAsync(It.IsAny<CancellationToken>()))
                           .ReturnsAsync(() =>
                           {
                               return new List<Folder>() { new Folder("Inbox", FolderAttributes.Inbox),
                                                                             new Folder("Sent", FolderAttributes.Sent),
                                                                             new Folder("Folder1", FolderAttributes.None),
                                                                             new Folder("Folder2", FolderAttributes.None),
                                                                             new Folder("Folder3", FolderAttributes.None),
                                                                             new Folder("Folder4", FolderAttributes.None),
                                                                             new Folder("Folder5", FolderAttributes.None),
                                                                            };
                           });
                    mailBox.Setup(x => x.GetMessagesAsync(It.IsAny<Folder>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(new List<Message>());
                    mailBox.Setup(x => x.GetDefaultInboxFolderAsync(It.IsAny<CancellationToken>()))
                           .ReturnsAsync(new Folder("Inbox", FolderAttributes.Inbox));
                    mailBox.Setup(x => x.ReplaceDraftMessageAsync(It.IsAny<uint>(),
                                                                  It.IsAny<Message>(),
                                                                  It.IsAny<CancellationToken>()))
                           .ReturnsAsync(
                            (uint id, Message m, CancellationToken ct) =>
                            {
                                var messageCopy = m.ShallowCopy();
                                messageCopy.Id = id + 4;
                                messageCopy.Pk = 0; // MailBox may not preserve Pk
                                return messageCopy;
                            });
                    return mailBox.Object;
                }
                return external;
            }

            IMessageProtector GetMessageProtector(IMessageProtector external)
            {
                if (external is null)
                {
                    var mailBox = new Mock<IMessageProtector>();
                    return mailBox.Object;
                }
                return external;
            }
        }



        public static Message CreateMessage(uint id)
        {
            return CreateMessage(id, false, false, DateTimeOffset.Now);
        }

        public static Message CreateMessage(uint id, bool read)
        {
            return CreateMessage(id, read, false, DateTimeOffset.Now);
        }

        public static Message CreateFlaggedMessage(uint id)
        {
            return CreateMessage(id, false, true, DateTimeOffset.Now);
        }

        public static Message CreateMessage(uint id, bool read, bool flagged, DateTimeOffset date)
        {
            var message = new Message()
            {
                Id = id,
                Date = date,
                IsMarkedAsRead = read,
                IsFlagged = flagged
            };
            return message;
        }

        public static Account GetTestEmailAccount()
        {
            return GetTestEmailAccount(new EmailAddress("Test@test.test"));
        }

        public static Account GetTestEmailAccount(EmailAddress address)
        {
            var account = Account.Default;
            account.Email = address;
            account.IncomingServerAddress = "localhost";
            account.OutgoingServerAddress = "localhost";
            account.IncomingServerPort = 143;
            account.OutgoingServerPort = 143;
            account.AuthData = new BasicAuthData() { Password = "Pass123" };
            account.FoldersStructure.Add(new Folder("INBOX", FolderAttributes.Inbox));
            account.FoldersStructure.Add(new Folder("SENT", FolderAttributes.Sent));
            account.DefaultInboxFolder = account.FoldersStructure[0];
            return account;
        }
    }

    public class SyncDataProvider
    {
        internal List<Message> LocalMessages = new List<Message>();
        internal List<Message> RemoteMessages = new List<Message>();

        public Task<IReadOnlyList<Message>> LoadLocalMessagesAsync(uint minUid,
                                                                   uint maxUid,
                                                                   CancellationToken cancellationToken)
        {
            Debug.Assert(minUid <= maxUid);
            var res = LocalMessages.Where(x => x.Id < maxUid && x.Id >= minUid)
                                   .OrderByDescending(x => x.Id);
            return Task.FromResult<IReadOnlyList<Message>>(res.ToList());
        }

        public Task<IReadOnlyList<Message>> LoadRemoteMessagesAsync(Message fromMessage,
                                                                    int count,
                                                                    CancellationToken cancellationToken)
        {
            IEnumerable<Message> res = null;
            if (fromMessage is null)
            {
                res = RemoteMessages.OrderByDescending(x => x.Id)
                                    .Take((int)count);
            }
            else
            {
                res = RemoteMessages.Where(x => x.Id < fromMessage.Id)
                                    .OrderByDescending(x => x.Id)
                                    .Take((int)count);
            }
            return Task.FromResult<IReadOnlyList<Message>>(res.ToList());
        }
    }

}
