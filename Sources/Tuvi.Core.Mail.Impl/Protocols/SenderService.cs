using System;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail.Impl.Protocols
{
    internal abstract class SenderService : MailService, IDisposable
    {
        protected SenderService(string serverAddress, int serverPort, ICredentialsProvider credentialsProvider)
            : base(serverAddress, serverPort, credentialsProvider)
        {
        }

        public abstract Task<string> SendMessageAsync(Message message, CancellationToken cancellationToken);

        public abstract void Dispose();
    }
}
