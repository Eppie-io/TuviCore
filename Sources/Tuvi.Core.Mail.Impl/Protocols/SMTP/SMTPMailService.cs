using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail.Impl.Protocols.SMTP
{
    internal class SMTPMailService : SenderService
    {
        private readonly MailKit.Net.Smtp.SmtpClient SmtpClient;

        protected override MailKit.MailService Service { get => SmtpClient; }

        public SMTPMailService(string serverAddress, int serverPort)
            : base(serverAddress, serverPort)
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
            catch (MailKit.ServiceNotConnectedException exp)
            {
                throw new MailServiceIsNotConnectedException(exp.Message, exp);
            }
            catch (MailKit.ServiceNotAuthenticatedException exp)
            {
                throw new MailServiceIsNotAuthenticatedException(exp.Message, exp);
            }

            async Task<string> SendAsync()
            {
                using (var mimeMessage = message.ToMimeMessage())
                {
                    await SmtpClient.SendAsync(mimeMessage, cancellationToken).ConfigureAwait(false);
                    return mimeMessage.MessageId;
                }
            }
        }

        public override void Dispose()
        {
            SmtpClient?.Dispose();
        }
    }
}
