using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail.Impl.Protocols.SMTP
{
    internal class SMTPMailService : SenderService
    {
        private readonly MailKit.Net.Smtp.SmtpClient SmtpClient;

        protected override MailKit.MailService Service { get => SmtpClient; }

        public SMTPMailService(string serverAddress, int serverPort, ICredentialsProvider credentialsProvider)
            : base(serverAddress, serverPort, credentialsProvider)
        {
            SmtpClient = new MailKit.Net.Smtp.SmtpClient();
        }

        public override async Task<string> SendMessageAsync(Message message, CancellationToken cancellationToken)
        {
            try
            {
                return await SendAsync().ConfigureAwait(false);
            }
            catch (System.IO.IOException)
            {
                // after exception connection may be lost
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);

                // retry once
                return await SendAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Smtp.SmtpProtocolException)
            {
                // after exception connection may be lost
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);

                // retry once
                return await SendAsync().ConfigureAwait(false);
            }
            catch (MailKit.Net.Smtp.SmtpCommandException)
            {
                // after exception connection may be lost
                await RestoreConnectionAsync(cancellationToken).ConfigureAwait(false);

                // retry once
                return await SendAsync().ConfigureAwait(false);
            }

            async Task<string> SendAsync()
            {
                using (var mimeMessage = message.ToMimeMessage())
                {
                    RemoveDecentralizedEmails(message, mimeMessage);

                    await SmtpClient.SendAsync(mimeMessage, cancellationToken).ConfigureAwait(false);
                    return mimeMessage.MessageId;
                }
            }
        }

        private static void RemoveDecentralizedEmails(Message message, MimeKit.MimeMessage mimeMessage)
        {
            mimeMessage.From.Clear();
            mimeMessage.To.Clear();
            mimeMessage.Cc.Clear();
            mimeMessage.Bcc.Clear();

            mimeMessage.From.AddRange(from address in message.From select address.OriginalAddress.ToMailboxAddress());
            mimeMessage.To.AddRange(from address in message.To where !address.IsDecentralized select address.ToMailboxAddress());
            mimeMessage.Cc.AddRange(from address in message.Cc where !address.IsDecentralized select address.ToMailboxAddress());
            mimeMessage.Bcc.AddRange(from address in message.Bcc where !address.IsDecentralized select address.ToMailboxAddress());
        }

        public override void Dispose()
        {
            SmtpClient?.Dispose();
        }
    }
}
