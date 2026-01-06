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

using Microsoft.Extensions.Logging;
using Tuvi.Core;
using Tuvi.Core.Backup;
using Tuvi.Core.Backup.Impl;
using Tuvi.Core.Backup.Impl.JsonUtf8;
using Tuvi.Core.DataStorage;
using Tuvi.Core.DataStorage.Impl;
using Tuvi.Core.Dec;
using Tuvi.Core.Impl;
using Tuvi.Core.Impl.BackupManagement;
using Tuvi.Core.Impl.CredentialsManagement;
using Tuvi.Core.Impl.SecurityManagement;
using Tuvi.Core.Mail;
using Tuvi.Core.Mail.Impl;
using Tuvi.Core.Utils;
using TuviPgpLib;

namespace ComponentBuilder
{
    public static class Components
    {
        public static ITuviMail CreateTuviMailCore(string filePath, ImplementationDetailsProvider implementationDetailsProvider, ITokenRefresher tokenRefresher, ILoggerFactory loggerFactory = null)
        {
            if (loggerFactory != null)
            {
                Tuvi.Core.Logging.LoggingExtension.LoggerFactory = loggerFactory;
            }

            var dataStorage = GetDataStorage(filePath);
            var decClient = GetDecClient();
            var publicKeyService = GetPublicKeyService(decClient);
            var securityManager = GetSecurityManager(dataStorage, decClient, publicKeyService);
            var backupProtector = securityManager.GetBackupProtector();
            var backupManager = GetBackupManager(dataStorage, backupProtector, new JsonUtf8SerializationFactory(backupProtector), securityManager);
            var credentialsManager = GetCredentialsManager(dataStorage, tokenRefresher);
            var mailBoxFactory = GetMailBoxFactory(dataStorage, credentialsManager, decClient, publicKeyService);
            var mailServerTester = GetMailServerTester();

            return TuviCoreCreator.CreateTuviMailCore(mailBoxFactory, mailServerTester, dataStorage, securityManager, backupManager, credentialsManager, implementationDetailsProvider, decClient);
        }

        private static IDecStorageClient GetDecClient()
        {
            //return DecStorageBuilder.CreateWebClient(new System.Uri("http://localhost:7071/api"));
            return DecStorageBuilder.CreateWebClient(new System.Uri("https://testnet2.eppie.io/api"));
        }

        private static ISecurityManager GetSecurityManager(IDataStorage dataStorage, IDecStorageClient decClient, IPublicKeyService publicKeyService)
        {
            var pgpContext = GetPgpContext(dataStorage);
            var messageProtector = GetMessageProtector(pgpContext, publicKeyService);
            var backupProtector = GetBackupProtector(pgpContext);

            return SecurityManagerCreator.GetSecurityManager(
                dataStorage,
                pgpContext,
                messageProtector,
                backupProtector,
                publicKeyService);
        }

        // TODO: Move to settings
        private const string _etherscanApiKey = "";
        private static PublicKeyService GetPublicKeyService(IDecStorageClient decClient)
        {
            return PublicKeyService.CreateDefault(new Tuvi.Core.Dec.Impl.DecClientNameResolver(decClient), _etherscanApiKey, SharedHttpClient.Instance);
        }

        private static class SharedHttpClient
        {
            internal static readonly System.Net.Http.HttpClient Instance = new System.Net.Http.HttpClient();
        }

        private static IMailBoxFactory GetMailBoxFactory(IDataStorage dataStorage, ICredentialsManager credentialsManager, IDecStorageClient decClient, IPublicKeyService publicKeyService)
        {
            return new Tuvi.MailBoxFactory(dataStorage, credentialsManager, decClient, publicKeyService);
        }

        private static IMailServerTester GetMailServerTester()
        {
            return MailServerTesterCreator.CreateMailServerTester();
        }

        private static IDataStorage GetDataStorage(string dataStorageFilePath)
        {
            return DataStorageProvider.GetDataStorage(dataStorageFilePath);
        }

        private static ITuviPgpContext GetPgpContext(IKeyStorage keyStorage)
        {
            return TuviPgpLibImpl.TuviPgpContextCreator.GetPgpContext(keyStorage);
        }

        private static IMessageProtector GetMessageProtector(ITuviPgpContext pgpContext, IPublicKeyService publicKeyService)
        {
            return MessageProtectorCreator.GetMessageProtector(pgpContext, publicKeyService);
        }

        private static IBackupProtector GetBackupProtector(ITuviPgpContext pgpContext)
        {
            return BackupProtectorCreator.CreateBackupProtector(pgpContext);
        }

        private static IBackupManager GetBackupManager(IDataStorage storage, IBackupProtector backupProtector, IBackupSerializationFactory backupFactory, ISecurityManager security)
        {
            return BackupManagerCreator.GetBackupManager(storage, backupProtector, backupFactory, security);
        }

        private static ICredentialsManager GetCredentialsManager(IDataStorage storage, ITokenRefresher tokenRefresher)
        {
            return CredentialsManagerCreator.GetCredentialsProvider(storage, tokenRefresher);
        }
    }
}
