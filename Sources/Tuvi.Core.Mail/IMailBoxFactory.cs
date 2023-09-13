using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail
{
    public interface IMailBoxFactory
    {
        /// <summary>
        /// Create mailbox corresponding to <paramref name="account"/>
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        IMailBox CreateMailBox(Account account);
    }
}
