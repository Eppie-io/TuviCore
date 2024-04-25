using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core
{
    public interface ITokenResolver
    {
        void AddOrUpdateToken(EmailAddress emailAddress, string emailService, string refreshToken);
        Task<(string accessToken, string refreshToken)> GetAccessTokenAsync(EmailAddress emailAddress, CancellationToken cancellationToken = default);
    }
}
