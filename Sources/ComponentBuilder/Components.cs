using Microsoft.Extensions.Logging;
using MimeKit;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using Tuvi.Core;
using Tuvi.Core.Backup;
using Tuvi.Core.Backup.Impl;
using Tuvi.Core.Backup.Impl.JsonUtf8;
using Tuvi.Core.DataStorage;
using Tuvi.Core.DataStorage.Impl;
using Tuvi.Core.Impl;
using Tuvi.Core.Impl.BackupManagement;
using Tuvi.Core.Impl.CredentialsManagement;
using Tuvi.Core.Impl.SecurityManagement;
using Tuvi.Core.Mail;
using Tuvi.Core.Mail.Impl;
using Tuvi.Core.Mail.Impl.Protocols;
using Tuvi.Core.Utils;
using TuviPgpLib;
using TuviPgpLibImpl;

namespace ComponentBuilder
{
    internal class MyKeyMatcher : IKeyMatcher
    {
        public bool IsMatch(PgpPublicKey key, MailboxAddress mailbox)
        {
            var encodedPublicKey = PublicKeyConverter.ConvertPublicKeyToEmailName(key.GetKey() as ECPublicKeyParameters);
            return string.Equals(mailbox.ToEmailAddress().DecentralizedAddress, encodedPublicKey, StringComparison.OrdinalIgnoreCase);
        }
    }
    public static class Components
    {
        public static ITuviMail CreateTuviMailCore(string filePath, ImplementationDetailsProvider implementationDetailsProvider, ITokenRefresher tokenRefresher, ILoggerFactory loggerFactory = null)
        {
            if (loggerFactory != null)
            {
                Tuvi.Core.Logging.LoggingExtension.LoggerFactory = loggerFactory;
            }

            var dataStorage = GetDataStorage(filePath);
            var securityManager = GetSecurityManager(dataStorage);
            var backupProtector = securityManager.GetBackupProtector();
            var backupManager = GetBackupManager(dataStorage, backupProtector, new JsonUtf8SerializationFactory(backupProtector), securityManager);
            var credentialsManager = GetCredentialsManager(dataStorage, tokenRefresher);
            var mailBoxFactory = GetMailBoxFactory(dataStorage, credentialsManager, securityManager);
            var mailServerTester = GetMailServerTester();

            return TuviCoreCreator.CreateTuviMailCore(mailBoxFactory, mailServerTester, dataStorage, securityManager, backupManager, credentialsManager, implementationDetailsProvider);
        }

        private static ISecurityManager GetSecurityManager(IDataStorage dataStorage)
        {
            var pgpContext = GetPgpContext(dataStorage, new MyKeyMatcher());
            var messageProtector = GetMessageProtector(pgpContext);
            var backupProtector = GetBackupProtector(pgpContext);

            return SecurityManagerCreator.GetSecurityManager(
                dataStorage,
                pgpContext,
                messageProtector,
                backupProtector);
        }

        private static IMailBoxFactory GetMailBoxFactory(IDataStorage dataStorage, ICredentialsManager credentialsManager, ISecurityManager securityManager)
        {
            return new Tuvi.MailBoxFactory(dataStorage, credentialsManager, securityManager);
        }

        private static IMailServerTester GetMailServerTester()
        {
            return MailServerTesterCreator.CreateMailServerTester();
        }

        private static IDataStorage GetDataStorage(string dataStorageFilePath)
        {
            return DataStorageProvider.GetDataStorage(dataStorageFilePath);
        }

        private static ITuviPgpContext GetPgpContext(IKeyStorage keyStorage, IKeyMatcher keyMatcher)
        {
            return TuviPgpLibImpl.TuviPgpContextCreator.GetPgpContext(keyStorage, keyMatcher);
        }

        private static IMessageProtector GetMessageProtector(ITuviPgpContext pgpContext)
        {
            return MessageProtectorCreator.GetMessageProtector(pgpContext);
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
