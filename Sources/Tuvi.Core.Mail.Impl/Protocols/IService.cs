using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;


namespace Tuvi.Core.Mail.Impl.Protocols
{
    interface IService
    {
        Task ConnectAsync(CancellationToken cancellationToken);
        Task AuthenticateAsync(System.Net.NetworkCredential credential, CancellationToken cancellationToken);
        Task AuthenticateAsync(ICredentialsProvider credentialsProvider, CancellationToken cancellationToken);
        Task DisconnectAsync();

        bool IsConnected { get; }
        bool IsAuthentificated { get; }
    }
}
