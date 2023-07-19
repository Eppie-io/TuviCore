using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail
{
    public interface IMailServerTester
    {
        /// <summary>
        /// Try to access mail server with specified parameters.
        /// </summary>
        Task TestAsync(string host, int port, MailProtocol protocol, ICredentialsProvider credentialsProvider, CancellationToken cancellationToken = default);
    }
}
