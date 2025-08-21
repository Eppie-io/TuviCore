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
        Message Sign(Message message);

        /// <summary>
        /// Encrypt <paramref name="message"/> using PGP.
        /// <paramref name="message"/> is modified during method execution.
        /// </summary>
        /// <returns>Modified message</returns>
        /// <exception cref="NoPublicKeyException"/>
        /// <exception cref="MessageEncryptionException"/>
        Task<Message> EncryptAsync(Message message);

        /// <summary>
        /// Sign and encrypt <paramref name="message"/> using PGP.
        /// <paramref name="message"/> is modified during method execution.
        /// </summary>
        /// <returns>Modified message</returns>
        /// <exception cref="NoSecretKeyException"/>
        /// <exception cref="NoPublicKeyException"/>
        /// <exception cref="MessageEncryptionException"/>
        Task<Message> SignAndEncryptAsync(Message message);

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
