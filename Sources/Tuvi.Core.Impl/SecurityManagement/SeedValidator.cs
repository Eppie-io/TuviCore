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

using System.Globalization;
using System.Threading.Tasks;
using KeyDerivationLib;

namespace Tuvi.Core.Impl.SecurityManagement
{
    internal class SeedValidator : ISeedValidator
    {
        public bool IsWordExistInDictionary(string word)
        {
            return MasterKeyFactory.IsWordExistInDictionary(SeedNormalizer.NormalizeWord(word));
        }

        public Task<bool> IsWordExistInDictionaryAsync(string word)
        {
            return Task.Run(() => MasterKeyFactory.IsWordExistInDictionary(SeedNormalizer.NormalizeWord(word)));
        }
    }

    internal static class SeedNormalizer
    {
        public static string NormalizeWord(string str)
        {
            CultureInfo culture = new CultureInfo("en-US");
            return str?.ToLower(culture).Trim() ?? string.Empty;
        }
    }
}
