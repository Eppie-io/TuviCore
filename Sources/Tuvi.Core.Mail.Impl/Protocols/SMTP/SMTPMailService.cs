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
                using (var mimeMessage = message.ToMimeMessage())
                {
                    await SmtpClient.SendAsync(mimeMessage, cancellationToken).ConfigureAwait(false);
                    return mimeMessage.MessageId;
                }
            }
            catch (MailKit.ServiceNotConnectedException exp)
            {
                throw new MailServiceIsNotConnectedException(exp.Message, exp);
            }
            catch (MailKit.ServiceNotAuthenticatedException exp)
            {
                throw new MailServiceIsNotAuthentificatedException(exp.Message, exp);
            }
        }

        public override void Dispose()
        {
            SmtpClient?.Dispose();
        }
    }
}
