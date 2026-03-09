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
using System.Linq;
using Tuvi.Core.Entities;

namespace Tuvi.Core
{
    public class CompositeAccount : IEquatable<CompositeAccount>
    {
        private IReadOnlyList<CompositeFolder> _foldersStructure;
        public IReadOnlyList<CompositeFolder> FoldersStructure => _foldersStructure;
        public EmailAddress Email => _accounts.FirstOrDefault()?.Email;
        public string DisplayAddress => _accounts.FirstOrDefault()?.DisplayEmail.Address;

        private List<Account> _accounts;
        public IReadOnlyList<Account> Accounts => _accounts;

        private CompositeFolder _defaultInboxFolder;
        public CompositeFolder DefaultInboxFolder => _defaultInboxFolder;

        internal CompositeAccount(IReadOnlyList<CompositeFolder> folders, IEnumerable<Account> accounts, CompositeFolder inboxFolder)
        {
            _foldersStructure = folders;
            _defaultInboxFolder = inboxFolder;
            _accounts = accounts.ToList();
        }

        public bool HasAccount(Account account)
        {
            if (account is null)
            {
                return false;
            }

            return _accounts.Any(x => x?.Equals(account) == true);
        }

        public bool Equals(CompositeAccount other)
        {
            return !(other is null)
                && _accounts.Count == other._accounts.Count
                && _accounts.All(other.HasAccount);
        }

        public static bool operator ==(CompositeAccount left, CompositeAccount right)
        {
            return object.Equals(left, right);
        }

        public static bool operator !=(CompositeAccount left, CompositeAccount right)
        {
            return !object.Equals(left, right);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CompositeAccount);
        }

        public override int GetHashCode()
        {
            return _accounts.Aggregate(0, (hash, account) => hash ^ (account?.GetHashCode() ?? 0));
        }
    }
}
