using System.Diagnostics;
using Tuvi.Core;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Entities;
using Tuvi.Core.Mail;
using Tuvi.Proton;

[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("TuviMailLibTests")]
namespace Tuvi
{
    public enum MailBoxType : int
    {
        Email,
        Dec,
        Hybrid,
        Proton
    }
    internal class MailBoxFactory : IMailBoxFactory
    {
        private readonly IDataStorage _dataStorage;
        private IDataStorage DataStorage => _dataStorage;
        private readonly ICredentialsManager _credentialsManager;
        private ICredentialsManager CredentialsManager => _credentialsManager;
        private ISecurityManager _securityManager;
        public MailBoxFactory(IDataStorage dataStorage, ICredentialsManager credentialsManager, ISecurityManager securityManager)
        {
            Debug.Assert(dataStorage != null);
            Debug.Assert(credentialsManager != null);
            _dataStorage = dataStorage;
            _credentialsManager = credentialsManager;
            _securityManager = securityManager;
        }

        public IMailBox CreateMailBox(Account account)
        {
            var type = (MailBoxType)account.Type;
            if (type == MailBoxType.Hybrid || type == MailBoxType.Dec)
            {
                return Tuvi.Core.Dec.Impl.MailBoxCreator.Create(account, DataStorage);
            }
            var credentialsProvider = CredentialsManager.CreateCredentialsProvider(account);
            if (type == MailBoxType.Proton)
            {
                return Proton.MailBoxCreator.Create(account, credentialsProvider, DataStorage as IStorage);
            }

            var outgoingCredentialsProvider = CredentialsManager.CreateOutgoingCredentialsProvider(account);
            var incomingCredentialsProvider = CredentialsManager.CreateIncomingCredentialsProvider(account);
            return Tuvi.Core.Mail.Impl.MailBoxCreator.Create(account, outgoingCredentialsProvider, incomingCredentialsProvider);
        }
    }
}
