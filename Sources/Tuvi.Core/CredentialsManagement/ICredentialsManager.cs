using Tuvi.Core.Entities;

namespace Tuvi.Core
{
    public interface ICredentialsManager
    {
        ICredentialsProvider CreateCredentialsProvider(Account account);
    }
}
