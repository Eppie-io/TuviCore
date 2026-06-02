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
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Proton
{
    public delegate Task<(bool completed, string code)> TwoFactorCodeProvider(Exception previousAttemptException, CancellationToken cancellationToken);
    public delegate Task<(bool completed, string password)> MailboxPasswordProvider(Exception previousAttemptException, CancellationToken cancellationToken);
    public delegate Task<(bool completed, string verificationType, string token)> HumanVerifier(Uri verifierUrl, Exception previousAttemptException, CancellationToken cancellationToken);

    public class ProtonConfiguration
    {
        public Uri RedirectUri { get; set; }
        public string AppVersion { get; set; }
        public string UserAgent { get; set; }
        public string ClientSecret { get; set; }
    }

    public interface IProtonLoginHelper
    {
        Task<ProtonCredentials> LoginAsync(string userName,
                                           string password,
                                           TwoFactorCodeProvider twoFactorCodeProvider,
                                           MailboxPasswordProvider mailboxPasswordProvider,
                                           HumanVerifier humanVerifier,
                                           CancellationToken cancellationToken);
    }
}
