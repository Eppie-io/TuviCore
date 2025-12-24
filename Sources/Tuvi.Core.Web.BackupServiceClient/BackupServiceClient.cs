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

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Tuvi.Core.Web.BackupService;

namespace BackupServiceClientLibrary
{
    /// <summary>
    /// The class contains helper functions for sending and receiving backups.
    /// </summary>
    public static class BackupServiceClient
    {
        /// <summary>
        /// Sends data to the cloud.
        /// </summary>
        /// <param name="actionUrl">A reference to the Web Function responsible for storing data in the cloud.</param>
        /// <param name="name">The name of the object to save the data to.</param>
        /// <param name="publickeyStream">The publickey data stream.</param>
        /// <param name="signatureStream">The signature data stream.</param>
        /// <param name="backupStream">Backup data stream.</param>
        /// <returns>Returns true on success.</returns>
        public static async Task<bool> UploadAsync(string actionUrl, string name, Stream publickeyStream, Stream signatureStream, Stream backupStream)
        {
            if (string.IsNullOrWhiteSpace(actionUrl))
            {
                throw new ArgumentException($"{nameof(actionUrl)} can't be empty or contain only a space.", nameof(actionUrl));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"{nameof(name)} can't be empty or contain only a space.", nameof(name));
            }

            if (publickeyStream is null)
            {
                throw new System.ArgumentNullException(nameof(publickeyStream));
            }

            if (signatureStream is null)
            {
                throw new System.ArgumentNullException(nameof(signatureStream));
            }

            if (backupStream is null)
            {
                throw new System.ArgumentNullException(nameof(backupStream));
            }

            publickeyStream.Position = 0;
            signatureStream.Position = 0;
            backupStream.Position = 0;

            using (var filePublickeyStreamContent = new StreamContent(publickeyStream))
            using (var fileSignatureStreamContent = new StreamContent(signatureStream))
            using (var fileBackupStreamContent = new StreamContent(backupStream))
            using (var formData = new MultipartFormDataContent())
            using (var client = new HttpClient())
            {
                var publickeyName = name + DataIdentificators.PublicKeyExtension;
                var signatureName = name + DataIdentificators.SignatureExtension;
                var backupName = name + DataIdentificators.BackupExtension;

                formData.Add(filePublickeyStreamContent, publickeyName, publickeyName);
                formData.Add(fileSignatureStreamContent, signatureName, signatureName);
                formData.Add(fileBackupStreamContent, backupName, backupName);
                var response = await client.PostAsync(actionUrl, formData).ConfigureAwait(false);

                return response.IsSuccessStatusCode;
            }
        }

        /// <summary>
        /// Sends data to the cloud.
        /// </summary>
        /// <param name="actionUrl">A reference to the Web Function responsible for storing data in the cloud.</param>
        /// <param name="name">The name of the object to save the data to.</param>
        /// <param name="publickeyStream">The publickey data stream.</param>
        /// <param name="signatureStream">The signature data stream.</param>
        /// <param name="backupStream">Backup data stream.</param>
        /// <returns>Returns true on success.</returns>
        public static Task<bool> UploadAsync(Uri actionUrl, string name, Stream publickeyStream, Stream signatureStream, Stream backupStream)
        {
            if (actionUrl is null)
            {
                throw new ArgumentNullException(nameof(actionUrl));
            }

            return UploadAsync(actionUrl.AbsoluteUri, name, publickeyStream, signatureStream, backupStream);
        }

        /// <summary>
        /// Retrieves data from the cloud.
        /// </summary>
        /// <param name="actionUrl">A reference to the Web Function responsible for retrieving data from the cloud.</param>
        /// <param name="name">The name of the object to retrieve the data.</param>
        /// <returns>Returns a stream of data, or null if the download failed.</returns>
        public static async Task<Stream> DownloadAsync(string actionUrl, string name)
        {
            if (string.IsNullOrWhiteSpace(actionUrl))
            {
                throw new ArgumentException($"{nameof(actionUrl)} can't be empty or contain only a space.", nameof(actionUrl));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"{nameof(name)} can't be empty or contain only a space.", nameof(name));
            }

            using (var client = new HttpClient())
            using (var response = await client.GetAsync(actionUrl + HttpUtility.UrlEncode(name)).ConfigureAwait(false))
            {
                var statusCode = response.StatusCode;

                if (statusCode == System.Net.HttpStatusCode.OK)
                {
                    var stream = new MemoryStream();
                    await response.Content.CopyToAsync(stream).ConfigureAwait(false);

                    stream.Position = 0;

                    return stream;
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves data from the cloud.
        /// </summary>
        /// <param name="actionUrl">A reference to the Web Function responsible for retrieving data from the cloud.</param>
        /// <param name="name">The name of the object to retrieve the data.</param>
        /// <returns>Returns a stream of data, or null if the download failed.</returns>
        public static Task<Stream> DownloadAsync(Uri actionUrl, string name)
        {
            if (actionUrl is null)
            {
                throw new ArgumentNullException(nameof(actionUrl));
            }

            return DownloadAsync(actionUrl.AbsoluteUri, name);
        }
    }
}
