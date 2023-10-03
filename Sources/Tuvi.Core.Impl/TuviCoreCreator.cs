using Tuvi.Core.DataStorage;
using Tuvi.Core.Mail;

namespace Tuvi.Core.Impl
{
    public static class TuviCoreCreator
    {
        public static ITuviMail CreateTuviMailCore(
            IMailBoxFactory mailBoxFactory,
            IMailServerTester mailServerTester,
            IDataStorage dataStorage,
            ISecurityManager securityManager,
            IBackupManager backupManager,
            ICredentialsManager credentialsManager,
            ImplementationDetailsProvider implementationDetailsProvider)
        {
            return new TuviMail(mailBoxFactory, mailServerTester, dataStorage, securityManager, backupManager, credentialsManager, implementationDetailsProvider);
        }
    }
}
