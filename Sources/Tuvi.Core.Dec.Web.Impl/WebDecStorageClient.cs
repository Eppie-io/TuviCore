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

        public async Task<string> SendAsync(string address, byte[] data)
        {
            using (var dataStream = new MemoryStream(data))
            using (var dataContent = new StreamContent(dataStream))
            using (var formData = new MultipartFormDataContent())
            {
                formData.Add(dataContent, address, address);

                var response = await Client.PostAsync($"{Url}/send?folder={address}&code=85sEA3CukAJ8CUg0fbMgH3KoNB-gWhj9iDnMvDvTB384AzFuIuPw6w==", formData).ConfigureAwait(false);

                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        public async Task<IEnumerable<string>> ListAsync(string address)
        {
            var list = await Client.GetStringAsync($"{Url}/list?folder={address}&code=x3XfWh-wwuxHxvoA1S0bAwo6Zocq4DrccaiECMW-nZwGAzFu0lhI6Q==").ConfigureAwait(false);
            return JsonSerializer.Deserialize<IEnumerable<string>>(list);
        }

        public async Task<byte[]> GetAsync(string address, string hash)
        {
            return await Client.GetByteArrayAsync($"{Url}/get?folder={address}&hash={hash}&code=nyzDT27rYdP4x2Dx97CoPzBbfBsRQ8cti3oJ092R2BCGAzFuzMm1dg==").ConfigureAwait(false);
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
