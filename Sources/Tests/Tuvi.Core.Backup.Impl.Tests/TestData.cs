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
using KeyDerivation;
using KeyDerivation.Keys;
using KeyDerivationLib;
using MimeKit;
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

        // Test email addresses
        public const string SenderEmailAddress = "sender@test.com";
        public const string SenderName = "Test Sender";
        public const string RecipientEmailAddress = "recipient@test.com";
        public const string RecipientName = "Test Recipient";
        public const string SignerEmailAddress = "signer@test.com";
        public const string SignerName = "Test Signer";
        public const string SignerFingerprint = "0123456789ABCDEF";

        // Test message content
        public const string TestSubject = "Test Subject";
        public const string TestTextBody = "Test body";
        public const string TestHtmlBody = "<p>Test body</p>";
        public const string TestPreviewText = "Test preview";
        public const string DigestAlgorithm = "SHA256";

        // Test message subjects for ordering tests
        public const string FirstMessageSubject = "First";
        public const string SecondMessageSubject = "Second";
        public const string ThirdMessageSubject = "Third";

        // Test account emails
        public const string User1Email = "user1@test.com";
        public const string User2Email = "user2@test.com";
        public const string User3Email = "user3@test.com";
        public const string FullCycleEmail = "fullcycle@test.com";

        // Test attachment data
        public static readonly byte[] PdfMagicBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        public static readonly byte[] PngMagicBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG

        // Test attachment file names
        public const string DocumentFileName = "document.pdf";
        public const string ImageFileName = "image.png";
        public const string TestAttachmentFileName = "test.txt";

        // Test password
        public const string TestPassword = "test123";

        // Test date/time constants
        public static readonly DateTimeOffset TestMessageDate = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        public static readonly DateTimeOffset CompleteTestMessageDate = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        public static readonly DateTime TestSignatureDate = new DateTime(2025, 1, 15, 12, 30, 0, DateTimeKind.Utc);
        public static readonly DateTimeOffset BackupManagerTestMessageDate = new DateTimeOffset(2025, 1, 15, 14, 0, 0, TimeSpan.Zero);

        // Test folder names
        public const string InboxFolderName = "INBOX";
        public const string SentFolderName = "Sent";
        public const string DraftsFolderName = "Drafts";

        // Test message subjects
        public const string InboxMessageSubject = "Inbox Message";
        public const string SentMessageSubject = "Sent Message";
        public const string DraftMessageSubject = "Draft Message";
        public const string ImportantMessageSubject = "Important Message";
        public const string CompleteTestMessageSubject = "Complete Test Message";
        public const string Account1MessageSubject = "Account1 Message";
        public const string Account2MessageSubject = "Account2 Message";

        // Test email addresses for message recipients
        public const string From1Email = "from1@test.com";
        public const string From1Name = "From User 1";
        public const string From2Email = "from2@test.com";
        public const string From2Name = "From User 2";
        public const string ReplyToEmail = "replyto@test.com";
        public const string ReplyToName = "Reply To";
        public const string To1Email = "to1@test.com";
        public const string To1Name = "To User 1";
        public const string To2Email = "to2@test.com";
        public const string To2Name = "To User 2";
        public const string CcEmail = "cc@test.com";
        public const string CcName = "CC User";
        public const string BccEmail = "bcc@test.com";
        public const string BccName = "BCC User";

        // Test attachment content
        public const string TestAttachmentContent = "Test attachment content";
        public const string CompleteTestBodyText = "Complete test body text";
        public const string CompleteTestBodyHtml = "<html><body><p>Complete test body HTML</p></body></html>";
        public const string CompleteTestPreviewText = "Complete test preview";

        // Test email addresses for BackupManager tests
        public const string FullCycleTestEmail = "fullcycle@test.com";
        public const string MismatchTestEmail = "mismatch@test.com";

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
