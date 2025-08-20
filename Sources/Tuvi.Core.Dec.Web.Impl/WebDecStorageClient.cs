using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Tuvi.Core.Dec.Web.Impl
{
    internal class WebDecStorageClient : IDecStorageClient
    {
        private string Url;
        private HttpClient _httpClient;
        private bool _disposedValue;

        private HttpClient Client
        {
            get
            {
                if (_httpClient == null)
                {
                    _httpClient = new HttpClient();
                }
                return _httpClient;
            }
        }

        public WebDecStorageClient(string url)
        {
            Url = url;
        }

        public async Task<string> SendAsync(string address, string hash)
        {
            return await Client.GetStringAsync($"{Url}/send?address={address}&hash={hash}&code=testnet").ConfigureAwait(false);
        }

        public async Task<IEnumerable<string>> ListAsync(string address)
        {
            var list = await Client.GetStringAsync($"{Url}/list?address={address}&code=testnet").ConfigureAwait(false);
            return JsonSerializer.Deserialize<IEnumerable<string>>(list);
        }

        public async Task<byte[]> GetAsync(string hash)
        {
            return await Client.GetByteArrayAsync($"{Url}/get?hash={hash}&code=testnet").ConfigureAwait(false);
        }

        public async Task<string> PutAsync(byte[] data)
        {
            using (var dataStream = new MemoryStream(data))
            using (var dataContent = new StreamContent(dataStream))
            using (var formData = new MultipartFormDataContent())
            {
                formData.Add(dataContent, "data", "data");

                var response = await Client.PostAsync($"{Url}/put?code=testnet", formData).ConfigureAwait(false);

                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue)
            {
                return;
            }

            if (disposing)
            {
                _httpClient?.Dispose();
            }

            _disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            System.GC.SuppressFinalize(this);
        }
    }
}
