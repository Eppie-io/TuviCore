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

using System.Diagnostics.Contracts;
using System.Linq;
using Tuvi.Core.Entities;

namespace Tuvi.Proton
{
    public static class Extensions
    {
        private static readonly string[] ProtonSuffixes = new[] { "@proton.me", "@protonmail.com", "@proton.local" };
        public static bool IsProton(this EmailAddress emailAddress)
        {
            Contract.Requires(emailAddress != null);
            return ProtonSuffixes.Any(x => emailAddress.Address.EndsWith(x, System.StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
