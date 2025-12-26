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
using MimeKit.Cryptography;
using Tuvi.Core.Entities;
using Tuvi.Core.Mail.Impl.Protocols.IMAP;

namespace Tuvi.Core.Mail.Impl.Protocols
{
    public static class MyExtensions
    {
        public static MimeKit.MimeMessage ToMimeMessage(this Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            MimeKit.MimeMessage mimeMessage = new MimeKit.MimeMessage();

            mimeMessage.From.AddRange(from address in message.From select address.ToMailboxAddress());
            mimeMessage.ReplyTo.AddRange(from address in message.ReplyTo select address.ToMailboxAddress());
            mimeMessage.To.AddRange(from address in message.To select address.ToMailboxAddress());
            mimeMessage.Cc.AddRange(from address in message.Cc select address.ToMailboxAddress());
            mimeMessage.Bcc.AddRange(from address in message.Bcc select address.ToMailboxAddress());

            mimeMessage.Subject = message.Subject;

            if (message.Protection.Type == MessageProtectionType.None)
            {
                mimeMessage.Body = message.BuildMessageBody();
            }
            else
            {
                mimeMessage.Body = message.MimeBody.ToMimeEntity();
            }

            return mimeMessage;
        }

        public static MimeKit.MailboxAddress ToMailboxAddress(this EmailAddress emailAddress)
        {
            if (emailAddress == null)
            {
                throw new ArgumentNullException(nameof(emailAddress));
            }

            return new MimeKit.MailboxAddress(emailAddress.Name ?? "", emailAddress.Address);
        }

        public static Message ToTuviMailMessage(this MimeKit.MimeMessage mimeMessage, Folder folder, CancellationToken cancellationToken = default)
        {
            if (mimeMessage == null)
            {
                throw new ArgumentNullException(nameof(mimeMessage));
            }

            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            var message = new Message();

            message.From.AddRange(from address in mimeMessage.From select address.ToEmailAddress());
            message.ReplyTo.AddRange(from address in mimeMessage.ReplyTo select address.ToEmailAddress());
            message.To.AddRange(from address in mimeMessage.To select address.ToEmailAddress());
            message.Cc.AddRange(from address in mimeMessage.Cc select address.ToEmailAddress());
            message.Bcc.AddRange(from address in mimeMessage.Bcc select address.ToEmailAddress());
            message.Date = mimeMessage.Date;

            message.Subject = mimeMessage.Subject;

            var mimeVisitor = new HtmlPreviewVisitor();
            mimeVisitor.Visit(mimeMessage);
            message.TextBody = mimeMessage.TextBody;
            message.HtmlBody = mimeVisitor.HtmlBody;
            message.Folder = folder;

            message.Protection.Type = mimeMessage.Body.GetMessageProtectionType();

            if (message.Protection.Type == MessageProtectionType.None)
            {
                message.AddAttachments(mimeMessage.Attachments, cancellationToken);
            }
            else
            {
                message.MimeBody = mimeMessage.Body.ToArray(cancellationToken);
            }

            return message;
        }

        private static MessageProtectionType GetMessageProtectionType(this MimeKit.MimeEntity mimeEntity)
        {
            if (mimeEntity is MultipartEncrypted)
            {
                return MessageProtectionType.Encryption;
            }
            else if (mimeEntity is MultipartSigned)
            {
                return MessageProtectionType.Signature;
            }
            else
            {
                return MessageProtectionType.None;
            }
        }

        public static Message ToTuviMailMessage(this MailKit.IMessageSummary messageSymmary, Folder folder)
        {
            if (messageSymmary == null)
            {
                throw new ArgumentNullException(nameof(messageSymmary));
            }

            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            var message = new Message();

            if (messageSymmary.Envelope != null)
            {
                message.From.AddRange(from address in messageSymmary.Envelope.From select address.ToEmailAddress());
                message.ReplyTo.AddRange(from address in messageSymmary.Envelope.ReplyTo select address.ToEmailAddress());
                message.To.AddRange(from address in messageSymmary.Envelope.To select address.ToEmailAddress());
                message.Cc.AddRange(from address in messageSymmary.Envelope.Cc select address.ToEmailAddress());
                message.Bcc.AddRange(from address in messageSymmary.Envelope.Bcc select address.ToEmailAddress());

                message.Subject = messageSymmary.Envelope.Subject;
            }

            message.Id = messageSymmary.UniqueId.Id;

            message.Date = messageSymmary.Date;

            message.IsMarkedAsRead = messageSymmary.Flags.Value.HasFlag(MailKit.MessageFlags.Seen);
            message.IsFlagged = messageSymmary.Flags.Value.HasFlag(MailKit.MessageFlags.Flagged);
            message.Folder = folder;

            if (messageSymmary.Attachments != null)
            {
                foreach (var attachment in messageSymmary.Attachments)
                {
                    message.Attachments.Add(new Attachment() { FileName = attachment.FileName });
                }
            }

            message.PreviewText = messageSymmary.PreviewText;

            return message;
        }

        public static EmailAddress ToEmailAddress(this MimeKit.InternetAddress internetAddress)
        {
            if (internetAddress == null)
            {
                throw new ArgumentNullException(nameof(internetAddress));
            }

            if (internetAddress is MimeKit.MailboxAddress address)
            {
                return new EmailAddress(address.Address, address.Name);
            }

            // It seems we have got a Group Address, we don't support them
            // Empty string allows us to keep current design without changes
            return new EmailAddress("", internetAddress.Name);
        }

        public static EmailAddress ToEmailAddress(this MimeKit.MailboxAddress mailboxAddress)
        {
            if (mailboxAddress == null)
            {
                throw new ArgumentNullException(nameof(mailboxAddress));
            }

            return new EmailAddress(mailboxAddress.Address, mailboxAddress.Name);
        }

        public static byte[] ToArray(this MimeKit.MimeEntity mimeBody, CancellationToken cancellationToken = default)
        {
            if (mimeBody == null)
            {
                throw new ArgumentNullException(nameof(mimeBody));
            }

            using (MemoryStream serializationStream = new MemoryStream())
            {
                mimeBody.WriteTo(serializationStream, cancellationToken);
                return serializationStream.ToArray();
            }
        }

        public static MimeKit.MimeEntity ToMimeEntity(this byte[] mimeBody, CancellationToken cancellationToken = default)
        {
            if (mimeBody == null)
            {
                throw new ArgumentNullException(nameof(mimeBody));
            }

            using (MemoryStream deserializationStream = new MemoryStream(mimeBody))
            {
                return MimeKit.MimeEntity.Load(deserializationStream, cancellationToken);
            }
        }

        public static MimeKit.MimeEntity BuildMessageBody(this Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            MimeKit.BodyBuilder bodyBuilder = new MimeKit.BodyBuilder
            {
                TextBody = message.TextBody,
                HtmlBody = message.HtmlBody
            };

            foreach (var attachment in message.Attachments)
            {
                bodyBuilder.Attachments.Add(attachment.FileName, attachment.Data);
            }

            return bodyBuilder.ToMessageBody();
        }

        public static void SetMimeBody(this Message message, MimeKit.MimeEntity mimeBody)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            message.MimeBody = mimeBody.ToArray(default);
        }

        public static void ClearUnprotectedBody(this Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            message.TextBody = null;
            message.HtmlBody = null;
            message.Attachments.Clear();
        }

        public static async Task AddContentAsync(this Message message, MimeKit.MimeEntity mimeEntity, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (mimeEntity is MimeKit.Multipart multipartBody)
            {
                await message.AddMultipartEntityAsync(multipartBody, cancellationToken).ConfigureAwait(false);
            }
            else if (mimeEntity is MimeKit.MimePart mimePart)
            {
                await message.AddMimePartAsync(mimePart, cancellationToken).ConfigureAwait(false);
            }
        }

        public static async Task AddMultipartEntityAsync(this Message message, MimeKit.Multipart multipartMime, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (multipartMime == null)
            {
                throw new ArgumentNullException(nameof(multipartMime));
            }

            if (multipartMime.ContentType.Parameters.Contains("protected-headers"))
            {
                message.AddProtectedSubject(multipartMime);
            }

            foreach (MimeKit.MimeEntity mimeEntity in multipartMime)
            {
                if (mimeEntity is MimeKit.Multipart multipartEntity)
                {
                    await message.AddMultipartEntityAsync(multipartEntity, cancellationToken).ConfigureAwait(false);
                }
                else if (mimeEntity is MimeKit.MimePart mimePart)
                {
                    await message.AddMimePartAsync(mimePart, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public static async Task AddMimePartAsync(this Message message, MimeKit.MimePart mimePart, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (mimePart == null)
            {
                throw new ArgumentNullException(nameof(mimePart));
            }

            if (mimePart.IsAttachment)
            {
                await message.AddAttachmentAsync(mimePart, cancellationToken).ConfigureAwait(false);
            }
            else if (mimePart is MimeKit.TextPart textPart)
            {
                message.AddBody(textPart);
            }
        }

        public static void AddBody(this Message message, MimeKit.TextPart textPart)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (textPart == null)
            {
                throw new ArgumentNullException(nameof(textPart));
            }

            if (textPart.IsHtml)
            {
                message.HtmlBody = textPart.Text;
            }
            else if (textPart.IsPlain)
            {
                message.TextBody = textPart.Text;
            }
        }

        public static async Task AddAttachmentAsync(this Message message, MimeKit.MimePart mimePart, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (mimePart == null)
            {
                throw new ArgumentNullException(nameof(mimePart));
            }

            var attachment = new Attachment
            {
                FileName = mimePart.FileName
            };

            using (var memory = new MemoryStream())
            {
                await mimePart.Content.DecodeToAsync(memory, cancellationToken).ConfigureAwait(false);
                attachment.Data = memory.ToArray();
            }

            message.Attachments.Add(attachment);
        }

        public static void AddAttachments(this Message message, IEnumerable<MimeKit.MimeEntity> attachments, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (attachments == null)
            {
                throw new ArgumentNullException(nameof(attachments));
            }

            foreach (var attachment in attachments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attachment is MimeKit.MimePart mimePart)
                {
                    message.AddAttachment(mimePart, cancellationToken);
                }
                else if (attachment is MimeKit.MessagePart messagePart)
                {
                    message.AddAttachment(messagePart, cancellationToken);
                }
            }
        }

        public static void AddAttachment(this Message message, MimeKit.MimePart mimePart, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (mimePart == null)
            {
                throw new ArgumentNullException(nameof(mimePart));
            }

            var attachment = new Attachment
            {
                FileName = mimePart.FileName
            };

            using (var memory = new MemoryStream())
            {
                mimePart.Content.DecodeTo(memory, cancellationToken);
                attachment.Data = memory.ToArray();
            }

            message.Attachments.Add(attachment);
        }

        public static void AddAttachment(this Message message, MimeKit.MessagePart messagePart, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (messagePart == null)
            {
                throw new ArgumentNullException(nameof(messagePart));
            }

            var attachment = new Attachment
            {
                FileName = messagePart.ContentDisposition?.FileName
            };

            using (var memory = new MemoryStream())
            {
                messagePart.Message.WriteTo(memory, cancellationToken);
                attachment.Data = memory.ToArray();
            }

            message.Attachments.Add(attachment);
        }

        public static void AddProtectedSubject(this Message message, MimeKit.MimeEntity mimeEntity)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (mimeEntity == null)
            {
                throw new ArgumentNullException(nameof(mimeEntity));
            }

            int subjectHeaderIndex = mimeEntity.Headers.IndexOf(MimeKit.HeaderId.Subject);

            if (subjectHeaderIndex != -1)
            {
                var subjectHeader = mimeEntity.Headers[subjectHeaderIndex];
                message.Subject = subjectHeader.Value;
            }
        }

        public static void AddSignatures(this Message message, DigitalSignatureCollection signatures)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            foreach (var signature in signatures)
            {
                SignatureInfo signatureInfo = new SignatureInfo();
                signatureInfo.Created = signature.CreationDate;
                signatureInfo.DigestAlgorithm = signature.DigestAlgorithm.ToString();
                try
                {
                    signatureInfo.IsVerified = signature.Verify();
                }
                catch (DigitalSignatureVerifyException)
                {
                    signatureInfo.IsVerified = false;
                }
                signatureInfo.SignerEmail = signature.SignerCertificate?.Email;
                signatureInfo.SignerName = signature.SignerCertificate?.Name;
                signatureInfo.SignerFingerprint = signature.SignerCertificate?.Fingerprint;

                message.Protection.SignaturesInfo.Add(signatureInfo);
            }
        }

        public static Folder ToTuviMailFolder(this MailKit.IMailFolder mailkitFolder)
        {
            if (mailkitFolder == null)
            {
                throw new ArgumentNullException(nameof(mailkitFolder));
            }

            return new Folder(mailkitFolder.FullName, mailkitFolder.Attributes.ToTuviMailFolderAttributes())
            {
                UnreadCount = mailkitFolder.Unread,
                TotalCount = mailkitFolder.Count
            };
        }

        public static FolderAttributes ToTuviMailFolderAttributes(this MailKit.FolderAttributes mailkitAttributes)
        {
            FolderAttributes folderAttributes = FolderAttributes.None;

            if (mailkitAttributes.HasFlag(MailKit.FolderAttributes.Inbox))
            {
                folderAttributes |= FolderAttributes.Inbox;
            }

            if (mailkitAttributes.HasFlag(MailKit.FolderAttributes.Drafts))
            {
                folderAttributes |= FolderAttributes.Draft;
            }

            if (mailkitAttributes.HasFlag(MailKit.FolderAttributes.Junk))
            {
                folderAttributes |= FolderAttributes.Junk;
            }

            if (mailkitAttributes.HasFlag(MailKit.FolderAttributes.Trash))
            {
                folderAttributes |= FolderAttributes.Trash;
            }

            if (mailkitAttributes.HasFlag(MailKit.FolderAttributes.Sent))
            {
                folderAttributes |= FolderAttributes.Sent;
            }

            if (mailkitAttributes.HasFlag(MailKit.FolderAttributes.Important))
            {
                folderAttributes |= FolderAttributes.Important;
            }

            if (mailkitAttributes.HasFlag(MailKit.FolderAttributes.All))
            {
                folderAttributes |= FolderAttributes.All;
            }
            return folderAttributes;
        }
    }
}
