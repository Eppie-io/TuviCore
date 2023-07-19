using Tuvi.Core.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core
{
    public interface ICredentialsManager
    {
        Task<ICredentialsProvider> CreateCredentialsProviderAsync(Account account, CancellationToken cancellationToken = default);
    }
}
