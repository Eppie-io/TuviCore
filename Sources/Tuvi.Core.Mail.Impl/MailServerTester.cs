using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail.Impl
{
    public static class MailServerTesterCreator
    {
        public static IMailServerTester CreateMailServerTester()
        {
            return new MyMailServerTester();
        }
    }

    internal class MyMailServerTester : IMailServerTester
    {
        public Task TestAsync(string host, int port, MailProtocol protocol, ICredentialsProvider credentialsProvider, CancellationToken cancellationToken = default)
        {
            return MailServerTester.TestAsync(host, port, protocol, credentialsProvider, cancellationToken);
        }
    }

    internal static class MailServerTester
    {
        public static async Task TestAsync(string host,
                                           int port,
                                           MailProtocol protocol,
                                           ICredentialsProvider credentialsProvider,
                                           CancellationToken cancellationToken = default)
        {
            var service = GetService(host, port, protocol);
            await TestServiceAsync(service, credentialsProvider, cancellationToken).ConfigureAwait(false);
        }

        private static Protocols.MailService GetService(string host, int port, MailProtocol protocol)
        {
            switch (protocol)
            {
                case MailProtocol.IMAP:
                    {
                        return new Protocols.IMAP.IMAPMailService(host, port);
                    }
                case MailProtocol.SMTP:
                    {
                        return new Protocols.SMTP.SMTPMailService(host, port);
                    }
                default:
                    {
                        throw new ProtocolIsNotSupportedException(protocol);
                    }
            }
        }

        private static async Task TestServiceAsync(Protocols.MailService service, ICredentialsProvider credentialsProvider, CancellationToken cancellationToken)
        {
            await service.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await service.AuthenticateAsync(credentialsProvider, cancellationToken).ConfigureAwait(false);
        }
    }
}
