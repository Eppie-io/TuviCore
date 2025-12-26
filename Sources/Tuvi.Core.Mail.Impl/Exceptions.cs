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
using TuviPgpLib.Entities;

namespace Tuvi.Core.Mail.Impl
{
    public class MessageEncryptionException : CryptoContextException
    {
        public MessageEncryptionException()
        {
        }

        public MessageEncryptionException(string message) : base(message)
        {
        }

        public MessageEncryptionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class MessageSigningException : CryptoContextException
    {
        public MessageSigningException()
        {
        }

        public MessageSigningException(string message) : base(message)
        {
        }

        public MessageSigningException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class MessageDecryptionException : CryptoContextException
    {
        public MessageDecryptionException()
        {
        }

        public MessageDecryptionException(string message) : base(message)
        {
        }

        public MessageDecryptionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class MessageSignatureVerificationException : CryptoContextException
    {
        public MessageSignatureVerificationException()
        {
        }

        public MessageSignatureVerificationException(string message) : base(message)
        {
        }

        public MessageSignatureVerificationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class NoSecretKeyException : CryptoContextException
    {
        public NoSecretKeyException()
        {
        }

        public NoSecretKeyException(string keyId) : base()
        {
            KeyId = keyId;
        }

        public NoSecretKeyException(string keyId, Exception innerException) : base(string.Empty, innerException)
        {
            KeyId = keyId;
        }

        public string KeyId { get; }
    }
}
