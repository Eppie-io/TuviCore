﻿using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;
using Tuvi.Core.Backup;
using Tuvi.Core.Backup.Impl;
using Tuvi.Core.Entities.Exceptions;

namespace BackupTests
{
    public class BackupSerializationTests : BaseBackupTest
    {
        [OneTimeSetUp]
        protected void InitializeContext()
        {
            Initialize();
        }

        [Test]
        [Category("Serialization")]
        [Category("Backup")]
        public async Task IntegerBinarySerializationAsync()
        {
            await Task.Run(() =>
            {
                foreach (var pair in TestData.IntegerToBufferPairs)
                {
                    var integer = pair.Item1;
                    var referenceBuffer = pair.Item2;
                    var actualBuffer = integer.ToByteBuffer();

                    Assert.That(referenceBuffer, Is.EqualTo(actualBuffer));
                }
            }).ConfigureAwait(true);
        }

        [Test]
        [Category("Serialization")]
        [Category("Backup")]
        public async Task IntegerBinaryDeserializationAsync()
        {
            await Task.Run(() =>
            {
                foreach (var pair in TestData.IntegerToBufferPairs)
                {
                    var referenceInteger = pair.Item1;
                    var buffer = pair.Item2;
                    var actualInterger = buffer.FromByteBuffer();

                    Assert.That(referenceInteger, Is.EqualTo(actualInterger));
                }
            }).ConfigureAwait(true);
        }

        [Test]
        [Category("Protection")]
        [Category("Backup")]
        public async Task DataProtectorTest()
        {
            using (var protectedData = new MemoryStream())
            using (var deatachedSignatureData = new MemoryStream())
            using (var publicKeyData = new MemoryStream())
            {
                using (var unprotectedData = new MemoryStream())
                {
                    await unprotectedData.WriteAsync(TestData.DataToProtect).ConfigureAwait(false);

                    unprotectedData.Position = 0;
                    await BackupDataProtector.LockDataAsync(unprotectedData, protectedData).ConfigureAwait(false);
                    await BackupDataProtector.CreateDetachedSignatureDataAsync(protectedData, deatachedSignatureData, publicKeyData).ConfigureAwait(false);
                }
                                
                using (var unprotectedData = new MemoryStream())
                {
                    protectedData.Position = 0;
                    deatachedSignatureData.Position = 0;
                    var signed = await BackupDataSignatureVerifier.VerifySignatureAsync(protectedData, deatachedSignatureData).ConfigureAwait(false);

                    Assert.That(signed, Is.True);

                    protectedData.Position = 0;
                    await BackupDataProtector.UnlockDataAsync(protectedData, unprotectedData).ConfigureAwait(false);                   

                    Assert.That(TestData.DataToProtect, Is.EqualTo(unprotectedData.ToArray()));
                }
            }
        }

        [Test]        
        [Category("Backup")]
        public async Task BuildThenParseBackupAsync()
        {
            using (var backup = await BuildBackupAsync().ConfigureAwait(true))
            {
                await ParseBackupAsync(backup).ConfigureAwait(true);
            }            
        }


        [Test]        
        [Category("Backup")]
        public async Task BackupParseIfNoObjectAsync()
        {
            using (var backup = new MemoryStream())
            {
                IBackupBuilder builder = BackupSerializationFactory.CreateBackupBuilder();

                await builder.SetVersionAsync(TestData.ProtocolVersion).ConfigureAwait(true);

                await builder.BuildBackupAsync(backup).ConfigureAwait(true);

                backup.Position = 0;

                IBackupParser parser = BackupSerializationFactory.CreateBackupParser();

                await parser.ParseBackupAsync(backup).ConfigureAwait(true);

                var version = await parser.GetVersionAsync().ConfigureAwait(true);
                Assert.That(TestData.ProtocolVersion, Is.EqualTo(version));

                Assert.ThrowsAsync<BackupDeserializationException>(() => parser.GetAccountsAsync());
            }
        }

        [Test]
        [Category("Backup")]
        public async Task BackupImportedPublicKeys()
        {
            using (var backup = new MemoryStream())
            {
                IBackupBuilder builder = BackupSerializationFactory.CreateBackupBuilder();

                await builder.SetImportedPublicKeysAsync(TestData.ImportedPublicKeys).ConfigureAwait(true);

                await builder.BuildBackupAsync(backup).ConfigureAwait(true);

                backup.Position = 0;

                IBackupParser parser = BackupSerializationFactory.CreateBackupParser();

                await parser.ParseBackupAsync(backup).ConfigureAwait(true);

                var importedPublicKeys = await parser.GetImportedPublicKeysAsync().ConfigureAwait(true);

                Assert.That(importedPublicKeys, Is.EqualTo(TestData.ImportedPublicKeys));
            }
        }

        [Test]        
        [Category("Backup")]
        public async Task ParseUnsupportedBackupAsync()
        {
            var brokenBackupData = new byte[] { 8, 5, 2, 0 };

            await Task.Run(() =>
            {
                using var backupData = new MemoryStream(brokenBackupData);
                IBackupParser parser = BackupSerializationFactory.CreateBackupParser();

                Assert.ThrowsAsync<BackupParsingException>(() => parser.ParseBackupAsync(backupData));
            }).ConfigureAwait(true);
        }

        [Test]
        [Category("Backup")]
        public async Task BackupSettings()
        {
            using (var backup = new MemoryStream())
            {
                IBackupBuilder builder = BackupSerializationFactory.CreateBackupBuilder();

                await builder.SetSettingsAsync(TestData.SomeSettings).ConfigureAwait(true);
                await builder.BuildBackupAsync(backup).ConfigureAwait(true);

                backup.Position = 0;

                IBackupParser parser = BackupSerializationFactory.CreateBackupParser();

                await parser.ParseBackupAsync(backup).ConfigureAwait(true);

                var settings = await parser.GetSettingsAsync().ConfigureAwait(true);

                Assert.That(settings.EppieAccountCounter, Is.EqualTo(TestData.SomeSettings.EppieAccountCounter));
                Assert.That(settings.BitcoinAccountCounter, Is.EqualTo(TestData.SomeSettings.BitcoinAccountCounter));
            }
        }
    }
}
