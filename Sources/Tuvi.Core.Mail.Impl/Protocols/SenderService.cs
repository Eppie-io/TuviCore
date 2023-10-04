using Tuvi.Core.Entities;
using MimeKit.Cryptography;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core.Mail.Impl.Protocols
{
    internal abstract class SenderService : MailService, IDisposable
    {
        protected SenderService(string serverAddress, int serverPort)
            : base(serverAddress, serverPort)
        {
        }

        public abstract Task<string> SendMessageAsync(Message message, CancellationToken cancellationToken);

        public abstract void Dispose();
    }
}
