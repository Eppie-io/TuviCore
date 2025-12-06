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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;
using MimeKit.Cryptography;
using Moq;
using NUnit.Framework;
using SecurityManagementTests;
using Tuvi.Core.Entities;
using Tuvi.Core.Mail.Impl.Protocols;
using Tuvi.Core.Utils;
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

        private static readonly IPublicKeyService _publicKeyService = PublicKeyService.CreateDefault(PublicKeyService.NoOpNameResolver);

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

                var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext, _publicKeyService);
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

                var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext, _publicKeyService);
                message = messageProtector.TryVerifyAndDecryptAsync(message).Result;

                Assert.That(
                    MessageProtectionType.SignatureAndEncryption,
                    Is.EqualTo(message.Protection.Type),
                    "Decrypted message protection type was not properly set.");

                Assert.That(message.TextBody.SequenceEqual(EncryptionTestsData.PlainText), Is.True, "Decrypted message content has been altered");
            }
        }

        [Test]
        public async Task TwoPartiesInteroperation()
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

                var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext, _publicKeyService);
                message = await messageProtector.SignAndEncryptAsync(message, default).ConfigureAwait(true);

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

                var messageProtector = MessageProtectorCreator.GetMessageProtector(pgpContext, _publicKeyService);
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
    public class MessageProtectorExtensionsTests
    {
        private static TuviPgpContext InitializePgpContext()
        {
            var keyStorage = new MockPgpKeyStorage().Get();
            var context = new TuviPgpContext(keyStorage);
            context.LoadContextAsync().Wait();
            return context;
        }

        [Test]
        public async Task AddDecentralizedAddressWithKeyLoaded()
        {
            using (var pgpContext = InitializePgpContext())
            {
                var decentralizedEmail = AccountInfo.GetAccount2().Email;
                pgpContext.GeneratePgpKeysByTagOld(EncryptionTestsData.ReceiverMasterKey, AccountInfo.GetAccount2().GetPgpUserIdentity(), AccountInfo.GetAccount2().GetKeyTag());

                var emails = new List<EmailAddress> { decentralizedEmail };
                var publicKeyServiceMock = new Mock<IPublicKeyService>();

                await pgpContext.TryToAddDecPublicKeysAsync(emails, publicKeyServiceMock.Object, default).ConfigureAwait(false);

                publicKeyServiceMock.Verify(x => x.GetEncodedByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
                publicKeyServiceMock.Verify(x => x.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        [Test]
        public async Task SkipNonDecentralizedAddresses()
        {
            using (var pgpContext = InitializePgpContext())
            {
                var standardEmail = new EmailAddress("standard@example.com", "Standard User");
                var emails = new List<EmailAddress> { standardEmail };
                var publicKeyServiceMock = new Mock<IPublicKeyService>();

                await pgpContext.TryToAddDecPublicKeysAsync(emails, publicKeyServiceMock.Object, default).ConfigureAwait(false);

                publicKeyServiceMock.Verify(x => x.GetEncodedByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
                publicKeyServiceMock.Verify(x => x.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        [Test]
        public void EmptyEmailListDoesNotThrow()
        {
            using (var pgpContext = InitializePgpContext())
            {
                var emails = new List<EmailAddress>();
                var publicKeyServiceMock = new Mock<IPublicKeyService>();

                Assert.DoesNotThrowAsync(async () =>
                    await pgpContext.TryToAddDecPublicKeysAsync(emails, publicKeyServiceMock.Object, default).ConfigureAwait(false));

                publicKeyServiceMock.Verify(x => x.GetEncodedByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        [Test]
        public async Task HandleNoPublicKeyException()
        {
            using (var pgpContext = InitializePgpContext())
            {
                var decentralizedEmail = AccountInfo.GetAccount2().Email;
                var emails = new List<EmailAddress> { decentralizedEmail };
                var publicKeyServiceMock = new Mock<IPublicKeyService>();

                publicKeyServiceMock
                    .Setup(x => x.GetEncodedByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new NoPublicKeyException(decentralizedEmail, (Exception)null));

                publicKeyServiceMock
                    .Setup(x => x.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new NoPublicKeyException(decentralizedEmail, (Exception)null));

                await pgpContext.TryToAddDecPublicKeysAsync(emails, publicKeyServiceMock.Object, default).ConfigureAwait(false);

                publicKeyServiceMock.Verify(x => x.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        [Test]
        public async Task HandleNotSupportedException()
        {
            using (var pgpContext = InitializePgpContext())
            {
                var decentralizedEmail = AccountInfo.GetAccount2().Email;
                var emails = new List<EmailAddress> { decentralizedEmail };
                var publicKeyServiceMock = new Mock<IPublicKeyService>();

                publicKeyServiceMock
                    .Setup(x => x.GetEncodedByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new NotSupportedException("Not supported"));

                await pgpContext.TryToAddDecPublicKeysAsync(emails, publicKeyServiceMock.Object, default).ConfigureAwait(false);

                publicKeyServiceMock.Verify(x => x.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        [Test]
        public async Task MultipleAddressesProcessedCorrectly()
        {
            using (var pgpContext = InitializePgpContext())
            {
                var decentralizedEmail1 = AccountInfo.GetAccount().Email;
                var decentralizedEmail2 = AccountInfo.GetAccount2().Email;
                var standardEmail = new EmailAddress("standard@example.com", "Standard User");

                pgpContext.GeneratePgpKeysByTagOld(EncryptionTestsData.SenderMasterKey, AccountInfo.GetAccount().GetPgpUserIdentity(), AccountInfo.GetAccount().GetKeyTag());
                pgpContext.GeneratePgpKeysByTagOld(EncryptionTestsData.ReceiverMasterKey, AccountInfo.GetAccount2().GetPgpUserIdentity(), AccountInfo.GetAccount2().GetKeyTag());

                var emails = new List<EmailAddress> { decentralizedEmail1, standardEmail, decentralizedEmail2 };
                var publicKeyServiceMock = new Mock<IPublicKeyService>();

                await pgpContext.TryToAddDecPublicKeysAsync(emails, publicKeyServiceMock.Object, default).ConfigureAwait(false);

                publicKeyServiceMock.Verify(x => x.GetEncodedByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        [Test]
        public void NullContextThrowsArgumentNullException()
        {
            OpenPgpContext nullContext = null;
            var emails = new List<EmailAddress>();
            var publicKeyServiceMock = new Mock<IPublicKeyService>();

            // When null context is passed, ArgumentNullException should be thrown
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await nullContext.TryToAddDecPublicKeysAsync(emails, publicKeyServiceMock.Object, default).ConfigureAwait(false));
        }

        [Test]
        public void NullEmailsThrowsArgumentNullException()
        {
            using (var pgpContext = InitializePgpContext())
            {
                var publicKeyServiceMock = new Mock<IPublicKeyService>();

                // When null emails are passed, ArgumentNullException should be thrown
                Assert.ThrowsAsync<ArgumentNullException>(async () =>
                    await pgpContext.TryToAddDecPublicKeysAsync(null, publicKeyServiceMock.Object, default).ConfigureAwait(false));
            }
        }

        [Test]
        public void NullPublicKeyServiceThrowsArgumentNullException()
        {
            using (var pgpContext = InitializePgpContext())
            {
                var emails = new List<EmailAddress>();

                // When null publicKeyService is passed, ArgumentNullException should be thrown
                Assert.ThrowsAsync<ArgumentNullException>(async () =>
                    await pgpContext.TryToAddDecPublicKeysAsync(emails, null, default).ConfigureAwait(false));
            }
        }
    }
}
