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

                pgpContext.DeriveKeyPair(EncryptionTestsData.ReceiverMasterKey, AccountInfo.GetAccount2().GetPgpUserIdentity());
                pgpContext.DeriveKeyPair(EncryptionTestsData.SenderMasterKey, AccountInfo.GetAccount().GetPgpUserIdentity());

                using MimeMessage mimeMessage = new MimeMessage();
                mimeMessage.To.Add(receiverAddress.ToMailBoxAddres());
                mimeMessage.From.Add(senderAddress.ToMailBoxAddres());
                mimeMessage.Subject = EncryptionTestsData.Subject;
                mimeMessage.Body = new TextPart { Text = EncryptionTestsData.PlainText };
                mimeMessage.Sign(pgpContext);

                Assert.IsTrue(mimeMessage.Body is MultipartSigned, "Message body was not properly signed");

                Message message = mimeMessage.ToTuviMailMessage(new Folder());

                Assert.AreEqual(
                    MessageProtectionType.Signature,
                    message.Protection.Type,
                    "Signed message protection type was not properly set.");

                var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext);
                message = messageProtector.TryVerifyAndDecryptAsync(message).Result;

                Assert.AreEqual(
                    MessageProtectionType.Signature,
                    message.Protection.Type,
                    "Verified message protection type was not properly set.");

                Assert.IsTrue(message.TextBody.SequenceEqual(EncryptionTestsData.PlainText), "Decrypted message content has been altered");
            }
        }

        [Test]
        public void DecryptMessage()
        {
            using (var pgpContext = InitializePgpContext())
            {
                var receiverAddress = AccountInfo.GetAccount2().Email;
                var senderAddress = AccountInfo.GetAccount().Email;

                pgpContext.DeriveKeyPair(EncryptionTestsData.ReceiverMasterKey, AccountInfo.GetAccount2().GetPgpUserIdentity());
                pgpContext.DeriveKeyPair(EncryptionTestsData.SenderMasterKey, AccountInfo.GetAccount().GetPgpUserIdentity());

                using MimeMessage mimeMessage = new MimeMessage();
                mimeMessage.To.Add(receiverAddress.ToMailBoxAddres());
                mimeMessage.From.Add(senderAddress.ToMailBoxAddres());
                mimeMessage.Subject = EncryptionTestsData.Subject;
                mimeMessage.Body = new TextPart { Text = EncryptionTestsData.PlainText };
                mimeMessage.SignAndEncrypt(pgpContext);

                Assert.IsTrue(mimeMessage.Body is MultipartEncrypted, "Message body was not properly encrypted");

                Message message = mimeMessage.ToTuviMailMessage(new Folder());

                Assert.AreEqual(
                    MessageProtectionType.Encryption,
                    message.Protection.Type,
                    "Signed and encrypted message protection type was not properly set.");

                var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext);
                message = messageProtector.TryVerifyAndDecryptAsync(message).Result;

                Assert.AreEqual(
                    MessageProtectionType.SignatureAndEncryption,
                    message.Protection.Type,
                    "Decrypted message protection type was not properly set.");

                Assert.IsTrue(message.TextBody.SequenceEqual(EncryptionTestsData.PlainText), "Decrypted message content has been altered");
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
                pgpContext.DeriveKeyPair(EncryptionTestsData.ReceiverMasterKey, AccountInfo.GetAccount2().GetPgpUserIdentity());
                pgpContext.ExportPublicKeys(new List<UserIdentity> { AccountInfo.GetAccount2().Email.ToUserIdentity() }, receiverPubKey, true);
            }

            // export sender pub key - used to verify signature
            using (var pgpContext = InitializePgpContext())
            {
                pgpContext.DeriveKeyPair(EncryptionTestsData.SenderMasterKey, AccountInfo.GetAccount().GetPgpUserIdentity());
                pgpContext.ExportPublicKeys(new List<UserIdentity> { AccountInfo.GetAccount().Email.ToUserIdentity() }, senderPubKey, true);
            }

            receiverPubKey.Position = 0;
            senderPubKey.Position = 0;

            // sign and encrypt message
            using (var pgpContext = InitializePgpContext())
            {
                pgpContext.DeriveKeyPair(EncryptionTestsData.SenderMasterKey, AccountInfo.GetAccount().GetPgpUserIdentity());
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

                Assert.AreEqual(
                    MessageProtectionType.SignatureAndEncryption,
                    message.Protection.Type,
                    "Encrypted message protection type was not properly set.");

                Assert.IsNotNull(
                    message.MimeBody,
                    "Encrypted data is empty");

                mimeMessage = message.ToMimeMessage();
            }

            // decrypt and verify message
            using (var pgpContext = InitializePgpContext())
            {
                pgpContext.DeriveKeyPair(EncryptionTestsData.ReceiverMasterKey, AccountInfo.GetAccount2().GetPgpUserIdentity());
                pgpContext.ImportPublicKeys(senderPubKey, true);

                var message = mimeMessage.ToTuviMailMessage(new Folder());

                var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext);
                message = messageProtector.TryVerifyAndDecryptAsync(message).Result;

                Assert.AreEqual(
                    MessageProtectionType.SignatureAndEncryption,
                    message.Protection.Type,
                    "Decrypted message protection type was not properly set.");

                Assert.IsTrue(
                    message.TextBody.SequenceEqual(EncryptionTestsData.PlainText),
                    "Message text content was corrupted.");

                Assert.IsTrue(
                    message.HtmlBody.SequenceEqual(EncryptionTestsData.HtmlText),
                    "Message html content was corrupted.");

                Assert.AreEqual(
                    EncryptionTestsData.Attachment,
                    message.Attachments.FirstOrDefault(),
                    "Message attachment was corrupted.");
            }
        }        
    }
}
