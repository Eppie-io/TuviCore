using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Dec;
using Tuvi.Core.Impl;
using TuviPgpLib;

namespace Tuvi.Core.Mail.Impl
{
    internal class PgpDecProtector : IDecProtector
    {
        // TODO: commented code is an attempt to encrypt/decrypt using BC directly.
        // TODO: uncomment it if we will deside to use it
        //private static readonly DateTime KeyCreationTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private readonly IKeyStorage _keyStorage;

        public PgpDecProtector(IKeyStorage keyStorage)
        {
            _keyStorage = keyStorage;
        }

        public async Task<string> DecryptAsync(string identity, string tag, byte[] data, CancellationToken cancellationToken)
        {
            //return DecryptImplAsync(identity, tag, data, cancellationToken);
            using (var pgpContext = await EccPgpExtension.GetTemporalContextAsync(_keyStorage).ConfigureAwait(false))
            {
                var masterKey = await _keyStorage.GetMasterKeyAsync(cancellationToken).ConfigureAwait(false);
                return EccPgpExtension.Decrypt(pgpContext, masterKey, identity, tag, data, cancellationToken);
            }
        }

        public async Task<string> DecryptAsync(string identity, int account, byte[] data, CancellationToken cancellationToken)
        {
            //return DecryptImplAsync(identity, tag, data, cancellationToken);
            using (var pgpContext = await EccPgpExtension.GetTemporalContextAsync(_keyStorage).ConfigureAwait(false))
            {
                var masterKey = await _keyStorage.GetMasterKeyAsync(cancellationToken).ConfigureAwait(false);
                return EccPgpExtension.Decrypt(pgpContext, masterKey, identity, account, data, cancellationToken);
            }
        }

        public async Task<byte[]> EncryptAsync(string address, string data, CancellationToken cancellationToken)
        {
            //return EncryptImplAsync(address, data, cancellationToken);
            using (var pgpContext = await EccPgpExtension.GetTemporalContextAsync(_keyStorage).ConfigureAwait(false))
            {
                return EccPgpExtension.Encrypt(pgpContext, address, data, cancellationToken);
            }
        }

        //private static async Task<byte[]> EncryptImplAsync(string address, string data, CancellationToken cancellationToken)
        //{
        //    byte[] writeBuffer = ArrayPool<byte>.Shared.Rent(4096);
        //    var encryptor = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithmTag.Aes256, withIntegrityPacket: false);
        //    ECPublicKeyParameters reconvertedPublicKey = PublicKeyConverter.ConvertEmailNameToPublicKey(address);
        //    PgpPublicKey publicKey = new PgpPublicKey(PublicKeyAlgorithmTag.ECDH, reconvertedPublicKey, KeyCreationTime);
        //    encryptor.AddMethod(publicKey);

        //    using (var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(data)))
        //    using (var outputStream = new MemoryStream(capacity: (int)inputStream.Length))
        //    using (var encStream = encryptor.Open(outputStream, writeBuffer))
        //    {
        //        await inputStream.CopyToAsync(encStream, writeBuffer.Length, cancellationToken).ConfigureAwait(false);
        //        return outputStream.ToArray();
        //    }
        //}

        //private async Task<string> DecryptImplAsync(string identity, string tag, byte[] data, CancellationToken cancellationToken)
        //{
        //    var factory = new PgpObjectFactory(data);
        //    var pgpObject = factory.NextPgpObject();

        //    if (pgpObject is null)
        //    {
        //        return "";
        //    }

        //    var list = pgpObject as PgpEncryptedDataList;
        //    if (list is null)
        //    {
        //        return "";
        //    }
        //    PgpPrivateKey privateKey = null;
        //    using (var pgpContext = EccPgpExtension.GetTemporalContext(_keyStorage))
        //    {
        //        var masterKey = await _keyStorage.GetMasterKeyAsync(cancellationToken).ConfigureAwait(false);
        //        pgpContext.DeriveKeyPair(masterKey, identity, tag);
        //        var secretKey = pgpContext.EnumerateSecretKeys().Where(x => !x.IsMasterKey && !x.IsSigningKey).First();
        //        privateKey = secretKey.ExtractPrivateKeyUtf8(Array.Empty<char>());
        //    }

        //    var encryptedObject = list[0] as PgpPublicKeyEncryptedData;
        //    using (var inputStream = encryptedObject.GetDataStream(privateKey))
        //    {
        //        var outBytes = Streams.ReadAll(inputStream);
        //        //var factoryClean = new PgpObjectFactory(inputStream);
        //        //pgpObject = factory.NextPgpObject();
        //        //using (var outputStream = new MemoryStream(capacity: (int)inputStream.Length))
        //        //{
        //        //    inputStream.CopyTo(outputStream);
        //        return Encoding.UTF8.GetString(outBytes);
        //        //}
        //    }

        //    //using (var pgpContext = EccPgpExtension.GetTemporalContext(_keyStorage))
        //    //{
        //    //    var masterKey = await _keyStorage.GetMasterKeyAsync(cancellationToken).ConfigureAwait(false);
        //    //    return EccPgpExtension.Decrypt(pgpContext, masterKey, identity, tag, data, cancellationToken);
        //    //}
        //}
    }
}
