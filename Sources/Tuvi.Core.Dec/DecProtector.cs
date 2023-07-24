using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core.Dec
{
    public interface IDecProtector
    {
        Task<byte[]> EncryptAsync(string address, string data, CancellationToken cancellationToken);

        Task<string> DecryptAsync(string identity, string tag, byte[] data, CancellationToken cancellationToken);
    }
}
