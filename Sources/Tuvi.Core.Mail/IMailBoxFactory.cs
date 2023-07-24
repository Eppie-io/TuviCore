using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail
{
    public interface IMailBoxFactory
    {
        /// <summary>
        /// Create mailbox corresponding to <paramref name="account"/>
        /// </summary>
        /// <param name="account"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IMailBox> CreateMailBoxAsync(Account account, CancellationToken cancellationToken = default);
    }
}
