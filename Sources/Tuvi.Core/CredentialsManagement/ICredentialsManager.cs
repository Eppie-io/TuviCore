using System;
using Tuvi.Core.Entities;

namespace Tuvi.Core
{
    public interface ICredentialsManager : IDisposable
    {
        ICredentialsProvider CreateCredentialsProvider(Account account);
    }
}
