using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Dec
{
    public interface IDecProtector
    {
        Task<byte[]> EncryptAsync(string address, string data, CancellationToken cancellationToken);

        Task<string> DecryptAsync(Account account, byte[] data, CancellationToken cancellationToken);
    }
}
