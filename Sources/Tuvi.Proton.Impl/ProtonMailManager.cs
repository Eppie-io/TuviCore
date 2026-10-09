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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;
using Tuvi.Core.Mail;

namespace Tuvi.Proton.Impl
{
    public class ProtonHelper : IProtonLoginHelper, IProtonMailBoxFactory
    {
        private ProtonConfiguration Configuration { get; }

        public ProtonHelper(ProtonConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IMailBox CreateMailBox(Account account, ICredentialsProvider credentialsProvider, IStorage storage)
        {
            return MailBoxCreator.Create(account, credentialsProvider, storage, Configuration);
        }

        public async Task<ProtonCredentials> LoginAsync(string userName,
                                                        string password,
                                                        TwoFactorCodeProvider twoFactorCodeProvider,
                                                        MailboxPasswordProvider mailboxPasswordProvider,
                                                        HumanVerifier humanVerifier,
                                                        CancellationToken cancellationToken)
        {
            var (userId, refreshToken, saltedKeyPass) = await ClientAuth.LoginFullAsync(userName,
                                                                                        password,
                                                                                        twoFactorCodeProvider,
                                                                                        mailboxPasswordProvider,
                                                                                        humanVerifier,
                                                                                        Configuration,
                                                                                        cancellationToken).ConfigureAwait(false);

            return new ProtonCredentials
            {
                UserId = userId,
                RefreshToken = refreshToken,
                SaltedPassword = saltedKeyPass
            };
        }

        public bool TryDeserializeHumanVerificationResponse(string json, out IHumanVerificationData data)
        {
            data = null;
            if (HumanVerificationCaptchaResponse.TryDeserialize(json, out var captchaResponse) && captchaResponse.IsCaptcha())
            {
                data = captchaResponse;
                return true;
            }
            return false;
        }
    }

    internal class HumanVerificationCaptchaResponse : IHumanVerificationData
    {
        public string HumanVerificationType => "captcha";
        private const string PostMessageCaptchaTypeKey = "pm_captcha";

        [JsonPropertyName("type")]
        public string PostMessageType { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }

        public bool IsCaptcha()
        {
            return !string.IsNullOrEmpty(PostMessageType) && PostMessageType == PostMessageCaptchaTypeKey;
        }

        public static bool TryDeserialize(string json, out HumanVerificationCaptchaResponse response)
        {
            response = null;
            try
            {
                response = JsonSerializer.Deserialize<HumanVerificationCaptchaResponse>(json);
                return true;
            }
            catch (ArgumentNullException)
            { }
            catch (JsonException)
            { }
            catch (NotSupportedException)
            { }

            return false;
        }
    }
}
