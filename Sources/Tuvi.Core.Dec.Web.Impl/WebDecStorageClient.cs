// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2025 Eppie (https://eppie.io)                                    //
//                                                                              //
//   Licensed under the Apache License, Version 2.0 (the "License"),            //
//   you may not use this file except in compliance with the License.           //
//   You may obtain a copy of the License at                                    //
//                                                                              //
//       http://www.apache.org/licenses/LICENSE-2.0                             //
//                                                                              //
//   Unless required by applicable law or agreed to in writing, software        //
//   distributed under the License is distributed on an "AS IS" BASIS,          //
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.   //
//   See the License for the specific language governing permissions and        //
//   limitations under the License.                                             //
//                                                                              //
// ---------------------------------------------------------------------------- //

using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Tuvi.Dec.Web.Impl.Tests")]

namespace Tuvi.Core.Dec.Web.Impl
{
    internal class WebDecStorageClient : IDecStorageClient
    {
        private readonly string Url;
        private readonly HttpClient _httpClient;
        private bool _disposedValue;

        public WebDecStorageClient(string url)
        {
            Url = url;
            _httpClient = new HttpClient();
        }

        public WebDecStorageClient(string url, HttpMessageHandler handler)
        {
            Url = url;
            _httpClient = new HttpClient(handler, disposeHandler: true);
        }

        private static string Escape(string value) => System.Uri.EscapeDataString(value ?? string.Empty);

        public async Task<string> SendAsync(string address, string hash, CancellationToken cancellationToken)
        {
            var uri = $"{Url}/send?address={Escape(address)}&hash={Escape(hash)}&code=testnet";
            using (var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false))
            {
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        public async Task<IEnumerable<string>> ListAsync(string address, CancellationToken cancellationToken)
        {
            var uri = $"{Url}/list?address={Escape(address)}&code=testnet";
            using (var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false))
            {
                var list = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonSerializer.Deserialize<IEnumerable<string>>(list);
            }
        }

        public async Task<byte[]> GetAsync(string hash, CancellationToken cancellationToken)
        {
            var uri = $"{Url}/get?hash={Escape(hash)}&code=testnet";
            using (var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false))
            {
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        public async Task<string> PutAsync(byte[] data, CancellationToken cancellationToken)
        {
            using (var dataContent = new ByteArrayContent(data))
            using (var formData = new MultipartFormDataContent())
            {
                formData.Add(dataContent, "data", "data");
                var response = await _httpClient.PostAsync($"{Url}/put?code=testnet", formData, cancellationToken).ConfigureAwait(false);
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        public async Task<string> ClaimNameAsync(string name, string address, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new System.ArgumentException("Name is empty.", nameof(name));
            }

            if (string.IsNullOrEmpty(address))
            {
                throw new System.ArgumentException("Address is empty.", nameof(address));
            }

            var url = $"{Url}/claim?name={System.Uri.EscapeDataString(name)}&address={System.Uri.EscapeDataString(address)}&code=testnet";
            return await GetStringWithCancellationAsync(url, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> GetAddressByNameAsync(string name, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new System.ArgumentException("Name is empty.", nameof(name));
            }

            var url = $"{Url}/address?name={System.Uri.EscapeDataString(name)}&code=testnet";
            return await GetStringWithCancellationAsync(url, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> GetStringWithCancellationAsync(string url, CancellationToken cancellationToken)
        {
            using (var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false))
            {
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
