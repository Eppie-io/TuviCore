using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SecurityManagementTests;
using Tuvi.Core.Backup;
using Tuvi.Core.Backup.Impl;
using Tuvi.Core.Backup.Impl.JsonUtf8;
using Tuvi.Core.Entities;
using TuviPgpLib.Entities;
using TuviPgpLibImpl;

namespace BackupTests
{
    public class BaseBackupTest
    {
        protected IBackupProtector BackupDataProtector { get; private set; }
        protected IBackupProtector BackupDataSignatureVerifier { get; private set; }

        protected static IBackupSerializationFactory BackupSerializationFactory { get; private set; }

        protected string Fingerprint { get; private set; }
        protected Org.BouncyCastle.Bcpg.OpenPgp.PgpPublicKey PublicKey { get; private set; }

        protected void Initialize()
        {
            var keyStorage = new MockPgpKeyStorage().Get();
            using var context = new TuviPgpContext(keyStorage);
            context.LoadContextAsync().Wait();
            context.DeriveKeyPair(TestData.MasterKey, TestData.BackupPgpKeyIdentity);

            var mailbox = TestData.GetAccount().GetMailbox();

            Fingerprint = context.GetSigningKey(mailbox).PublicKey.CreatePgpKeyInfo().Fingerprint;

            Assert.IsNotEmpty(Fingerprint);

            BackupDataProtector = BackupProtectorCreator.CreateBackupProtector(context);
            BackupDataProtector.SetPgpKeyIdentity(TestData.BackupPgpKeyIdentity);

            BackupSerializationFactory = new JsonUtf8SerializationFactory(BackupDataProtector);
            BackupSerializationFactory.SetPackageIdentifier(TestData.BackupPackageIdentifier);

            var verificationKeyStorage = new MockPgpKeyStorage().Get();


            using(var verificationContext = new TuviPgpContext(verificationKeyStorage))
            using (var publicKeyStream = new MemoryStream())
            {
                verificationContext.LoadContextAsync().Wait();

                PublicKey = context.GetSigningKey(mailbox).PublicKey;

                Assert.IsTrue(!PublicKey.IsMasterKey);
                Assert.IsTrue(!PublicKey.IsEncryptionKey);
                
                var fingerprint = PublicKey.CreatePgpKeyInfo().Fingerprint;
                Assert.IsNotEmpty(fingerprint);
                Assert.IsTrue(Fingerprint == fingerprint);

                var identities = new List<UserIdentity> { TestData.GetAccount().GetUserIdentity() };

                context.ExportPublicKeys(identities, publicKeyStream, false);
                publicKeyStream.Position = 0;

                verificationContext.ImportPublicKeys(publicKeyStream, false);

                var verificationPublicKey = verificationContext.EnumeratePublicKeys().Where(key => key.IsMasterKey == false && key.IsEncryptionKey == false).First();
                var verificationPublicKeyFingerprint = verificationPublicKey.CreatePgpKeyInfo().Fingerprint;
                Assert.IsNotEmpty(verificationPublicKeyFingerprint);
                Assert.IsTrue(Fingerprint == verificationPublicKeyFingerprint);

                BackupDataSignatureVerifier = BackupProtectorCreator.CreateBackupProtector(verificationContext);
                BackupDataSignatureVerifier.SetPgpKeyIdentity(TestData.BackupPgpKeyIdentity);
            }
        }

        protected static async Task<Stream> BuildBackupAsync()
        {
            var backup = new MemoryStream();

            var builder = BackupSerializationFactory.CreateBackupBuilder();

            var account1 = TestData.Account1;
            var account2 = TestData.Account2;

            await builder.SetAccountsAsync(new List<Account> { account1, account2 }).ConfigureAwait(true);
            await builder.SetVersionAsync(TestData.ProtocolVersion).ConfigureAwait(true);

            await builder.BuildBackupAsync(backup).ConfigureAwait(true);

            return backup;
        }

        protected static async Task ParseBackupAsync(Stream backup)
        {
            if(backup == null)
            {
                throw new ArgumentNullException(nameof(backup));
            }

            backup.Position = 0;

            IBackupParser parser = BackupSerializationFactory.CreateBackupParser();

            await parser.ParseBackupAsync(backup).ConfigureAwait(true);

            var version = await parser.GetVersionAsync().ConfigureAwait(true);
            Assert.AreEqual(TestData.ProtocolVersion, version);

            var accounts = await parser.GetAccountsAsync().ConfigureAwait(true);
            Assert.AreEqual(TestData.Account1, accounts[0]);
            Assert.AreEqual(TestData.Account2, accounts[1]);
        }
    }
}
