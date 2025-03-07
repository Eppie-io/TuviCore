using MimeKit;
using MimeKit.Cryptography;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

[assembly: InternalsVisibleTo("Tuvi.Core.Mail.Tests")]
namespace Tuvi.Proton.Impl
{
    // SessionKey stores a decrypted session key.
    internal struct DecrypedSessionKey
    {
        // The decrypted binary session key.
        public byte[] Key { get; set; }
        // The symmetric encryption algorithm used with this key.
        public SymmetricKeyAlgorithmTag Algo { get; set; }
    }

    internal static class EmailAddressExtension
    {
        public static MailboxAddress ToMailboxAddress(this EmailAddress emailAddress)
        {
            return new MailboxAddress(emailAddress.Name ?? "", emailAddress.Address);
        }
    }

    internal static class Crypto
    {
        #region Public methods

        public static PgpPublicKey ExtractPgpPublicKey(string armored)
        {
            using (ArmoredInputStream keyIn = new ArmoredInputStream(
                new MemoryStream(Encoding.ASCII.GetBytes(armored))))
            {
                var pgpRing = new PgpPublicKeyRingBundle(keyIn);
                return pgpRing.GetKeyRings().First().GetPublicKeys().First(x => x.IsEncryptionKey && !x.IsMasterKey); // Find encrypting key
            }
        }

        public static (DecrypedSessionKey, byte[]) EncryptAndSignSplit(MyOpenPgpContext context, EmailAddress email, byte[] plain)
        {
            var signer = email.ToMailboxAddress();
            var encrypted = EncryptAndSign(context, email, plain);
            var privateKey = GetEncryptionPrivateKey(context, signer);
            (var encKey, var encBody) = Split(encrypted);
            return (DecryptSessionKey(privateKey, encrypted), encBody.ToArray());// here we pass encrypted since BC makes prefetch and read fails if there is nothing after session key packet
        }

        public static Stream EncryptAndSignArmored(MyOpenPgpContext context, EmailAddress emailAddress, byte[] data)
        {
            using (var inputData = new MemoryStream(data))
            {
                var signer = emailAddress.ToMailboxAddress();
                var encryptedMime = context.SignAndEncrypt(signer, DigestAlgorithm.Sha256, new List<MailboxAddress>() { signer }, inputData);

                var encryptedData = new MemoryStream();
                encryptedMime.WriteTo(FormatOptions.Default, encryptedData, contentOnly: true);
                encryptedData.Position = 0;
                return encryptedData;
            }
        }

        public static Stream EncryptArmored(MyOpenPgpContext context, EmailAddress emailAddress, byte[] data)
        {
            using (var inputData = new MemoryStream(data))
            {
                var signer = emailAddress.ToMailboxAddress();
                var encryptedMime = context.Encrypt(EncryptionAlgorithm.Aes256, new List<MailboxAddress>() { signer }, inputData);

                var encryptedData = new MemoryStream();
                encryptedMime.WriteTo(FormatOptions.Default, encryptedData, contentOnly: true);
                encryptedData.Position = 0;
                return encryptedData;
            }
        }

        public static Stream SignDetachedArmored(MyOpenPgpContext context, EmailAddress emailAddress, byte[] data)
        {
            using (var inputData = new MemoryStream(data))
            {
                var signer = emailAddress.ToMailboxAddress();
                var encryptedMime = context.Sign(signer, DigestAlgorithm.Sha512, inputData);

                var encryptedData = new MemoryStream();
                encryptedMime.WriteTo(FormatOptions.Default, encryptedData, contentOnly: true);
                encryptedData.Position = 0;
                return encryptedData;
            }
        }

        public static byte[] SignDetached(PgpPrivateKey privateKey, byte[] data)
        {
            PgpSignatureGenerator signGen = new PgpSignatureGenerator(PublicKeyAlgorithmTag.EdDsa, HashAlgorithmTag.Sha512);
            signGen.InitSign(PgpSignature.BinaryDocument, privateKey);

            signGen.Update(data);

            PgpSignature sig = signGen.Generate();
            using (var outputStream = new MemoryStream())
            {
                sig.Encode(outputStream);
                outputStream.Flush();
                outputStream.Position = 0;
                return outputStream.ToArray();
            }
        }

        public static byte[] SignDetached(MyOpenPgpContext context, EmailAddress emailAddress, byte[] data)
        {
            var secretKey = context.GetSigningKey(emailAddress.ToMailboxAddress());
            var privateKey = secretKey.ExtractPrivateKey(context.GetPassword(secretKey).ToCharArray());
            return SignDetached(privateKey, data);
        }

        public static byte[] EncryptAndSign(MyOpenPgpContext context, EmailAddress email, byte[] plain)
        {
            return ReadArmoredStream(EncryptAndSignArmored(context, email, plain));
        }

        public static (DecrypedSessionKey, byte[], byte[]) EncryptAttachment(MyOpenPgpContext context, EmailAddress emailAddress, byte[] data)
        {
            var privateKey = GetEncryptionPrivateKey(context, emailAddress.ToMailboxAddress());
            var bytes = ReadArmoredStream(EncryptArmored(context, emailAddress, data));
            var (encKey, encData) = Split(bytes);
            return (DecryptSessionKey(privateKey, bytes), encKey.ToArray(), encData.ToArray());
        }

        public static byte[] EncryptSessionKey(PgpPublicKey publicKey, DecrypedSessionKey sessionKey)
        {
            using (var inputStream = new MemoryStream(sessionKey.Key))
            {
                var bytes = Streams.ReadAll(EncryptStreamImpl(publicKey, sessionKey.Algo, inputStream, new MySecureRandom(sessionKey.Key)));
                (var encKey, _) = Split(bytes);
                return encKey.ToArray();
            }
        }

        public static DecrypedSessionKey DecryptSessionKey(PgpPrivateKey privateKey, byte[] encSessionKey)
        {
            var pgpF = new PgpObjectFactory(encSessionKey);
            var encList = (PgpEncryptedDataList)pgpF.NextPgpObject();
            var encP = (PgpPublicKeyEncryptedData)encList[0];
            var res = new DecrypedSessionKey();

            // HACK 
            var methodInfo = typeof(PgpPublicKeyEncryptedData).GetMethod("RecoverSessionData", BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Assert(methodInfo != null);
            byte[] sessionData = (byte[])methodInfo.Invoke(encP, new[] { privateKey });
            SymmetricKeyAlgorithmTag symmAlg = (SymmetricKeyAlgorithmTag)sessionData[0];
            Debug.Assert(symmAlg != SymmetricKeyAlgorithmTag.Null);

            res.Algo = symmAlg;
            res.Key = sessionData.AsSpan(1, sessionData.Length - 1 - 2).ToArray();

            return res;
        }

        public static string DecryptArmored(MyOpenPgpContext context, string encrypted, bool verifySignature = true)
        {
            using (var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(encrypted)))
            using (var decrypted = DecryptArmoredStream(context, memoryStream, verifySignature))
            using (var reader = new StreamReader(decrypted))
            {
                return reader.ReadToEnd();
            }
        }

        public static MemoryStream DecryptAttachment(MyOpenPgpContext context, string keyPackets, Stream dataStream)
        {
            var keyPacketsBytes = Convert.FromBase64String(keyPackets);
            using (var keyPacketStream = new MemoryStream(keyPacketsBytes))
            using (var inputStream = CombineStreams(new List<Stream>() { keyPacketStream, dataStream }))
            {
                return DecryptArmoredStream(context, inputStream, verifySignature: false);
            }
        }

        public static MemoryStream DecryptArmoredStream(MyOpenPgpContext context, Stream encrypted, bool verifySignature = true)
        {
            using (var mp = new MimePart()
            {
                ContentDisposition = new MimeKit.ContentDisposition("attachment"),
                Content = new MimeContent(encrypted)
            })
            using (MemoryStream stream = new MemoryStream())
            {
                mp.WriteTo(stream);
                stream.Position = 0;
                var decryptedData = new MemoryStream();
                var signatures = context.DecryptTo(stream, decryptedData);
                if (signatures != null && verifySignature)
                {
                    foreach (var signature in signatures)
                    {
                        if (signature.Verify() == false)
                        {
                            throw new CoreException("Proton: Invalid signature");
                        }
                    }
                }
                decryptedData.Position = 0;
                return decryptedData;
            }
        }

        public static byte[] PackToMultipart(string messageID, string fileName, byte[] key, byte[] data, byte[] signature)
        {
            using (var multiPart = new Multipart(ContentDisposition.FormData))
            using (var outStream = new MemoryStream())
            {
                multiPart.Add(CreateFormDataField("MessageID", messageID));
                multiPart.Add(CreateFormDataField("Filename", fileName));
                multiPart.Add(CreateFormDataField("MIMEType", MimeTypes.GetMimeType(fileName)));
                multiPart.Add(CreateFormDataField("Disposition", ContentDisposition.Attachment));
                multiPart.Add(CreateFormDataField("ContentID", ""));

                multiPart.Add(CreateMimepart("KeyPackets", "blob", key));
                multiPart.Add(CreateMimepart("DataPacket", "blob", data));
                multiPart.Add(CreateMimepart("Signature", "blob", signature));
                FormatOptions options = new FormatOptions();
                options.AlwaysQuoteParameterValues = true;

                multiPart.WriteTo(options, outStream, contentOnly: true);
                outStream.Position = 0;
                return outStream.ToArray();
            }
        }

        public static async Task<byte[]> PackToMultipartAsync(string messageID, string fileName, byte[] key, byte[] data, byte[] signature)
        {
            using (var multipartFormData = new MultipartFormDataContent())
            using (var content = new StringContent(fileName))
            {
                multipartFormData.Add(content, "Filename");

                return await multipartFormData.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        public static PgpSecretKey GetEncryptionKey(MyOpenPgpContext context, MailboxAddress signer)
        {
            return context.EnumerateSecretKeyRings(signer)
                                   .SelectMany(x => x.GetSecretKeys())
                                   .Where(x => !x.IsMasterKey && x.PublicKey.IsEncryptionKey).First();
        }

        #endregion

        #region Private methods

        private static MimePart CreateFormDataField(string name, string value)
        {
            var part = new MimePart()
            {
                Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes(value)), ContentEncoding.Default),
                ContentDisposition = new ContentDisposition(ContentDisposition.FormData)
            };
            part.Headers.Remove("Content-Type");
            part.ContentDisposition.Parameters.Add("name", $"{name}");
            return part;
        }

        private static MimePart CreateMimepart(string name, string fileName, byte[] data)
        {
            var part = new MimePart()
            {
                Content = new MimeContent(new MemoryStream(data), ContentEncoding.Default),
                ContentDisposition = new ContentDisposition(ContentDisposition.FormData)
            };
            part.ContentDisposition.Parameters.Add("name", $"{name}");
            part.ContentDisposition.Parameters.Add("filename", $"{fileName}");
            return part;
        }
        internal static (Memory<byte>, Memory<byte>) Split(byte[] encrypted)
        {
            long splitPoint = GetSplitPoint(encrypted);
            if (splitPoint == -1)
            {
                throw new CoreException("invalid data");
            }

            var encSessionKey = encrypted.AsMemory(0, (int)splitPoint);
            var encBody = encrypted.AsMemory((int)splitPoint, (int)(encrypted.Length - splitPoint));

            return (encSessionKey, encBody);
        }

        private static BcpgInputStream GetPacketStream(Stream input)
        {
            // HACK 
            var methodInfo = typeof(BcpgInputStream).GetMethod("Wrap", BindingFlags.NonPublic | BindingFlags.Static);
            Debug.Assert(methodInfo != null);

            return (BcpgInputStream)methodInfo.Invoke(null, new[] { input });
        }

        private static long GetSplitPoint(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            using (var packets = GetPacketStream(stream))
            {
                while (true)
                {
                    var tag = packets.NextPacketTag();
                    if ((int)tag == -1)
                    {
                        return -1; // end of steam
                    }
                    switch (tag)
                    {
                        case PacketTag.PublicKeyEncryptedSession:
                        case PacketTag.SymmetricKeyEncryptedSessionKey:
                            packets.ReadPacket();
                            return stream.Position;
                        case PacketTag.SymmetricKeyEncrypted:
                        case PacketTag.SymmetricEncryptedIntegrityProtected:
                            return -1;
                    }
                }
            }
        }

        private static byte[] ReadArmoredStream(Stream armored)
        {
            using (var input = new ArmoredInputStream(armored))
            using (var output = new MemoryStream())
            {
                input.CopyTo(output);
                output.Position = 0;
                return output.ToArray();
            }
        }

        private static Stream EncryptStreamImpl(PgpPublicKey publicKey, SymmetricKeyAlgorithmTag alg, Stream inputStream, SecureRandom random)
        {
            byte[] writeBuffer = ArrayPool<byte>.Shared.Rent(4096);
            var encryptor = new PgpEncryptedDataGenerator(alg, random);
            encryptor.AddMethod(publicKey);

            var outputStream = new MemoryStream();
            using (var encStream = encryptor.Open(outputStream, writeBuffer))
            {
                inputStream.CopyTo(encStream, writeBuffer.Length);
            }
            outputStream.Position = 0;
            return outputStream;
        }

        private static Stream CombineStreams(IEnumerable<Stream> streams)
        {
            var res = new MemoryStream();
            using (var armored = new ArmoredOutputStream(res))
            {
                foreach (var stream in streams)
                {
                    stream.CopyTo(armored);
                }
            }
            res.Position = 0;
            return res;
        }

        private static PgpPrivateKey GetEncryptionPrivateKey(MyOpenPgpContext context, MailboxAddress signer)
        {
            var secretKey = GetEncryptionKey(context, signer);
            var privateKey = secretKey.ExtractPrivateKey(context.GetPassword(secretKey).ToCharArray());
            return privateKey;
        }

        #endregion

    }
}
