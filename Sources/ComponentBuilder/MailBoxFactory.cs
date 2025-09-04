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
        private IDataStorage DataStorage => _dataStorage;

        private readonly ICredentialsManager _credentialsManager;
        private ICredentialsManager CredentialsManager => _credentialsManager;

        private readonly IDecStorageClient _decClient;
        private readonly IPublicKeyService _publicKeyService;

        public MailBoxFactory(IDataStorage dataStorage, ICredentialsManager credentialsManager, IDecStorageClient decClient, IPublicKeyService publicKeyService)
        {
            _dataStorage = dataStorage ?? throw new ArgumentNullException(nameof(dataStorage));
            _credentialsManager = credentialsManager ?? throw new ArgumentNullException(nameof(credentialsManager));
            _decClient = decClient ?? throw new ArgumentNullException(nameof(decClient));
            _publicKeyService = publicKeyService ?? throw new ArgumentNullException(nameof(publicKeyService));
        }

        public IMailBox CreateMailBox(Account account)
        {
            var type = account.Type;
            if (type == MailBoxType.Hybrid || type == MailBoxType.Dec)
            {
                return Tuvi.Core.Dec.Impl.MailBoxCreator.Create(account, DataStorage, _decClient, _publicKeyService);
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
