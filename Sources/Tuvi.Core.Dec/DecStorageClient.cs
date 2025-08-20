using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tuvi.Core.Dec
{
    public interface IDecStorageClient : IDisposable
    {
        Task<string> SendAsync(string address, string hash);

        Task<IEnumerable<string>> ListAsync(string address);

        Task<byte[]> GetAsync(string hash);

        Task<string> PutAsync(byte[] data);
    }
}
