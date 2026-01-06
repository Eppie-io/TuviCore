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
