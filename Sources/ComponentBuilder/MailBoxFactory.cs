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
using Tuvi.Core;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Dec;
using Tuvi.Core.Entities;
using Tuvi.Core.Mail;
using Tuvi.Core.Utils;
using Tuvi.Proton;

[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("TuviMailLibTests")]
namespace Tuvi
{
    internal class MailBoxFactory : IMailBoxFactory
    {
        private readonly IDataStorage _dataStorage;
        private readonly ICredentialsManager _credentialsManager;
        private readonly IProtonMailBoxFactory _protonMailBoxFactory;
        private readonly IDecStorageClient _decClient;
        private readonly IPublicKeyService _publicKeyService;
        private IStorage ProtonStorage => _dataStorage as IStorage;

        public MailBoxFactory(IDataStorage dataStorage, ICredentialsManager credentialsManager, IDecStorageClient decClient, IPublicKeyService publicKeyService, IProtonMailBoxFactory protonMailBoxFactory)
        {
            _dataStorage = dataStorage ?? throw new ArgumentNullException(nameof(dataStorage));
            _credentialsManager = credentialsManager ?? throw new ArgumentNullException(nameof(credentialsManager));
            _decClient = decClient ?? throw new ArgumentNullException(nameof(decClient));
            _publicKeyService = publicKeyService ?? throw new ArgumentNullException(nameof(publicKeyService));
            _protonMailBoxFactory = protonMailBoxFactory ?? throw new ArgumentNullException(nameof(protonMailBoxFactory));
        }

        public IMailBox CreateMailBox(Account account)
        {
            var type = account.Type;
            if (type == MailBoxType.Hybrid || type == MailBoxType.Dec)
            {
                return Tuvi.Core.Dec.Impl.MailBoxCreator.Create(account, _dataStorage, _decClient, _publicKeyService);
            }
            var credentialsProvider = _credentialsManager.CreateCredentialsProvider(account);
            if (type == MailBoxType.Proton)
            {
                return _protonMailBoxFactory.CreateMailBox(account, credentialsProvider, ProtonStorage);
            }

            var outgoingCredentialsProvider = _credentialsManager.CreateOutgoingCredentialsProvider(account);
            var incomingCredentialsProvider = _credentialsManager.CreateIncomingCredentialsProvider(account);
            return Tuvi.Core.Mail.Impl.MailBoxCreator.Create(account, outgoingCredentialsProvider, incomingCredentialsProvider);
        }
    }
}
