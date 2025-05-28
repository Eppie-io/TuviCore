using System;
using Tuvi.Core.Entities;

namespace Tuvi.Core
{
    public interface ICredentialsManager : IDisposable
    {
        ICredentialsProvider CreateCredentialsProvider(Account account);
        ICredentialsProvider CreateIncomingCredentialsProvider(Account accountData);
        ICredentialsProvider CreateOutgoingCredentialsProvider(Account accountData);
    }
}
