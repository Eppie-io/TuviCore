using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tuvi.Core.Dec
{
    public interface IDecStorageClient : IDisposable
    {
        Task<string> SendAsync(string address, byte[] data);

        Task<IEnumerable<string>> ListAsync(string address);

        Task<byte[]> GetAsync(string address, string hash);
    }
}
