using MimeKit;
using MimeKit.Cryptography;
using NUnit.Framework;
using SecurityManagementTests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl.SecurityManagement;
using Tuvi.Core.Mail.Impl.Protocols;
using TuviPgpLib.Entities;
using TuviPgpLibImpl;

namespace Tuvi.Core.Mail.Impl.Tests
{
    public class MessageProtectorTests
    {
        private static TuviPgpContext InitializePgpContext()
        {
            var keyStorage = new MockPgpKeyStorage().Get();
            var context = new TuviPgpContext(keyStorage);
            context.LoadContextAsync().Wait();
            return context;
        }

        [Test]
        public void VerifySignedMessage()
        {
            using (var pgpContext = InitializePgpContext())
            {
                var receiverAddress = AccountInfo.GetAccount2().Email;
                var senderAddress = AccountInfo.GetAccount().Email;

                pgpContext.GeneratePgpKeysByTagOld(EncryptionTestsData.ReceiverMasterKey, AccountInfo.GetAccount2().GetPgpUserIdentity(), AccountInfo.GetAccount2().GetKeyTag());
                pgpContext.GeneratePgpKeysByTagOld(EncryptionTestsData.SenderMasterKey, AccountInfo.GetAccount().GetPgpUserIdentity(), AccountInfo.GetAccount().GetKeyTag());

                using MimeMessage mimeMessage = new MimeMessage();
                mimeMessage.To.Add(receiverAddress.ToMailboxAddress());
                mimeMessage.From.Add(senderAddress.ToMailboxAddress());
                mimeMessage.Subject = EncryptionTestsData.Subject;
                mimeMessage.Body = new TextPart { Text = EncryptionTestsData.PlainText };
                mimeMessage.Sign(pgpContext);

                Assert.That(mimeMessage.Body is MultipartSigned, Is.True, "Message body was not properly signed");

                Message message = mimeMessage.ToTuviMailMessage(new Folder());

                Assert.That(
                    MessageProtectionType.Signature,
                    Is.EqualTo(message.Protection.Type),
                    "Signed message protection type was not properly set.");

                var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext);
                message = messageProtector.TryVerifyAndDecryptAsync(message).Result;

                Assert.That(
                    MessageProtectionType.Signature,
                    Is.EqualTo(message.Protection.Type),
                    "Verified message protection type was not properly set.");

                Assert.That(message.TextBody.SequenceEqual(EncryptionTestsData.PlainText), Is.True, "Decrypted message content has been altered");
            }
        }

        [Test]
        public void DecryptMessage()
        {
            using (var pgpContext = InitializePgpContext())
            {
                var receiverAddress = AccountInfo.GetAccount2().Email;
                var senderAddress = AccountInfo.GetAccount().Email;

                pgpContext.GeneratePgpKeysByTagOld(EncryptionTestsData.ReceiverMasterKey, AccountInfo.GetAccount2().GetPgpUserIdentity(), AccountInfo.GetAccount2().GetKeyTag());
                pgpContext.GeneratePgpKeysByTagOld(EncryptionTestsData.SenderMasterKey, AccountInfo.GetAccount().GetPgpUserIdentity(), AccountInfo.GetAccount().GetKeyTag());

                using MimeMessage mimeMessage = new MimeMessage();
                mimeMessage.To.Add(receiverAddress.ToMailboxAddress());
                mimeMessage.From.Add(senderAddress.ToMailboxAddress());
                mimeMessage.Subject = EncryptionTestsData.Subject;
                mimeMessage.Body = new TextPart { Text = EncryptionTestsData.PlainText };
                mimeMessage.SignAndEncrypt(pgpContext);

                Assert.That(mimeMessage.Body is MultipartEncrypted, Is.True, "Message body was not properly encrypted");

                Message message = mimeMessage.ToTuviMailMessage(new Folder());

                Assert.That(
                    MessageProtectionType.Encryption,
                    Is.EqualTo(message.Protection.Type),
                    "Signed and encrypted message protection type was not properly set.");

                var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext);
                message = messageProtector.TryVerifyAndDecryptAsync(message).Result;

                Assert.That(
                    MessageProtectionType.SignatureAndEncryption,
                    Is.EqualTo(message.Protection.Type),
                    "Decrypted message protection type was not properly set.");

                Assert.That(message.TextBody.SequenceEqual(EncryptionTestsData.PlainText), Is.True, "Decrypted message content has been altered");
            }
        }

        [Test]
        public void TwoPartiesInteroperation()
        {
            Stream receiverPubKey = new MemoryStream();
            Stream senderPubKey = new MemoryStream();
            MimeMessage mimeMessage;

            // export receiver pub key - used to encrypt message
            using (var pgpContext = InitializePgpContext())
            {
                pgpContext.GeneratePgpKeysByTagOld(EncryptionTestsData.ReceiverMasterKey, AccountInfo.GetAccount2().GetPgpUserIdentity(), AccountInfo.GetAccount2().GetKeyTag());
                pgpContext.ExportPublicKeys(new List<UserIdentity> { AccountInfo.GetAccount2().Email.ToUserIdentity() }, receiverPubKey, true);
            }

            // export sender pub key - used to verify signature
            using (var pgpContext = InitializePgpContext())
            {
                pgpContext.GeneratePgpKeysByTagOld(EncryptionTestsData.SenderMasterKey, AccountInfo.GetAccount().GetPgpUserIdentity(), AccountInfo.GetAccount().GetKeyTag());
                pgpContext.ExportPublicKeys(new List<UserIdentity> { AccountInfo.GetAccount().Email.ToUserIdentity() }, senderPubKey, true);
            }

            receiverPubKey.Position = 0;
            senderPubKey.Position = 0;

            // sign and encrypt message
            using (var pgpContext = InitializePgpContext())
            {
                pgpContext.GeneratePgpKeysByTagOld(EncryptionTestsData.SenderMasterKey, AccountInfo.GetAccount().GetPgpUserIdentity(), AccountInfo.GetAccount().GetKeyTag());
                pgpContext.ImportPublicKeys(receiverPubKey, true);

                Message message = new Message();
                message.To.Add(AccountInfo.GetAccount2().Email);
                message.From.Add(AccountInfo.GetAccount().Email);
                message.Subject = EncryptionTestsData.Subject;
                message.TextBody = EncryptionTestsData.PlainText;
                message.HtmlBody = EncryptionTestsData.HtmlText;
                message.Attachments.Add(EncryptionTestsData.Attachment);
                message.Folder = new Folder();

                var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext);
                message = messageProtector.SignAndEncrypt(message);

                Assert.That(
                    MessageProtectionType.SignatureAndEncryption,
                    Is.EqualTo(message.Protection.Type),
                    "Encrypted message protection type was not properly set.");

                Assert.That(
                    message.MimeBody,
                    Is.Not.Null,
                    "Encrypted data is empty");

                mimeMessage = message.ToMimeMessage();
            }

            // decrypt and verify message
            using (var pgpContext = InitializePgpContext())
            {
                pgpContext.GeneratePgpKeysByTagOld(EncryptionTestsData.ReceiverMasterKey, AccountInfo.GetAccount2().GetPgpUserIdentity(), AccountInfo.GetAccount2().GetKeyTag());
                pgpContext.ImportPublicKeys(senderPubKey, true);

                var message = mimeMessage.ToTuviMailMessage(new Folder());

                var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext);
                message = messageProtector.TryVerifyAndDecryptAsync(message).Result;

                Assert.That(
                    MessageProtectionType.SignatureAndEncryption,
                    Is.EqualTo(message.Protection.Type),
                    "Decrypted message protection type was not properly set.");

                Assert.That(
                    message.TextBody.SequenceEqual(EncryptionTestsData.PlainText),
                    Is.True,
                    "Message text content was corrupted.");

                Assert.That(
                    message.HtmlBody.SequenceEqual(EncryptionTestsData.HtmlText),
                    Is.True,
                    "Message html content was corrupted.");

                Assert.That(
                    EncryptionTestsData.Attachment,
                    Is.EqualTo(message.Attachments.FirstOrDefault()),
                    "Message attachment was corrupted.");
            }
        }        
    }
}
