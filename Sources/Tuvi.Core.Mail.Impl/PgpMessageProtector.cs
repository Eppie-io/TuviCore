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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;
using MimeKit.Cryptography;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Parameters;
using Tuvi.Core.Entities;
using Tuvi.Core.Mail.Impl.Protocols;
using Tuvi.Core.Utils;
using TuviPgpLib;
using TuviPgpLib.Entities;
using TuviPgpLibImpl;

namespace Tuvi.Core.Mail.Impl
{
    public static class MessageProtectorCreator
    {
        public static IMessageProtector GetMessageProtector(ITuviPgpContext pgpContext, IPublicKeyService publicKeyService)
        {
            return new PgpMessageProtector(pgpContext, publicKeyService);
        }
    }

    internal class PgpMessageProtector : IMessageProtector
    {
        private const DigestAlgorithm DefaultDigestAlgorithm = DigestAlgorithm.Sha256;
        private readonly OpenPgpContext PgpContext;
        private readonly IPublicKeyService PublicKeyService;

        /// <summary>
        /// <paramref name="pgpContext"/> has to be child of <see cref="MimeKit.Cryptography.OpenPgpContext"/>.
        /// Unfortunately MimeKit has no internal interfaces but only abstract classes which can't be inherited by application interfaces.
        /// To prevent passing of incompatible objects <see cref="IncompatibleCryptoContextException"/> is thrown.
        /// <paramref name="pgpContext"/> isn't used directly as an argument type to prevent spreading of <see cref="MimeKit"/> library over application modules.
        /// </summary>
        /// <exception cref="IncompatibleCryptoContextException"/>
        public PgpMessageProtector(ITuviPgpContext pgpContext, IPublicKeyService publicKeyService)
        {
            PgpContext = pgpContext as OpenPgpContext;
            if (PgpContext is null)
            {
                throw new IncompatibleCryptoContextException();
            }

            PublicKeyService = publicKeyService;
        }

        public async Task<Message> SignAsync(Message message, CancellationToken cancellationToken)
        {
            try
            {
                return await TrySignMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (PrivateKeyNotFoundException e)
            {
                throw new NoSecretKeyException(e.KeyId, e);
            }
            catch (Exception e)
            {
                throw new MessageSigningException(e.Message, e);
            }
        }

        public async Task<Message> EncryptAsync(Message message, CancellationToken cancellationToken)
        {
            try
            {
                return await TryEncryptMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (PublicKeyNotFoundException e)
            {
                throw new NoPublicKeyException(e.Mailbox.ToEmailAddress(), e);
            }
            catch (Exception e)
            {
                throw new MessageEncryptionException(e.Message, e);
            }
        }

        public async Task<Message> SignAndEncryptAsync(Message message, CancellationToken cancellationToken)
        {
            try
            {
                return await TrySignAndEncryptMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (PrivateKeyNotFoundException e)
            {
                throw new NoSecretKeyException(e.KeyId, e);
            }
            catch (PublicKeyNotFoundException e)
            {
                throw new NoPublicKeyException(e.Mailbox.ToEmailAddress(), e);
            }
            catch (Exception e)
            {
                throw new MessageEncryptionException(e.Message, e);
            }
        }

        public async Task<Message> TryVerifyAndDecryptAsync(Message message, CancellationToken cancellationToken)
        {
            if (message.Protection.Type == MessageProtectionType.Signature)
            {
                return await VerifyMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
            else if (message.Protection.Type == MessageProtectionType.Encryption
                  || message.Protection.Type == MessageProtectionType.SignatureAndEncryption)
            {
                return await DecryptMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return message;
            }
        }

        private async Task<Message> TrySignMessageAsync(Message message, CancellationToken cancellationToken)
        {
            using (var mimeMessage = message.ToMimeMessage())
            {
                await mimeMessage.SignAsync(PgpContext, DefaultDigestAlgorithm, cancellationToken).ConfigureAwait(false);

                message.SetMimeBody(mimeMessage.Body);
                message.ClearUnprotectedBody();
                message.Protection.Type = MessageProtectionType.Signature;

                return message;
            }
        }

        private async Task<Message> TryEncryptMessageAsync(Message message, CancellationToken cancellationToken)
        {
            await AddReceiverKeysToContextAsync(message, cancellationToken).ConfigureAwait(false);

            using (var mimeMessage = message.ToMimeMessage())
            {
                mimeMessage.Encrypt(PgpContext, cancellationToken);

                message.SetMimeBody(mimeMessage.Body);
                message.ClearUnprotectedBody();
                message.Protection.Type = MessageProtectionType.Encryption;

                return message;
            }
        }

        private async Task<Message> TrySignAndEncryptMessageAsync(Message message, CancellationToken cancellationToken)
        {
            await AddReceiverKeysToContextAsync(message, cancellationToken).ConfigureAwait(false);

            using (var mimeMessage = message.ToMimeMessage())
            {
                mimeMessage.SignAndEncrypt(PgpContext, DefaultDigestAlgorithm, cancellationToken);

                message.SetMimeBody(mimeMessage.Body);
                message.ClearUnprotectedBody();
                message.Protection.Type = MessageProtectionType.SignatureAndEncryption;

                return message;
            }
        }

        private async Task<Message> DecryptMessageAsync(Message message, CancellationToken cancellationToken)
        {
            try
            {
                return await TryDecryptMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (PrivateKeyNotFoundException e)
            {
                throw new NoSecretKeyException(e.KeyId, e);
            }
            catch (Exception e)
            {
                throw new MessageDecryptionException(e.Message, e);
            }
        }

        private async Task<Message> VerifyMessageAsync(Message message, CancellationToken cancellationToken)
        {
            try
            {
                return await TryVerifyMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new MessageSignatureVerificationException(e.Message, e);
            }
        }

        private async Task<Message> TryDecryptMessageAsync(Message message, CancellationToken cancellationToken)
        {
            message.Attachments.Clear();

            var mimeBody = message.MimeBody.ToMimeEntity(cancellationToken);
            if (mimeBody is MultipartEncrypted encryptedBody)
            {
                await AddSenderKeysToContextAsync(message, cancellationToken).ConfigureAwait(false);

                var body = encryptedBody.Decrypt(PgpContext, out DigitalSignatureCollection signatures, cancellationToken);
                if (body is MultipartSigned signedBody)
                {
                    signatures = await TryVerifySignedBodyAsync(signedBody, cancellationToken).ConfigureAwait(false);
                    body = signedBody[0];
                }

                await message.AddContentAsync(body, cancellationToken).ConfigureAwait(false);

                if (signatures?.Count > 0)
                {
                    message.Protection.Type = MessageProtectionType.SignatureAndEncryption;
                    message.AddSignatures(signatures);
                }
            }

            return message;
        }

        private async Task<Message> TryVerifyMessageAsync(Message message, CancellationToken cancellationToken)
        {
            message.Attachments.Clear();

            var mimeBody = message.MimeBody.ToMimeEntity(cancellationToken);
            if (mimeBody is MultipartSigned signedBody)
            {
                await AddSenderKeysToContextAsync(message, cancellationToken).ConfigureAwait(false);

                var signatures = await TryVerifySignedBodyAsync(signedBody, cancellationToken).ConfigureAwait(false);

                var body = signedBody[0];
                await message.AddContentAsync(body, cancellationToken).ConfigureAwait(false);

                if (signatures?.Count > 0)
                {
                    message.AddSignatures(signatures);
                }
            }

            return message;
        }

        private async Task<DigitalSignatureCollection> TryVerifySignedBodyAsync(MultipartSigned multipartSigned, CancellationToken cancellationToken)
        {
            // TODO: TVM-230 Here we can catch MimeKit signature verification exceptions
            // not to break on all the message processing.
            try
            {
                return await multipartSigned.VerifyAsync(PgpContext, cancellationToken).ConfigureAwait(false);
            }
            catch (FormatException) { }
            catch (NotSupportedException) { }
            catch (Org.BouncyCastle.Cms.CmsException) { }

            return null;
        }

        private Task AddReceiverKeysToContextAsync(Message message, CancellationToken cancellationToken)
        {
            return PgpContext.TryToAddDecPublicKeysAsync(message.AllRecipients, PublicKeyService, cancellationToken);
        }

        private Task AddSenderKeysToContextAsync(Message message, CancellationToken cancellationToken)
        {
            return PgpContext.TryToAddDecPublicKeysAsync(message.From, PublicKeyService, cancellationToken);
        }
    }

    public static class MessageProtectorExtensions
    {
        public static async Task TryToAddDecPublicKeysAsync(this OpenPgpContext context, IEnumerable<EmailAddress> emails, IPublicKeyService publicKeyService, CancellationToken cancellationToken)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (emails is null)
            {
                throw new ArgumentNullException(nameof(emails));
            }

            if (publicKeyService is null)
            {
                throw new ArgumentNullException(nameof(publicKeyService));
            }

            foreach (var emailAddress in emails)
            {
                if (emailAddress is null)
                {
                    throw new ArgumentException("Email address in collection cannot be null.", nameof(emails));
                }

                if (!emailAddress.IsDecentralized)
                {
                    continue;
                }

                // Key already exists for this exact address - skip
                if (HasPublicKey(context, emailAddress, cancellationToken))
                {
                    continue;
                }

                // Resolve alias to public key address
                var resolvedAddress = await TryResolveAddressAsync(emailAddress, publicKeyService, cancellationToken).ConfigureAwait(false);
                if (resolvedAddress is null)
                {
                    continue;
                }

                // Import key with emailAddress as UserID
                // ImportOrMerge handles both cases:
                // - Key doesn't exist yet: adds new key
                // - Key exists (under resolved or alias address): adds emailAddress as new UserID
                await ImportOrMergePublicKeyAsync(context, emailAddress, resolvedAddress, publicKeyService, cancellationToken).ConfigureAwait(false);
            }
        }

        private static bool HasPublicKey(OpenPgpContext context, EmailAddress emailAddress, CancellationToken cancellationToken)
        {
            try
            {
                var mailboxAddress = emailAddress.ToMailboxAddress();
                var keys = context.GetPublicKeys(new List<MailboxAddress> { mailboxAddress }, cancellationToken);
                return keys.Any();
            }
            catch (PublicKeyNotFoundException)
            {
                return false;
            }
        }

        private static async Task<EmailAddress> TryResolveAddressAsync(EmailAddress emailAddress, IPublicKeyService publicKeyService, CancellationToken cancellationToken)
        {
            try
            {
                var publicKey = await publicKeyService.GetEncodedByEmailAsync(emailAddress, cancellationToken).ConfigureAwait(false);
                // Create the appropriate Eppie address
                return EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, publicKey);
            }
            catch (Exception ex) when (ex is NoPublicKeyException || ex is NotSupportedException)
            {
                return null;
            }
        }

        private static async Task ImportOrMergePublicKeyAsync(OpenPgpContext context, EmailAddress emailAddress, EmailAddress resolvedAddress, IPublicKeyService publicKeyService, CancellationToken cancellationToken)
        {
            ECPublicKeyParameters publicKey = await publicKeyService.GetByEmailAsync(resolvedAddress, cancellationToken).ConfigureAwait(false);
            PgpPublicKeyRing keyRing = TuviPgpContext.CreatePgpPublicKeyRing(publicKey, publicKey, emailAddress.Address);

            if (context is ExternalStorageBasedPgpContext externalContext)
            {
                externalContext.ImportOrMerge(keyRing, cancellationToken);
            }
            else
            {
                context.Import(keyRing, cancellationToken);
            }
        }
    }
}
