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
using KeyDerivation;

namespace Tuvi.Core
{
    public class ImplementationDetailsProvider : IBackupDetailsProvider, IKeyDerivationDetailsProvider
    {
        private string _keyDerivationSalt;
        private string _backupPackageIdentifier;

        private Dictionary<SpecialPgpKeyType, string> _specialPgpKeyIdentities;

        public ImplementationDetailsProvider(string keyDerivationSalt, string backupPackageIdentifier, string backupPgpKeyIdentity)
        {
            _keyDerivationSalt = keyDerivationSalt;
            _backupPackageIdentifier = backupPackageIdentifier;

            _specialPgpKeyIdentities = new Dictionary<SpecialPgpKeyType, string>
                                      {
                                          { SpecialPgpKeyType.Backup, backupPgpKeyIdentity }
                                      };
        }

        public string GetPackageIdentifier()
        {
            return _backupPackageIdentifier;
        }

        public string GetSaltPhrase()
        {
            return _keyDerivationSalt;
        }

        public int GetSeedPhraseLength()
        {
            return 12;
        }

        public Dictionary<SpecialPgpKeyType, string> GetSpecialPgpKeyIdentities()
        {
            return _specialPgpKeyIdentities;
        }
    }
}
