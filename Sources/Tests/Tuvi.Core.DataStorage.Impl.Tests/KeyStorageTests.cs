using KeyDerivation.Keys;
using KeyDerivationLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TuviPgpLib.Entities;

namespace Tuvi.Core.DataStorage.Tests
{
    // These tests are synchronous.
    // Storage acts like a state machine.
    // Use separate storage files for each test if parallel execution is needed.
    public class KeyStorageTests : TestWithStorageBase
    {
        [SetUp]
        public void SetupTest()
        {
            DeleteStorage();
        }

        private static MasterKey GenerateRandomMasterKey()
        {
            MasterKeyFactory factory = new MasterKeyFactory(new TestKeyDerivationDetailsProvider());
            factory.GenerateSeedPhrase();
            return factory.GetMasterKey();
        }

        private static PgpPublicKeyBundle GenerateRandomPgpPublicKeyBundleData()
        {
            var data = new byte[100];

            RandomNumberGenerator.Fill(data);

            return new PgpPublicKeyBundle { Data = data };
        }

        private static PgpSecretKeyBundle GenerateRandomPgpSecretKeyBundleData()
        {
            var rand = new Random();
            var data = new byte[100];

            RandomNumberGenerator.Fill(data);

            return new PgpSecretKeyBundle { Data = data };
        }

        [Test]
        public void MasterKeyNotInitialized()
        {
            using (var storage = GetDataStorage())
            {
                storage.CreateAsync(Password).Wait();
                Assert.That(storage.IsMasterKeyExistAsync().Result, Is.False);
            }
        }

        [Test]
        public void MasterKeyInitialized()
        {
            using (var storage = GetDataStorage())
            {
                storage.CreateAsync(Password).Wait();
                storage.InitializeMasterKeyAsync(GenerateRandomMasterKey()).Wait();
                Assert.That(storage.IsMasterKeyExistAsync().Result, Is.True);
            }
        }

        [Test]
        public void GetMasterKey()
        {
            using (var storage = GetDataStorage())
            {
                storage.CreateAsync(Password).Wait();
                var masterKey = GenerateRandomMasterKey();
                storage.InitializeMasterKeyAsync(masterKey).Wait();
                Assert.That(masterKey, Is.EqualTo(storage.GetMasterKeyAsync().Result));
            }
        }

        [Test]
        public void PgpPublicKeysStorage()
        {
            using (var storage = GetDataStorage())
            {
                storage.CreateAsync(Password).Wait();
                storage.InitializeMasterKeyAsync(GenerateRandomMasterKey()).Wait();
                var keys = storage.GetPgpPublicKeysAsync().Result;
                Assert.That(null, Is.EqualTo(keys), "Key hasn't to exist.");

                var randomKeyData = GenerateRandomPgpPublicKeyBundleData();
                storage.SavePgpPublicKeys(randomKeyData);

                var extracted = storage.GetPgpPublicKeysAsync().Result;
                Assert.That(randomKeyData, Is.EqualTo(extracted), "Key data wasn't stored properly.");

                randomKeyData = GenerateRandomPgpPublicKeyBundleData();
                storage.SavePgpPublicKeys(randomKeyData);

                extracted = storage.GetPgpPublicKeysAsync().Result;
                Assert.That(randomKeyData, Is.EqualTo(extracted), "Key data wasn't updated properly.");
            }
        }

        [Test]
        public void PgpSecretKeysStorage()
        {
            using (var storage = GetDataStorage())
            {
                storage.CreateAsync(Password).Wait();
                storage.InitializeMasterKeyAsync(GenerateRandomMasterKey()).Wait();
                Assert.That(null, Is.EqualTo(storage.GetPgpSecretKeysAsync().Result), "Key hasn't to exist.");

                var randomKeyData = GenerateRandomPgpSecretKeyBundleData();
                storage.SavePgpSecretKeys(randomKeyData);

                var extracted = storage.GetPgpSecretKeysAsync().Result;
                Assert.That(randomKeyData, Is.EqualTo(extracted), "Key data wasn't stored properly.");

                randomKeyData = GenerateRandomPgpSecretKeyBundleData();
                storage.SavePgpSecretKeys(randomKeyData);

                extracted = storage.GetPgpSecretKeysAsync().Result;
                Assert.That(randomKeyData, Is.EqualTo(extracted), "Key data wasn't updated properly.");
            }
        }

        [Test]
        public async Task OpenCreateTest()
        {
            DeleteStorage();
            Assert.That(DatabaseFileExists(), Is.False);
            var storage = await CreateDataStorageAsync().ConfigureAwait(true);
            Assert.DoesNotThrow(() => storage.Dispose());
            Assert.That(DatabaseFileExists(), Is.True);
            Assert.DoesNotThrowAsync(async () => storage = await OpenDataStorageAsync().ConfigureAwait(true));
            Assert.DoesNotThrow(() => storage.Dispose());
            Assert.That(DatabaseFileExists(), Is.True);
            Assert.DoesNotThrowAsync(async () => storage = await OpenDataStorageAsync().ConfigureAwait(true));
            Assert.DoesNotThrowAsync(() => storage.ResetAsync());
            Assert.That(DatabaseFileExists(), Is.False);
            Assert.DoesNotThrow(() => storage.Dispose());
            Assert.DoesNotThrowAsync(async () => storage = await CreateDataStorageAsync().ConfigureAwait(true));
            Assert.That(DatabaseFileExists(), Is.True);
            Assert.DoesNotThrowAsync(() => storage.ResetAsync());
            Assert.That(DatabaseFileExists(), Is.False);
            Assert.DoesNotThrow(() => storage.Dispose());
            Assert.That(DatabaseFileExists(), Is.False);
        }

        [Test]
        public void OpenCreateMultithreadTest()
        {
            for (int j = 0; j < 100; ++j)
            {
                Assert.That(DatabaseFileExists(), Is.False);
                var tasks = new List<Task>();
                var storage = GetDataStorage();
                Assert.That(DatabaseFileExists(), Is.False);
                for (int i = 0; i < 100; ++i)
                {
                    tasks.Add(Task.Run(async () =>
                    {
#pragma warning disable CA5394 // Do not use insecure randomness
                        await Task.Delay(Random.Shared.Next(200)).ConfigureAwait(false);
#pragma warning restore CA5394 // Do not use insecure randomness
                        await storage.OpenAsync(Password).ConfigureAwait(false);
                        Assert.That(DatabaseFileExists(), Is.True);
                    }));
                }
                storage.ResetAsync(); // should wait all connections
                Assert.That(DatabaseFileExists(), Is.False);
            }

        }
    }
}
