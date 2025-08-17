using System;
using Tuvi.Core.Entities;
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
