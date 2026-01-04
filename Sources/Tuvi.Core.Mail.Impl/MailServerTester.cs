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
            var service = GetService(host, port, protocol, credentialsProvider);
            await TestServiceAsync(service, cancellationToken).ConfigureAwait(false);
        }

        private static Protocols.MailService GetService(string host, int port, MailProtocol protocol, ICredentialsProvider credentialsProvider)
        {
            switch (protocol)
            {
                case MailProtocol.IMAP:
                    {
                        return new Protocols.IMAP.IMAPMailService(host, port, credentialsProvider);
                    }
                case MailProtocol.SMTP:
                    {
                        return new Protocols.SMTP.SMTPMailService(host, port, credentialsProvider);
                    }
                default:
                    {
                        throw new ProtocolIsNotSupportedException(protocol);
                    }
            }
        }

        private static async Task TestServiceAsync(Protocols.MailService service, CancellationToken cancellationToken)
        {
            await service.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await service.AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
