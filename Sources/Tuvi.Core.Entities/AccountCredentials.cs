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
using System.Threading;
using System.Threading.Tasks;

namespace Tuvi.Core.Entities
{
    public class AccountCredentials
    {
        public string UserName { get; set; }
    }

    public class BasicCredentials : AccountCredentials
    {
        public string Password { get; set; }

        public static BasicCredentials Create(Account account)
        {
            if (account?.AuthData is BasicCredentials basicCredentials)
            {
                return new BasicCredentials()
                {
                    UserName = account.Email.Address,
                    Password = basicCredentials.Password
                };
            }

            return null;
        }
    }

    public class OAuth2Credentials : AccountCredentials
    {
        public string AccessToken { get; set; }
    }

    public class ProtonCredentials : AccountCredentials
    {
        public string UserId { get; set; }
        public string RefreshToken { get; set; }
        public string SaltedPassword { get; set; }
    }

    public interface ICredentialsProvider
    {
        Task<AccountCredentials> GetCredentialsAsync(HashSet<string> supportedAuthMechanisms, CancellationToken cancellationToken = default);
    }
}
