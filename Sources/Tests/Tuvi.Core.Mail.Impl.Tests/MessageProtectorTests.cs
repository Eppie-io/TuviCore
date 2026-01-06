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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KeyDerivation.Keys;
using MimeKit;
using MimeKit.Cryptography;
using Moq;
using NUnit.Framework;
using SecurityManagementTests;
using Tuvi.Core.Dec;
using Tuvi.Core.Dec.Impl;
using Tuvi.Core.Entities;
using Tuvi.Core.Mail.Impl.Protocols;
using Tuvi.Core.Utils;
using TuviPgpLib;
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
        private const int CoinType = 3630; // Eppie BIP44 coin type
        private const int Channel = 10;    // Email channel
        private const int KeyIndex = 0;    // Key index

        private static readonly IPublicKeyService _testPublicKeyService = PublicKeyService.CreateDefault(PublicKeyService.NoOpNameResolver);

        private static (Mock<IKeyStorage> storageMock, MasterKey masterKey) CreateKeyStorageMockWithMasterKey()
        {
            var masterKey = EncryptionTestsData.ReceiverMasterKey;
            var storageMock = new Mock<IKeyStorage>();
            storageMock.Setup(s => s.GetMasterKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(masterKey);
            storageMock.Setup(s => s.GetPgpPublicKeysAsync(It.IsAny<CancellationToken>())).ReturnsAsync((TuviPgpLib.Entities.PgpPublicKeyBundle)null);
            storageMock.Setup(s => s.GetPgpSecretKeysAsync(It.IsAny<CancellationToken>())).ReturnsAsync((TuviPgpLib.Entities.PgpSecretKeyBundle)null);
            return (storageMock, masterKey);
        }

        private static TuviPgpContext InitializePgpContext()
        {
            var keyStorage = new MockPgpKeyStorage().Get();
            var context = new TuviPgpContext(keyStorage);
            context.LoadContextAsync().Wait();
            return context;
        }

        private static string DerivePubKey(MasterKey masterKey, int accountIndex)
        {
            var key = EccPgpContext.GenerateEccPublicKey(masterKey, CoinType, accountIndex, Channel, KeyIndex);
            return _testPublicKeyService.Encode(key);
        }

        private sealed class DictNameResolver : IEppieNameResolver
        {
            private readonly Dictionary<string, string> _map;
            public DictNameResolver(Dictionary<string, string> map) => _map = map;
            public Task<string> ResolveAsync(string name, CancellationToken cancellationToken = default)
            {
                _map.TryGetValue(name, out var v);
                return Task.FromResult(v);
            }
        }

        private static PublicKeyService CreatePublicKeyService(Dictionary<string, string> aliasMap)
        {
            return PublicKeyService.CreateDefault(new DictNameResolver(aliasMap));
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

        [Test]
        public async Task AddTwoDecentralizedAddressesOneByOne()
        {
            // Create a key storage mock with a master key
            var (storageMock, masterKey) = CreateKeyStorageMockWithMasterKey();

            // Derive public keys for two different account indices
            var pubKey1 = DerivePubKey(masterKey, 1);
            var pubKey2 = DerivePubKey(masterKey, 2);

            // Create alias names and map them to derived keys
            var alias1 = "alice-test-alias";
            var alias2 = "bob-test-alias";

            var aliasMap = new Dictionary<string, string>
            {
                { alias1, pubKey1 },
                { alias2, pubKey2 }
            };

            var publicKeyService = CreatePublicKeyService(aliasMap);

            // Create decentralized addresses using alias names
            var decentralizedEmail1 = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias1);
            var decentralizedEmail2 = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias2);

            // Verify both addresses are decentralized
            Assert.That(decentralizedEmail1.IsDecentralized, Is.True, "First email is not decentralized");
            Assert.That(decentralizedEmail2.IsDecentralized, Is.True, "Second email is not decentralized");

            using (var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storageMock.Object).ConfigureAwait(false))
            {
                // Initial state - no public keys
                Assert.That(pgpContext.GetPublicKeysInfo(), Is.Empty, "Initial context should have no keys");

                // Add first address
                var emails1 = new List<EmailAddress> { decentralizedEmail1 };
                await pgpContext.TryToAddDecPublicKeysAsync(emails1, publicKeyService, default).ConfigureAwait(false);

                // Verify first key was added
                var keysAfterFirst = pgpContext.GetPublicKeysInfo().Count;
                Assert.That(keysAfterFirst, Is.Positive, "First key should be added to context");

                // Add second address
                var emails2 = new List<EmailAddress> { decentralizedEmail2 };
                await pgpContext.TryToAddDecPublicKeysAsync(emails2, publicKeyService, default).ConfigureAwait(false);

                // Verify second key was added
                var keysAfterSecond = pgpContext.GetPublicKeysInfo().Count;
                Assert.That(keysAfterSecond, Is.GreaterThan(keysAfterFirst), "Second key should be added to context");
            }
        }

        [Test]
        public async Task AddTwoDecentralizedAddressesWithSameKey()
        {
            // Create a key storage mock with a master key
            var (storageMock, masterKey) = CreateKeyStorageMockWithMasterKey();

            var pubKey1 = DerivePubKey(masterKey, 1);

            // Create alias names and map them to derived keys
            var alias1 = "alice-test-alias";
            var alias2 = "bob-test-alias";

            var aliasMap = new Dictionary<string, string>
            {
                { alias1, pubKey1 },
                { alias2, pubKey1 }
            };

            var publicKeyService = CreatePublicKeyService(aliasMap);

            // Create decentralized addresses using alias names
            var decentralizedEmail1 = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias1);
            var decentralizedEmail2 = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias2);

            // Verify both addresses are decentralized
            Assert.That(decentralizedEmail1.IsDecentralized, Is.True, "First email is not decentralized");
            Assert.That(decentralizedEmail2.IsDecentralized, Is.True, "Second email is not decentralized");

            using (var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storageMock.Object).ConfigureAwait(false))
            {
                // Initial state - no public keys
                Assert.That(pgpContext.GetPublicKeysInfo(), Is.Empty, "Initial context should have no keys");

                // Add first address
                var emails1 = new List<EmailAddress> { decentralizedEmail1 };
                await pgpContext.TryToAddDecPublicKeysAsync(emails1, publicKeyService, default).ConfigureAwait(false);

                // Verify first key was added and can be retrieved by first address
                Assert.That(pgpContext.GetPublicKeysInfo(), Is.Not.Empty, "First key should be added to context");
                Assert.That(HasPublicKeyForAddress(pgpContext, decentralizedEmail1), Is.True, "Should be able to get key for first address");

                // Add second address (same key, different alias)
                var emails2 = new List<EmailAddress> { decentralizedEmail2 };
                await pgpContext.TryToAddDecPublicKeysAsync(emails2, publicKeyService, default).ConfigureAwait(false);

                // Verify both addresses can retrieve the key
                // Note: keysAfterSecond may equal keysAfterFirst since it's the same key with merged UserIDs
                Assert.That(HasPublicKeyForAddress(pgpContext, decentralizedEmail1), Is.True, "Should still be able to get key for first address");
                Assert.That(HasPublicKeyForAddress(pgpContext, decentralizedEmail2), Is.True, "Should be able to get key for second address");
            }
        }

        [Test]
        public async Task AddResolvedAddressThenAlias()
        {
            // Scenario: First add <pub_key>@eppie, then add alias <name>@eppie
            var (storageMock, masterKey) = CreateKeyStorageMockWithMasterKey();

            var pubKey = DerivePubKey(masterKey, 1);
            var alias = "alice-test-alias";

            var aliasMap = new Dictionary<string, string>
            {
                { alias, pubKey },
                { pubKey, pubKey } // pub_key resolves to itself
            };

            var publicKeyService = CreatePublicKeyService(aliasMap);

            // Create addresses
            var resolvedEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, pubKey);
            var aliasEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias);

            using (var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storageMock.Object).ConfigureAwait(false))
            {
                // Initial state - no public keys
                Assert.That(pgpContext.GetPublicKeysInfo(), Is.Empty, "Initial context should have no keys");

                // Step 1: Add resolved address (<pub_key>@eppie)
                await pgpContext.TryToAddDecPublicKeysAsync(new[] { resolvedEmail }, publicKeyService, default).ConfigureAwait(false);

                var keysAfterResolved = pgpContext.GetPublicKeysInfo().Count;
                Assert.That(keysAfterResolved, Is.EqualTo(1), "One key should be added");
                Assert.That(HasPublicKeyForAddress(pgpContext, resolvedEmail), Is.True, "Should be able to get key for resolved address");

                // Step 2: Add alias (<name>@eppie) - should merge UserID into existing key
                await pgpContext.TryToAddDecPublicKeysAsync(new[] { aliasEmail }, publicKeyService, default).ConfigureAwait(false);

                var keysAfterAlias = pgpContext.GetPublicKeysInfo().Count;
                Assert.That(keysAfterAlias, Is.EqualTo(1), "Still one key (merged UserIDs)");
                Assert.That(HasPublicKeyForAddress(pgpContext, resolvedEmail), Is.True, "Should still be able to get key for resolved address");
                Assert.That(HasPublicKeyForAddress(pgpContext, aliasEmail), Is.True, "Should be able to get key for alias");
            }
        }

        [Test]
        public async Task AddAliasThenResolvedAddress()
        {
            // Scenario: First add alias <name>@eppie, then add <pub_key>@eppie
            var (storageMock, masterKey) = CreateKeyStorageMockWithMasterKey();

            var pubKey = DerivePubKey(masterKey, 1);
            var alias = "alice-test-alias";

            var aliasMap = new Dictionary<string, string>
            {
                { alias, pubKey },
                { pubKey, pubKey } // pub_key resolves to itself
            };

            var publicKeyService = CreatePublicKeyService(aliasMap);

            // Create addresses
            var aliasEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias);
            var resolvedEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, pubKey);

            using (var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storageMock.Object).ConfigureAwait(false))
            {
                // Initial state - no public keys
                Assert.That(pgpContext.GetPublicKeysInfo(), Is.Empty, "Initial context should have no keys");

                // Step 1: Add alias (<name>@eppie)
                await pgpContext.TryToAddDecPublicKeysAsync(new[] { aliasEmail }, publicKeyService, default).ConfigureAwait(false);

                var keysAfterAlias = pgpContext.GetPublicKeysInfo().Count;
                Assert.That(keysAfterAlias, Is.EqualTo(1), "One key should be added");
                Assert.That(HasPublicKeyForAddress(pgpContext, aliasEmail), Is.True, "Should be able to get key for alias");

                // Step 2: Add resolved address (<pub_key>@eppie) - should merge UserID into existing key
                await pgpContext.TryToAddDecPublicKeysAsync(new[] { resolvedEmail }, publicKeyService, default).ConfigureAwait(false);

                var keysAfterResolved = pgpContext.GetPublicKeysInfo().Count;
                Assert.That(keysAfterResolved, Is.EqualTo(1), "Still one key (merged UserIDs)");
                Assert.That(HasPublicKeyForAddress(pgpContext, aliasEmail), Is.True, "Should still be able to get key for alias");
                Assert.That(HasPublicKeyForAddress(pgpContext, resolvedEmail), Is.True, "Should be able to get key for resolved address");
            }
        }

        private static bool HasPublicKeyForAddress(OpenPgpContext context, EmailAddress emailAddress)
        {
            try
            {
                var mailboxAddress = emailAddress.ToMailboxAddress();
                var keys = context.GetPublicKeys(new List<MailboxAddress> { mailboxAddress }, default);
                return keys.Any();
            }
            catch (PublicKeyNotFoundException)
            {
                return false;
            }
        }
    }
}
