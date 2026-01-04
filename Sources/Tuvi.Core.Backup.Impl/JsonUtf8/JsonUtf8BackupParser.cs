// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2026 Eppie (https://eppie.io)                                    //
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
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;
using Tuvi.Core.Entities.Exceptions;

namespace Tuvi.Core.Backup.Impl
{
    internal class JsonUtf8BackupParser : ProtectedPackageParserBase, IBackupParser
    {
        private readonly string PackageIdentifier;
        private readonly ContentSerializationFormat SupportedSerializationFormat = ContentSerializationFormat.JsonUtf8;
        protected BackupSectionsDictionary BackupSections = new BackupSectionsDictionary();

        public JsonUtf8BackupParser(string packageIdentifier, IBackupDataUnlocker dataUnlocker)
            : base(dataUnlocker)
        {
            if (packageIdentifier is null)
            {
                throw new ArgumentNullException(nameof(packageIdentifier));
            }

            PackageIdentifier = packageIdentifier;
        }

        public async Task ParseBackupAsync(Stream backupData, CancellationToken cancellationToken)
        {
            if (backupData is null)
            {
                throw new ArgumentNullException(nameof(backupData));
            }

            await base.ParsePackageAsync(backupData, cancellationToken).ConfigureAwait(false);
        }

        public Task<BackupProtocolVersion> GetVersionAsync(CancellationToken cancellationToken)
        {
            return GetObjectFromSectionAsync<BackupProtocolVersion>(BackupSectionType.Version, cancellationToken);
        }

        public Task<IList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            return GetObjectFromSectionAsync<IList<Account>>(BackupSectionType.Accounts, cancellationToken);
        }

        public Task<IReadOnlyList<EmailAccountBackupContainer>> GetMessagesAsync(CancellationToken cancellationToken = default)
        {
            return GetObjectFromSectionAsync<IReadOnlyList<EmailAccountBackupContainer>>(BackupSectionType.Messages, cancellationToken);
        }

        public Task<IList<byte[]>> GetImportedPublicKeysAsync(CancellationToken cancellationToken = default)
        {
            return GetObjectFromSectionAsync<IList<byte[]>>(BackupSectionType.PublicKeys, cancellationToken);
        }

        public Task<Settings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            return GetObjectFromSectionAsync<Settings>(BackupSectionType.Settings, cancellationToken);
        }

        protected sealed override string GetPackageIdentifier()
        {
            return PackageIdentifier;
        }

        protected sealed override async Task ParseProtectedPackageContentAsync(Stream content, CancellationToken cancellationToken)
        {
            var backupSerializationFormat = await ParseBackupContentHeaderAsync(content, cancellationToken).ConfigureAwait(false);
            if (backupSerializationFormat != SupportedSerializationFormat)
            {
                throw new BackupDeserializationException($"Unsupported backup serialization format: {backupSerializationFormat}");
            }

            try
            {
                BackupSections = await JsonSerializer
                .DeserializeAsync<BackupSectionsDictionary>(content, GetDefaultOptions(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw new BackupDeserializationException("Error in backup content deserialization from JSON.", exception);
            }
        }

        private static async Task<ContentSerializationFormat> ParseBackupContentHeaderAsync(Stream backupData, CancellationToken cancellationToken)
        {
            byte[] bytes = new byte[sizeof(ContentSerializationFormat)];

            await backupData.ReadAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);

            var serializationFormatValue = bytes.FromByteBuffer();
            if (Enum.IsDefined(typeof(ContentSerializationFormat), serializationFormatValue))
            {
                return (ContentSerializationFormat)serializationFormatValue;
            }
            else
            {
                throw new BackupDeserializationException("Unknown backup serialization format.");
            }
        }

        private async Task<TObject> GetObjectFromSectionAsync<TObject>(BackupSectionType sectionType, CancellationToken cancellationToken)
        {
            try
            {
                using (var backupSectionData = new MemoryStream(BackupSections[sectionType]))
                {
                    return await JsonSerializer.DeserializeAsync<TObject>(
                        backupSectionData,
                        GetDefaultOptions(),
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                throw new BackupDeserializationException("Error in JSON backup section data deserialization.", exception);
            }
        }

        private static JsonSerializerOptions GetDefaultOptions()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonUtf8.Converters.JsonAuthenticationDataConverter());
            options.Converters.Add(new JsonUtf8.Converters.JsonMessageConverter());
            options.Converters.Add(new JsonUtf8.Converters.JsonProtectionInfoConverter());

            return options;
        }
    }
}

