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
            context.GeneratePgpKeysByTagOld(TestData.MasterKey, TestData.BackupPgpKeyIdentity, TestData.BackupPgpKeyIdentity);

            var mailbox = TestData.GetAccount().GetMailbox();

            Fingerprint = context.GetSigningKey(mailbox).PublicKey.CreatePgpKeyInfo().Fingerprint;

            Assert.That(Fingerprint, Is.Not.Empty);

            BackupDataProtector = BackupProtectorCreator.CreateBackupProtector(context);
            BackupDataProtector.SetPgpKeyIdentity(TestData.BackupPgpKeyIdentity);

            BackupSerializationFactory = new JsonUtf8SerializationFactory(BackupDataProtector);
            BackupSerializationFactory.SetPackageIdentifier(TestData.BackupPackageIdentifier);

            var verificationKeyStorage = new MockPgpKeyStorage().Get();


            using (var verificationContext = new TuviPgpContext(verificationKeyStorage))
            using (var publicKeyStream = new MemoryStream())
            {
                verificationContext.LoadContextAsync().Wait();

                PublicKey = context.GetSigningKey(mailbox).PublicKey;

                Assert.That(PublicKey.IsMasterKey, Is.True);
                Assert.That(!PublicKey.IsEncryptionKey, Is.True);

                var fingerprint = PublicKey.CreatePgpKeyInfo().Fingerprint;
                Assert.That(fingerprint, Is.Not.Empty);
                Assert.That(Fingerprint == fingerprint, Is.True);

                var identities = new List<UserIdentity> { TestData.GetAccount().GetUserIdentity() };

                context.ExportPublicKeys(identities, publicKeyStream, false);
                publicKeyStream.Position = 0;

                verificationContext.ImportPublicKeys(publicKeyStream, false);

                var verificationPublicKey = verificationContext.EnumeratePublicKeys().Where(key => key.IsMasterKey == true && key.IsEncryptionKey == false).First();
                var verificationPublicKeyFingerprint = verificationPublicKey.CreatePgpKeyInfo().Fingerprint;
                Assert.That(verificationPublicKeyFingerprint, Is.Not.Empty);
                Assert.That(Fingerprint == verificationPublicKeyFingerprint, Is.True);

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
            if (backup is null)
            {
                throw new ArgumentNullException(nameof(backup));
            }

            backup.Position = 0;

            IBackupParser parser = BackupSerializationFactory.CreateBackupParser();

            await parser.ParseBackupAsync(backup).ConfigureAwait(true);

            var version = await parser.GetVersionAsync().ConfigureAwait(true);
            Assert.That(TestData.ProtocolVersion, Is.EqualTo(version));

            var accounts = await parser.GetAccountsAsync().ConfigureAwait(true);
            Assert.That(TestData.Account1, Is.EqualTo(accounts[0]));
            Assert.That(TestData.Account2, Is.EqualTo(accounts[1]));
        }
    }
}
