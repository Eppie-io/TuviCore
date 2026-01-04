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

using System.Collections.Generic;
using System.Linq;
using Tuvi.Core.Entities;

namespace Tuvi.Core
{
    public class CompositeAccount
    {
        private IReadOnlyList<CompositeFolder> _foldersStructure;
        public IReadOnlyList<CompositeFolder> FoldersStructure => _foldersStructure;
        public EmailAddress Email => _addresses.First();
        private List<EmailAddress> _addresses;
        public IReadOnlyList<EmailAddress> Addresses => _addresses;

        private CompositeFolder _defaultInboxFolder;
        public CompositeFolder DefaultInboxFolder => _defaultInboxFolder;

        internal CompositeAccount(IReadOnlyList<CompositeFolder> folders, IEnumerable<EmailAddress> emailAddresses, CompositeFolder inboxFolder)
        {
            _foldersStructure = folders;
            _defaultInboxFolder = inboxFolder;
            _addresses = new List<EmailAddress>(emailAddresses);
        }
    }
}
