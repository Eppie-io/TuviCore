using KeyDerivation;
using KeyDerivation.Keys;
using KeyDerivationLib;
using MimeKit;
using System;
using System.Collections.Generic;
using Tuvi.Core.Entities;
using TuviPgpLib.Entities;

namespace BackupTests
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

    internal static class TestData
    {
        public static readonly List<Tuple<Int32, byte[]>> IntegerToBufferPairs = new List<Tuple<Int32, byte[]>>
        {
            new Tuple<Int32, byte[]>(0, new byte[] { 0, 0, 0, 0 }),
            new Tuple<Int32, byte[]>(1, new byte[] { 0, 0, 0, 1 }),
            new Tuple<Int32, byte[]>(0x12345678, new byte[] { 0x12, 0x34, 0x56, 0x78 })
        };

        public static readonly BackupProtocolVersion ProtocolVersion = new BackupProtocolVersion
        {
            Major = 1,
            Minor = 2,
            Build = 3,
            Revision = 4
        };

        public static readonly Account Account1 = new Account
        {
            Email = new EmailAddress("john.doe@mail.com", "John Doe"),
            AuthData = new BasicAuthData() { Password = "qwertyuiop" },
            IncomingServerAddress = "imap.mail.box",
            IncomingServerPort = 12345,
            IncomingMailProtocol = MailProtocol.IMAP,
            OutgoingServerAddress = "smtp.mail.box",
            OutgoingServerPort = 98745,
            OutgoingMailProtocol = MailProtocol.SMTP,
            Id = 753,
            FoldersStructure = new List<Folder> {
                new Folder("inboxFolder", FolderAttributes.Inbox),
                new Folder("usualFolder", FolderAttributes.None),
                new Folder("DraftFolder", FolderAttributes.Draft)},
            DefaultInboxFolder = new Folder("inboxFolder", FolderAttributes.Inbox)
        };

        public static readonly Account Account2 = new Account
        {
            Email = new EmailAddress("mr.smith@mail.box", "Mr. Smith"),
            AuthData = new OAuth2Data() 
            { 
                RefreshToken = "asdfghjkl",
                AuthAssistantId = "id #1"
            },
            IncomingServerAddress = "imap.mail.box",
            IncomingServerPort = 7412,
            IncomingMailProtocol = MailProtocol.POP3,
            OutgoingServerAddress = "smtp.mail.box",
            OutgoingServerPort = 951,
            OutgoingMailProtocol = MailProtocol.IMAP,
            Id = 852,
            FoldersStructure = new List<Folder> {
                new Folder("inbox2Folder", FolderAttributes.Inbox),
                new Folder("usual2Folder", FolderAttributes.None),
                new Folder("Draft2Folder", FolderAttributes.Draft)},
            DefaultInboxFolder = new Folder("inbox2Folder", FolderAttributes.Inbox)
        };

        public class TestAccount
        {
            public string Name = "";
            public string Address = "";

            public MailboxAddress GetMailbox()
            {
                return new MailboxAddress(Name, Address);
            }

            public UserIdentity GetUserIdentity()
            {
                return new UserIdentity(Name, Address);
            }

            public string GetPgpIdentity()
            {
                return Address;
            }
        };

        public static TestAccount GetAccount()
        {
            return new TestAccount { Address = BackupPgpKeyIdentity, Name = "Backup" };
        }

        public const string BackupPackageIdentifier = "Tuvi.Backup.Test";
        public const string BackupPgpKeyIdentity = "backup@test";

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

        public static readonly byte[] DataToProtect = System.Text.Encoding.UTF8.GetBytes("some data to put into a package");

        public static readonly List<byte[]> ImportedPublicKeys = new List<byte[]>
        {
            new byte[32]{
                0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x1A, 0x1B, 0x1C, 0x1D, 0x1F, 0x20, 0x21, 0x22,
                0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x1A, 0x1B, 0x1C, 0x1D, 0x1F, 0x20, 0x21, 0x22
            },

            new byte[32]{
                0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x1A, 0x1B, 0x1C, 0x1D, 0x1F, 0x20, 0x21, 0x22,
                0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x1A, 0x1B, 0x1C, 0x1D, 0x1F, 0x20, 0x21, 0x22
            }
        };

        public static readonly Settings SomeSettings = new Settings
        {
            EppieAccountCounter = 123,
            BitcoinAccountCounter = 321,
        };
    }
}
