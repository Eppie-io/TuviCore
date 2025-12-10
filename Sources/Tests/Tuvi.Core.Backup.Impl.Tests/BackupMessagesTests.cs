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
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Entities;
using Tuvi.Core.Entities.Exceptions;

namespace BackupTests
{
    [TestFixture]
    public class BackupMessagesTests : BaseBackupTest
    {
        [OneTimeSetUp]
        protected void InitializeContext()
        {
            Initialize();
        }

        [Test]
        [Category("Backup")]
        [Category("Messages")]
        public async Task BackupSingleMessageSerializesAndDeserializesCorrectly()
        {
            // Arrange
            var message = CreateTestMessage();
            var folder = new FolderMessagesBackupContainer(TestData.InboxFolderName, new List<Message> { message });
            var emailContainer = new EmailAccountBackupContainer(TestData.Account1.Email.Address, new List<FolderMessagesBackupContainer> { folder });

            using var backup = new MemoryStream();
            var builder = BackupSerializationFactory.CreateBackupBuilder();

            // Act
            await builder.SetVersionAsync(TestData.ProtocolVersion).ConfigureAwait(true);
            await builder.SetMessagesAsync(new List<EmailAccountBackupContainer> { emailContainer }).ConfigureAwait(true);
            await builder.BuildBackupAsync(backup).ConfigureAwait(true);

            backup.Position = 0;

            var parser = BackupSerializationFactory.CreateBackupParser();
            await parser.ParseBackupAsync(backup).ConfigureAwait(true);
            var restoredMessages = await parser.GetMessagesAsync().ConfigureAwait(true);

            // Assert
            Assert.That(restoredMessages, Is.Not.Null);
            Assert.That(restoredMessages.Count, Is.EqualTo(1));

            var restoredContainer = restoredMessages[0];
            Assert.That(restoredContainer.EmailAccount, Is.EqualTo(TestData.Account1.Email.Address));
            Assert.That(restoredContainer.Folders.Count, Is.EqualTo(1));

            var restoredFolder = restoredContainer.Folders[0];
            Assert.That(restoredFolder.FolderFullName, Is.EqualTo(TestData.InboxFolderName));
            Assert.That(restoredFolder.Messages.Count, Is.EqualTo(1));

            var restoredMessage = restoredFolder.Messages[0];
            Assert.That(restoredMessage.Date, Is.EqualTo(TestData.TestMessageDate));
            AssertMessageEquals(message, restoredMessage);
        }

        [Test]
        [Category("Backup")]
        [Category("Messages")]
        public async Task BackupMultipleMessagesPreservesOrder()
        {
            // Arrange
            var messages = new List<Message>
            {
                CreateTestMessage(id: 1, subject: TestData.FirstMessageSubject),
                CreateTestMessage(id: 2, subject: TestData.SecondMessageSubject),
                CreateTestMessage(id: 3, subject: TestData.ThirdMessageSubject)
            };

            var folder = new FolderMessagesBackupContainer(TestData.InboxFolderName, messages);
            var emailContainer = new EmailAccountBackupContainer(TestData.Account1.Email.Address, new List<FolderMessagesBackupContainer> { folder });

            using var backup = new MemoryStream();
            var builder = BackupSerializationFactory.CreateBackupBuilder();

            // Act
            await builder.SetVersionAsync(TestData.ProtocolVersion).ConfigureAwait(true);
            await builder.SetMessagesAsync(new List<EmailAccountBackupContainer> { emailContainer }).ConfigureAwait(true);
            await builder.BuildBackupAsync(backup).ConfigureAwait(true);

            backup.Position = 0;

            var parser = BackupSerializationFactory.CreateBackupParser();
            await parser.ParseBackupAsync(backup).ConfigureAwait(true);
            var restoredMessages = await parser.GetMessagesAsync().ConfigureAwait(true);

            // Assert
            var restoredFolder = restoredMessages[0].Folders[0];
            Assert.That(restoredFolder.Messages.Count, Is.EqualTo(3));
            Assert.That(restoredFolder.Messages[0].Subject, Is.EqualTo(TestData.FirstMessageSubject));
            Assert.That(restoredFolder.Messages[1].Subject, Is.EqualTo(TestData.SecondMessageSubject));
            Assert.That(restoredFolder.Messages[2].Subject, Is.EqualTo(TestData.ThirdMessageSubject));
        }

        [Test]
        [Category("Backup")]
        [Category("Messages")]
        public async Task BackupMultipleFoldersPreservesStructure()
        {
            // Arrange
            var inboxMessages = new List<Message> { CreateTestMessage(id: 1, subject: TestData.InboxMessageSubject) };
            var sentMessages = new List<Message> { CreateTestMessage(id: 2, subject: TestData.SentMessageSubject) };
            var draftMessages = new List<Message> { CreateTestMessage(id: 3, subject: TestData.DraftMessageSubject) };

            var folders = new List<FolderMessagesBackupContainer>
            {
                new FolderMessagesBackupContainer(TestData.InboxFolderName, inboxMessages),
                new FolderMessagesBackupContainer(TestData.SentFolderName, sentMessages),
                new FolderMessagesBackupContainer(TestData.DraftsFolderName, draftMessages)
            };

            var emailContainer = new EmailAccountBackupContainer(TestData.Account1.Email.Address, folders);

            using var backup = new MemoryStream();
            var builder = BackupSerializationFactory.CreateBackupBuilder();

            // Act
            await builder.SetVersionAsync(TestData.ProtocolVersion).ConfigureAwait(true);
            await builder.SetMessagesAsync(new List<EmailAccountBackupContainer> { emailContainer }).ConfigureAwait(true);
            await builder.BuildBackupAsync(backup).ConfigureAwait(true);

            backup.Position = 0;

            var parser = BackupSerializationFactory.CreateBackupParser();
            await parser.ParseBackupAsync(backup).ConfigureAwait(true);
            var restoredMessages = await parser.GetMessagesAsync().ConfigureAwait(true);

            // Assert
            var restoredContainer = restoredMessages[0];
            Assert.That(restoredContainer.Folders.Count, Is.EqualTo(3));

            Assert.That(restoredContainer.Folders[0].FolderFullName, Is.EqualTo(TestData.InboxFolderName));
            Assert.That(restoredContainer.Folders[0].Messages[0].Subject, Is.EqualTo(TestData.InboxMessageSubject));

            Assert.That(restoredContainer.Folders[1].FolderFullName, Is.EqualTo(TestData.SentFolderName));
            Assert.That(restoredContainer.Folders[1].Messages[0].Subject, Is.EqualTo(TestData.SentMessageSubject));

            Assert.That(restoredContainer.Folders[2].FolderFullName, Is.EqualTo(TestData.DraftsFolderName));
            Assert.That(restoredContainer.Folders[2].Messages[0].Subject, Is.EqualTo(TestData.DraftMessageSubject));
        }

        [Test]
        [Category("Backup")]
        [Category("Messages")]
        public async Task BackupMultipleAccountsPreservesAllMessages()
        {
            // Arrange
            var account1Messages = new List<EmailAccountBackupContainer>
            {
                new EmailAccountBackupContainer(
                    TestData.Account1.Email.Address,
                    new List<FolderMessagesBackupContainer>
                    {
                        new FolderMessagesBackupContainer(TestData.InboxFolderName, new List<Message> { CreateTestMessage(id: 1, subject: TestData.Account1MessageSubject) })
                    })
            };

            var account2Messages = new EmailAccountBackupContainer(
                TestData.Account2.Email.Address,
                new List<FolderMessagesBackupContainer>
                {
                    new FolderMessagesBackupContainer(TestData.InboxFolderName, new List<Message> { CreateTestMessage(id: 2, subject: TestData.Account2MessageSubject) })
                });

            account1Messages.Add(account2Messages);

            using var backup = new MemoryStream();
            var builder = BackupSerializationFactory.CreateBackupBuilder();

            // Act
            await builder.SetVersionAsync(TestData.ProtocolVersion).ConfigureAwait(true);
            await builder.SetMessagesAsync(account1Messages).ConfigureAwait(true);
            await builder.BuildBackupAsync(backup).ConfigureAwait(true);

            backup.Position = 0;

            var parser = BackupSerializationFactory.CreateBackupParser();
            await parser.ParseBackupAsync(backup).ConfigureAwait(true);
            var restoredMessages = await parser.GetMessagesAsync().ConfigureAwait(true);

            // Assert
            Assert.That(restoredMessages.Count, Is.EqualTo(2));
            Assert.That(restoredMessages[0].EmailAccount, Is.EqualTo(TestData.Account1.Email.Address));
            Assert.That(restoredMessages[1].EmailAccount, Is.EqualTo(TestData.Account2.Email.Address));
        }

        [Test]
        [Category("Backup")]
        [Category("Messages")]
        public async Task BackupEmptyMessageListSucceeds()
        {
            // Arrange
            var emptyFolder = new FolderMessagesBackupContainer(TestData.InboxFolderName, new List<Message>());
            var emailContainer = new EmailAccountBackupContainer(TestData.Account1.Email.Address, new List<FolderMessagesBackupContainer> { emptyFolder });

            using var backup = new MemoryStream();
            var builder = BackupSerializationFactory.CreateBackupBuilder();

            // Act
            await builder.SetVersionAsync(TestData.ProtocolVersion).ConfigureAwait(true);
            await builder.SetMessagesAsync(new List<EmailAccountBackupContainer> { emailContainer }).ConfigureAwait(true);
            await builder.BuildBackupAsync(backup).ConfigureAwait(true);

            backup.Position = 0;

            var parser = BackupSerializationFactory.CreateBackupParser();
            await parser.ParseBackupAsync(backup).ConfigureAwait(true);
            var restoredMessages = await parser.GetMessagesAsync().ConfigureAwait(true);

            // Assert
            Assert.That(restoredMessages, Is.Not.Null);
            Assert.That(restoredMessages[0].Folders[0].Messages, Is.Empty);
        }

        [Test]
        [Category("Backup")]
        [Category("Messages")]
        public async Task BackupWithoutMessagesThrowsException()
        {
            // Arrange
            using var backup = new MemoryStream();
            var builder = BackupSerializationFactory.CreateBackupBuilder();

            // Act
            await builder.SetVersionAsync(TestData.ProtocolVersion).ConfigureAwait(true);
            await builder.BuildBackupAsync(backup).ConfigureAwait(true);

            backup.Position = 0;

            var parser = BackupSerializationFactory.CreateBackupParser();
            await parser.ParseBackupAsync(backup).ConfigureAwait(true);

            // Assert
            Assert.ThrowsAsync<BackupDeserializationException>(() => parser.GetMessagesAsync());
        }

        [Test]
        [Category("Backup")]
        [Category("Messages")]
        public async Task BackupMessageAttachmentsPreservesAttachments()
        {
            // Arrange
            var message = CreateTestMessage();
            message.Attachments.Add(new Attachment
            {
                FileName = TestData.DocumentFileName,
                Data = TestData.PdfMagicBytes
            });
            message.Attachments.Add(new Attachment
            {
                FileName = TestData.ImageFileName,
                Data = TestData.PngMagicBytes
            });

            var folder = new FolderMessagesBackupContainer(TestData.InboxFolderName, new List<Message> { message });
            var emailContainer = new EmailAccountBackupContainer(TestData.Account1.Email.Address, new List<FolderMessagesBackupContainer> { folder });

            using var backup = new MemoryStream();
            var builder = BackupSerializationFactory.CreateBackupBuilder();

            // Act
            await builder.SetVersionAsync(TestData.ProtocolVersion).ConfigureAwait(true);
            await builder.SetMessagesAsync(new List<EmailAccountBackupContainer> { emailContainer }).ConfigureAwait(true);
            await builder.BuildBackupAsync(backup).ConfigureAwait(true);

            backup.Position = 0;

            var parser = BackupSerializationFactory.CreateBackupParser();
            await parser.ParseBackupAsync(backup).ConfigureAwait(true);
            var restoredMessages = await parser.GetMessagesAsync().ConfigureAwait(true);

            // Assert
            var restoredMessage = restoredMessages[0].Folders[0].Messages[0];
            Assert.That(restoredMessage.Attachments.Count, Is.EqualTo(2));
            Assert.That(restoredMessage.Attachments[0].FileName, Is.EqualTo(TestData.DocumentFileName));
            Assert.That(restoredMessage.Attachments[0].Data, Is.EqualTo(TestData.PdfMagicBytes));
            Assert.That(restoredMessage.Attachments[1].FileName, Is.EqualTo(TestData.ImageFileName));
            Assert.That(restoredMessage.Attachments[1].Data, Is.EqualTo(TestData.PngMagicBytes));
        }

        [Test]
        [Category("Backup")]
        [Category("Messages")]
        public async Task BackupMessageProtectionPreservesProtectionInfo()
        {
            // Arrange
            var message = CreateTestMessage();
            message.Protection.Type = MessageProtectionType.SignatureAndEncryption;
            message.Protection.SignaturesInfo.Add(new SignatureInfo
            {
                Created = TestData.TestSignatureDate,
                DigestAlgorithm = TestData.DigestAlgorithm,
                IsVerified = true,
                SignerEmail = TestData.SignerEmailAddress,
                SignerName = TestData.SignerName,
                SignerFingerprint = TestData.SignerFingerprint
            });

            var folder = new FolderMessagesBackupContainer(TestData.InboxFolderName, new List<Message> { message });
            var emailContainer = new EmailAccountBackupContainer(TestData.Account1.Email.Address, new List<FolderMessagesBackupContainer> { folder });

            using var backup = new MemoryStream();
            var builder = BackupSerializationFactory.CreateBackupBuilder();

            // Act
            await builder.SetVersionAsync(TestData.ProtocolVersion).ConfigureAwait(true);
            await builder.SetMessagesAsync(new List<EmailAccountBackupContainer> { emailContainer }).ConfigureAwait(true);
            await builder.BuildBackupAsync(backup).ConfigureAwait(true);

            backup.Position = 0;

            var parser = BackupSerializationFactory.CreateBackupParser();
            await parser.ParseBackupAsync(backup).ConfigureAwait(true);
            var restoredMessages = await parser.GetMessagesAsync().ConfigureAwait(true);

            // Assert
            var restoredMessage = restoredMessages[0].Folders[0].Messages[0];
            Assert.That(restoredMessage.Protection.Type, Is.EqualTo(MessageProtectionType.SignatureAndEncryption));
            Assert.That(restoredMessage.Protection.SignaturesInfo.Count, Is.EqualTo(1));

            var signature = restoredMessage.Protection.SignaturesInfo[0];
            Assert.That(signature.Created, Is.EqualTo(TestData.TestSignatureDate));
            Assert.That(signature.DigestAlgorithm, Is.EqualTo(TestData.DigestAlgorithm));
            Assert.That(signature.IsVerified, Is.True);
            Assert.That(signature.SignerEmail, Is.EqualTo(TestData.SignerEmailAddress));
            Assert.That(signature.SignerFingerprint, Is.EqualTo(TestData.SignerFingerprint));
        }

        [Test]
        [Category("Backup")]
        [Category("Messages")]
        public async Task BackupCompleteMessageDataPreservesAllFields()
        {
            // Arrange
            var message = CreateCompleteTestMessage();
            var folder = new FolderMessagesBackupContainer(TestData.InboxFolderName, new List<Message> { message });
            var emailContainer = new EmailAccountBackupContainer(TestData.Account1.Email.Address, new List<FolderMessagesBackupContainer> { folder });

            using var backup = new MemoryStream();
            var builder = BackupSerializationFactory.CreateBackupBuilder();

            // Act
            await builder.SetVersionAsync(TestData.ProtocolVersion).ConfigureAwait(true);
            await builder.SetMessagesAsync(new List<EmailAccountBackupContainer> { emailContainer }).ConfigureAwait(true);
            await builder.BuildBackupAsync(backup).ConfigureAwait(true);

            backup.Position = 0;

            var parser = BackupSerializationFactory.CreateBackupParser();
            await parser.ParseBackupAsync(backup).ConfigureAwait(true);
            var restoredMessages = await parser.GetMessagesAsync().ConfigureAwait(true);

            // Assert
            var restoredMessage = restoredMessages[0].Folders[0].Messages[0];
            Assert.That(restoredMessage.Date, Is.EqualTo(TestData.CompleteTestMessageDate));
            AssertCompleteMessageEquals(message, restoredMessage);
        }

        #region Helper Methods

        private static Message CreateTestMessage(uint id = 1, string subject = "Test Subject")
        {
            var message = new Message
            {
                Id = id,
                Subject = subject,
                Date = TestData.TestMessageDate,
                TextBody = TestData.TestTextBody,
                HtmlBody = TestData.TestHtmlBody,
                PreviewText = TestData.TestPreviewText,
                IsMarkedAsRead = false,
                IsFlagged = false
            };

            message.From.Add(new EmailAddress(TestData.SenderEmailAddress, TestData.SenderName));
            message.To.Add(new EmailAddress(TestData.RecipientEmailAddress, TestData.RecipientName));

            return message;
        }

        private static Message CreateCompleteTestMessage()
        {
            var message = new Message
            {
                Id = 100,
                Subject = TestData.CompleteTestMessageSubject,
                Date = TestData.CompleteTestMessageDate,
                TextBody = TestData.CompleteTestBodyText,
                HtmlBody = TestData.CompleteTestBodyHtml,
                PreviewText = TestData.CompleteTestPreviewText,
                IsMarkedAsRead = true,
                IsFlagged = true,
                IsDecentralized = false
            };

            message.From.Add(new EmailAddress(TestData.From1Email, TestData.From1Name));
            message.From.Add(new EmailAddress(TestData.From2Email, TestData.From2Name));

            message.ReplyTo.Add(new EmailAddress(TestData.ReplyToEmail, TestData.ReplyToName));

            message.To.Add(new EmailAddress(TestData.To1Email, TestData.To1Name));
            message.To.Add(new EmailAddress(TestData.To2Email, TestData.To2Name));

            message.Cc.Add(new EmailAddress(TestData.CcEmail, TestData.CcName));
            message.Bcc.Add(new EmailAddress(TestData.BccEmail, TestData.BccName));

            message.Attachments.Add(new Attachment
            {
                FileName = TestData.TestAttachmentFileName,
                Data = System.Text.Encoding.UTF8.GetBytes(TestData.TestAttachmentContent)
            });

            message.Protection.Type = MessageProtectionType.Signature;

            return message;
        }

        private static void AssertMessageEquals(Message expected, Message actual)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.Subject, Is.EqualTo(expected.Subject));
            Assert.That(actual.Date, Is.EqualTo(expected.Date));
            Assert.That(actual.TextBody, Is.EqualTo(expected.TextBody));
            Assert.That(actual.HtmlBody, Is.EqualTo(expected.HtmlBody));
            Assert.That(actual.IsMarkedAsRead, Is.EqualTo(expected.IsMarkedAsRead));
            Assert.That(actual.IsFlagged, Is.EqualTo(expected.IsFlagged));

            Assert.That(actual.From.Count, Is.EqualTo(expected.From.Count));
            Assert.That(actual.To.Count, Is.EqualTo(expected.To.Count));
        }

        private static void AssertCompleteMessageEquals(Message expected, Message actual)
        {
            AssertMessageEquals(expected, actual);

            Assert.That(actual.PreviewText, Is.EqualTo(expected.PreviewText));
            Assert.That(actual.IsDecentralized, Is.EqualTo(expected.IsDecentralized));

            Assert.That(actual.ReplyTo.Count, Is.EqualTo(expected.ReplyTo.Count));
            Assert.That(actual.Cc.Count, Is.EqualTo(expected.Cc.Count));
            Assert.That(actual.Bcc.Count, Is.EqualTo(expected.Bcc.Count));

            Assert.That(actual.Attachments.Count, Is.EqualTo(expected.Attachments.Count));
            Assert.That(actual.Protection.Type, Is.EqualTo(expected.Protection.Type));
        }

        #endregion
    }
}
