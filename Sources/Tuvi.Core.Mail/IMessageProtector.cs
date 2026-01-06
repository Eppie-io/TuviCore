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

using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail
{
    /// <summary>
    /// Handling of PGP protected messages
    /// </summary>
    public interface IMessageProtector
    {
        /// <summary>
        /// Sign <paramref name="message"/> using PGP.
        /// <paramref name="message"/> is modified during method execution.
        /// </summary>
        /// <returns>Modified message</returns>
        /// <exception cref="NoSecretKeyException"/>
        /// <exception cref="MessageSigningException"/>
        Task<Message> SignAsync(Message message, CancellationToken cancellationToken);

        /// <summary>
        /// Encrypt <paramref name="message"/> using PGP.
        /// <paramref name="message"/> is modified during method execution.
        /// </summary>
        /// <returns>Modified message</returns>
        /// <exception cref="NoPublicKeyException"/>
        /// <exception cref="MessageEncryptionException"/>
        Task<Message> EncryptAsync(Message message, CancellationToken cancellationToken);

        /// <summary>
        /// Sign and encrypt <paramref name="message"/> using PGP.
        /// <paramref name="message"/> is modified during method execution.
        /// </summary>
        /// <returns>Modified message</returns>
        /// <exception cref="NoSecretKeyException"/>
        /// <exception cref="NoPublicKeyException"/>
        /// <exception cref="MessageEncryptionException"/>
        Task<Message> SignAndEncryptAsync(Message message, CancellationToken cancellationToken);

        /// <summary>
        /// Tries to decrypt and verify message signatures if needed.
        /// If <paramref name="message"/> has no any protection it is returned unmodified.
        /// Otherwise <paramref name="message"/> is modified.
        /// </summary>
        /// <returns>Modified message</returns>
        /// <exception cref="NoSecretKeyException"/>
        /// <exception cref="MessageDecryptionException"/>
        /// <exception cref="MessageSignatureVerificationException"/>
        Task<Message> TryVerifyAndDecryptAsync(Message message, CancellationToken cancellationToken = default);
    }
}
