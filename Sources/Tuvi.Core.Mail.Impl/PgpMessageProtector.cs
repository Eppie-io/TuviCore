using System;
using System.Threading;
using System.Threading.Tasks;
using MimeKit.Cryptography;
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;
using Tuvi.Core.Mail.Impl.Protocols;
using TuviPgpLib;
using TuviPgpLib.Entities;

namespace Tuvi.Core.Mail.Impl
{
    public static class MessageProtectorCreator
    {
        public static IMessageProtector GetMessageProtector(ITuviPgpContext pgpContext)
        {
            return new PgpMessageProtector(pgpContext);
        }
    }

    internal class PgpMessageProtector : IMessageProtector
    {
        private readonly OpenPgpContext PgpContext;

        /// <summary>
        /// <paramref name="pgpContext"/> has to be child of <see cref="MimeKit.Cryptography.OpenPgpContext"/>.
        /// Unfortunately MimeKit has no internal interfaces but only abstract classes which can't be inherited by application interfaces.
        /// To prevent passing of incompatible objects <see cref="IncompatibleCryptoContextException"/> is thrown.
        /// <paramref name="pgpContext"/> isn't used directly as an argument type to prevent spreading of <see cref="MimeKit"/> library over application modules.
        /// </summary>
        /// <exception cref="IncompatibleCryptoContextException"/>
        public PgpMessageProtector(ITuviPgpContext pgpContext)
        {
            PgpContext = pgpContext as OpenPgpContext;
            if (PgpContext == null)
            {
                throw new IncompatibleCryptoContextException();
            }
        }

        public Message Sign(Message message)
        {
            try
            {
                return TrySignMessage(message);
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

        public Message Encrypt(Message message)
        {
            try
            {
                return TryEncryptMessage(message);
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

        public Message SignAndEncrypt(Message message)
        {
            try
            {
                return TrySignAndEncryptMessage(message);
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

        private Message TrySignMessage(Message message)
        {
            using (var mimeMessage = message.ToMimeMessage())
            {
                mimeMessage.Sign(PgpContext);

                message.SetMimeBody(mimeMessage.Body);
                message.ClearUnprotectedBody();
                message.Protection.Type = MessageProtectionType.Signature;

                return message;
            }
        }

        private Message TryEncryptMessage(Message message)
        {
            AddReceiverKeysToContext(message);
            using (var mimeMessage = message.ToMimeMessage())
            {
                mimeMessage.Encrypt(PgpContext);

                message.SetMimeBody(mimeMessage.Body);
                message.ClearUnprotectedBody();
                message.Protection.Type = MessageProtectionType.Encryption;

                return message;
            }
        }

        private Message TrySignAndEncryptMessage(Message message)
        {
            AddReceiverKeysToContext(message);
            using (var mimeMessage = message.ToMimeMessage())
            {
                mimeMessage.SignAndEncrypt(PgpContext);

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
                //AddSenderKeysToContext(message);
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
                //AddSenderKeysToContext(message);
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

        private void AddReceiverKeysToContext(Message message)
        {
            foreach (var recipient in message.AllRecipients)
            {
                PgpContext.TryToAddDecEncryptionKey(recipient);
            }
        }

        //private void AddSenderKeysToContext(Message message)
        //{
        //    foreach (var sender in message.From)
        //    {
        //        PgpContext.TryToAddDecSignerPublicKey(sender);
        //    }
        //}
    }
}
