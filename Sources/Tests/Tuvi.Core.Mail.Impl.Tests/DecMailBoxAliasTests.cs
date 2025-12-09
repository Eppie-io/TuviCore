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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KeyDerivation.Keys;
using Moq;
using NUnit.Framework;
using Tuvi.Core.DataStorage;
using Tuvi.Core.Dec;
using Tuvi.Core.Dec.Impl;
using Tuvi.Core.Entities;
using Tuvi.Core.Utils;
using TuviPgpLibImpl;

namespace Tuvi.Core.Mail.Impl.Tests
{
    [TestFixture]
    public class DecMailBoxAliasTests
    {
        private const int CoinType = 3630; // Eppie BIP44 coin type
        private const int Channel = 10;    // Email channel
        private const int KeyIndex = 0;    // Key index

        private static readonly string[] TestSeedPhrase1 = {
            "apple","apple","apple","apple","apple","apple",
            "apple","apple","apple","apple","apple","apple"
        };

        private sealed class DictNameResolver : IEppieNameResolver
        {
            private readonly Dictionary<string, string> _map;
            public DictNameResolver(Dictionary<string, string> map) => _map = map;
            public Task<string> ResolveAsync(string name, CancellationToken cancellationToken = default)
            {
                _map.TryGetValue(name, out var v); return Task.FromResult(v);
            }
        }

        private static PublicKeyService CreateService(Dictionary<string, string> map)
        {
            return PublicKeyService.CreateDefault(new DictNameResolver(map));
        }

        private sealed class CountingResolver : IEppieNameResolver
        {
            private readonly Dictionary<string, string> _map;
            public int CallCount { get; private set; }
            public CountingResolver(Dictionary<string, string> map) => _map = map;
            public Task<string> ResolveAsync(string name, CancellationToken cancellationToken = default)
            {
                CallCount++;
                _map.TryGetValue(name, out var v); return Task.FromResult(v);
            }
        }

        private static PublicKeyService CreateCountingService(CountingResolver resolver)
        {
            return PublicKeyService.CreateDefault(resolver);
        }

        private static (Mock<IDecStorage> storage, MasterKey masterKey) CreateKeyStorageMock()
        {
            var masterKey = EncryptionTestsData.CreateTestMasterKeyForSeed(TestSeedPhrase1);
            var messages = new Dictionary<EmailAddress, List<DecMessage>>();
            var storage = new Mock<IDecStorage>();
            storage.Setup(s => s.GetMasterKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(masterKey);
            storage.Setup(s => s.GetDecMessagesAsync(It.IsAny<EmailAddress>(), It.IsAny<Folder>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((EmailAddress email, Folder folder, int count, CancellationToken ct) =>
                   {
                       if (!messages.ContainsKey(email)) return new List<DecMessage>();
                       return messages[email].Where(m => m.FolderAttributes == FolderAttributes.Inbox).ToList();
                   });
            storage.Setup(s => s.AddDecMessageAsync(It.IsAny<EmailAddress>(), It.IsAny<DecMessage>(), It.IsAny<CancellationToken>()))
                   .Callback((EmailAddress email, DecMessage msg, CancellationToken ct) =>
                   {
                       if (!messages.ContainsKey(email)) messages[email] = new List<DecMessage>();
                       if (!messages[email].Contains(msg)) messages[email].Add(msg);
                   })
                   .ReturnsAsync((EmailAddress email, DecMessage msg, CancellationToken ct) => msg);
            storage.Setup(s => s.IsDecMessageExistsAsync(It.IsAny<EmailAddress>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);
            return (storage, masterKey);
        }

        private static string DerivePubKey(MasterKey masterKey, int accountIndex)
        {
            var key = EccPgpContext.GenerateEccPublicKey(masterKey, CoinType, accountIndex, Channel, KeyIndex);
            return PublicKeyService.CreateDefault(PublicKeyService.NoOpNameResolver).Encode(key);
        }

        private static Mock<IDecStorageClient> CreateClient()
        {
            var sent = new Dictionary<string, string>();
            var blobs = new Dictionary<string, byte[]>();
            var m = new Mock<IDecStorageClient>();
            m.Setup(c => c.PutAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((byte[] data, CancellationToken ct) =>
             {
                 var hash = GetSHA256(data); blobs[hash] = data; return hash;
             });
            m.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((string addr, string hash, CancellationToken ct) => { sent[hash] = addr; return hash; });
            m.Setup(c => c.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((string addr, CancellationToken ct) => sent.Where(x => x.Value == addr).Select(x => x.Key).ToList());
            m.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((string hash, CancellationToken ct) => blobs[hash]);
            return m;
        }

        private static string GetSHA256(byte[] data)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return StringHelper.BytesToHex(sha.ComputeHash(data));
        }

        private static Account CreateAccount(EmailAddress email, int accountIndex)
        {
            return new Account { Email = email, DecentralizedAccountIndex = accountIndex };
        }

        private static Message CreateMessage(EmailAddress from, EmailAddress to, uint id = 1)
        {
            var msg = new Message();
            msg.From.Add(from);
            msg.To.Add(to);
            msg.Subject = "Alias test message " + id;
            msg.TextBody = "Body";
            msg.Id = id;
            msg.Date = DateTimeOffset.UtcNow;
            msg.Folder = new Folder("Sent", FolderAttributes.Sent);
            return msg;
        }

        [Test]
        [Category("DecAlias")]
        public async Task AliasSelfSendAndReceive()
        {
            var (storageMock, masterKey) = CreateKeyStorageMock();
            var accountIndex = 1;
            var alias = "alias-self";
            var derivedKey = DerivePubKey(masterKey, accountIndex);
            var svc = CreateService(new Dictionary<string, string> { { alias, derivedKey } });
            var aliasEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias);
            var client = CreateClient();
            using var box = new DecMailBox(CreateAccount(aliasEmail, accountIndex), storageMock.Object, client.Object, new PgpDecProtector(storageMock.Object, svc), svc);
            var folders = await box.GetFoldersStructureAsync(default).ConfigureAwait(true);
            var sentFolder = folders.First(f => f.IsSent);
            var inbox = folders.First(f => f.IsInbox);
            var msg = CreateMessage(aliasEmail, aliasEmail);
            msg.Folder = sentFolder;

            await box.SendMessageAsync(msg, default).ConfigureAwait(true);
            var received = await box.GetMessagesAsync(inbox, 50, default).ConfigureAwait(true);

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].From.Single().Address, Is.EqualTo(aliasEmail.Address));
            Assert.That(received[0].To.Single().Address, Is.EqualTo(aliasEmail.Address));
        }

        [Test]
        [Category("DecAlias")]
        public async Task AliasDifferentAccountsSendReceive()
        {
            var (senderStorage, senderMaster) = CreateKeyStorageMock();
            var (receiverStorage, receiverMaster) = CreateKeyStorageMock();
            var senderIndex = 2; var receiverIndex = 3;
            var senderAlias = "alias-sender"; var receiverAlias = "alias-receiver";
            var senderKey = DerivePubKey(senderMaster, senderIndex);
            var receiverKey = DerivePubKey(receiverMaster, receiverIndex);
            var svc = CreateService(new Dictionary<string, string> { { senderAlias, senderKey }, { receiverAlias, receiverKey } });
            var senderEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, senderAlias);
            var receiverEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, receiverAlias);
            var client = CreateClient();
            using var senderBox = new DecMailBox(CreateAccount(senderEmail, senderIndex), senderStorage.Object, client.Object, new PgpDecProtector(senderStorage.Object, svc), svc);
            using var receiverBox = new DecMailBox(CreateAccount(receiverEmail, receiverIndex), receiverStorage.Object, client.Object, new PgpDecProtector(receiverStorage.Object, svc), svc);
            var sentFolder = (await senderBox.GetFoldersStructureAsync(default).ConfigureAwait(true)).First(f => f.IsSent);
            var inbox = (await receiverBox.GetFoldersStructureAsync(default).ConfigureAwait(true)).First(f => f.IsInbox);
            var msg = CreateMessage(senderEmail, receiverEmail);
            msg.Folder = sentFolder;

            await senderBox.SendMessageAsync(msg, default).ConfigureAwait(true);
            var received = await receiverBox.GetMessagesAsync(inbox, 20, default).ConfigureAwait(true);

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].From.Single().Address, Is.EqualTo(senderEmail.Address));
            Assert.That(received[0].To.Any(a => a.Address == receiverEmail.Address), Is.True);
        }

        [Test]
        [Category("DecAlias")]
        public async Task AliasUnresolvableThrowsOnSend()
        {
            var (storageMock, masterKey) = CreateKeyStorageMock();
            var alias = "alias-missing";
            var svc = CreateService(new Dictionary<string, string>()); // empty resolver map
            var aliasEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias);
            var client = CreateClient();
            using var box = new DecMailBox(CreateAccount(aliasEmail, 1), storageMock.Object, client.Object, new PgpDecProtector(storageMock.Object, svc), svc);
            var sentFolder = (await box.GetFoldersStructureAsync(default).ConfigureAwait(true)).First(f => f.IsSent);
            var msg = CreateMessage(aliasEmail, aliasEmail);
            msg.Folder = sentFolder;

            AsyncTestDelegate act = () => box.SendMessageAsync(msg, default);
            Assert.ThrowsAsync<NoPublicKeyException>(act);
        }

        [Test]
        [Category("DecAlias")]
        public async Task DirectKeyBypassesResolver()
        {
            var (storageMock, masterKey) = CreateKeyStorageMock();
            var index = 5;
            var directKey = DerivePubKey(masterKey, index);
            // Resolver map lacks directKey entry; should not be queried since segment already valid key
            var svc = CreateService(new Dictionary<string, string>());
            var email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, directKey);
            var client = CreateClient();
            using var box = new DecMailBox(CreateAccount(email, index), storageMock.Object, client.Object, new PgpDecProtector(storageMock.Object, svc), svc);
            var folders = await box.GetFoldersStructureAsync(default).ConfigureAwait(true);
            var sent = folders.First(f => f.IsSent);
            var inbox = folders.First(f => f.IsInbox);
            var msg = CreateMessage(email, email, 42);
            msg.Folder = sent;
            await box.SendMessageAsync(msg, default).ConfigureAwait(true);
            var received = await box.GetMessagesAsync(inbox, 10, default).ConfigureAwait(true);
            Assert.That(received.Count, Is.EqualTo(1));
        }

        [Test]
        [Category("DecAlias")]
        public async Task MixedAliasAndDirectKeySendResolverCalledOnce()
        {
            var (senderStorage, senderMaster) = CreateKeyStorageMock();
            var (aliasReceiverStorage, aliasReceiverMaster) = CreateKeyStorageMock();
            var (directReceiverStorage, directReceiverMaster) = CreateKeyStorageMock();

            var senderIndex = 6;
            var aliasReceiverIndex = 7;
            var directReceiverIndex = 8;

            var aliasName = "alias-mixed";
            var aliasKey = DerivePubKey(aliasReceiverMaster, aliasReceiverIndex);
            var directKey = DerivePubKey(directReceiverMaster, directReceiverIndex);

            var resolver = new CountingResolver(new Dictionary<string, string> { { aliasName, aliasKey } });
            var svc = CreateCountingService(resolver);

            var aliasEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, aliasName);
            var directKeyEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, directKey);
            var senderEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, DerivePubKey(senderMaster, senderIndex));

            var client = CreateClient();
            using var senderBox = new DecMailBox(CreateAccount(senderEmail, senderIndex), senderStorage.Object, client.Object, new PgpDecProtector(senderStorage.Object, svc), svc);
            using var aliasReceiverBox = new DecMailBox(CreateAccount(aliasEmail, aliasReceiverIndex), aliasReceiverStorage.Object, client.Object, new PgpDecProtector(aliasReceiverStorage.Object, svc), svc);
            using var directReceiverBox = new DecMailBox(CreateAccount(directKeyEmail, directReceiverIndex), directReceiverStorage.Object, client.Object, new PgpDecProtector(directReceiverStorage.Object, svc), svc);

            var sentFolder = (await senderBox.GetFoldersStructureAsync(default).ConfigureAwait(true)).First(f => f.IsSent);
            var aliasInbox = (await aliasReceiverBox.GetFoldersStructureAsync(default).ConfigureAwait(true)).First(f => f.IsInbox);
            var directInbox = (await directReceiverBox.GetFoldersStructureAsync(default).ConfigureAwait(true)).First(f => f.IsInbox);

            var msg = CreateMessage(senderEmail, aliasEmail, 1001);
            msg.To.Add(directKeyEmail);
            msg.Folder = sentFolder;

            await senderBox.SendMessageAsync(msg, default).ConfigureAwait(true);

            var aliasMessages = await aliasReceiverBox.GetMessagesAsync(aliasInbox, 10, default).ConfigureAwait(true);
            var directMessages = await directReceiverBox.GetMessagesAsync(directInbox, 10, default).ConfigureAwait(true);

            Assert.That(resolver.CallCount, Is.EqualTo(1), "Resolver should be called only for alias, not for direct key");
            Assert.That(aliasMessages.Count, Is.EqualTo(1));
            Assert.That(directMessages.Count, Is.EqualTo(1));
        }

        [Test]
        [Category("DecAlias")]
        public async Task MultipleAliasesAllResolvedDeliveredToEach()
        {
            var (senderStorage, senderMaster) = CreateKeyStorageMock();
            var (r1Storage, r1Master) = CreateKeyStorageMock();
            var (r2Storage, r2Master) = CreateKeyStorageMock();
            int r1Index = 9, r2Index = 10;
            string alias1 = "alias-multi-1", alias2 = "alias-multi-2";
            var key1 = DerivePubKey(r1Master, r1Index);
            var key2 = DerivePubKey(r2Master, r2Index);
            var resolver = new CountingResolver(new Dictionary<string, string> { { alias1, key1 }, { alias2, key2 } });
            var svc = CreateCountingService(resolver);
            var senderEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, DerivePubKey(senderMaster, 11));
            var a1Email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias1);
            var a2Email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias2);
            var client = CreateClient();
            using var senderBox = new DecMailBox(CreateAccount(senderEmail, 11), senderStorage.Object, client.Object, new PgpDecProtector(senderStorage.Object, svc), svc);
            using var r1Box = new DecMailBox(CreateAccount(a1Email, r1Index), r1Storage.Object, client.Object, new PgpDecProtector(r1Storage.Object, svc), svc);
            using var r2Box = new DecMailBox(CreateAccount(a2Email, r2Index), r2Storage.Object, client.Object, new PgpDecProtector(r2Storage.Object, svc), svc);
            var sentFolder = (await senderBox.GetFoldersStructureAsync(default).ConfigureAwait(true)).First(f => f.IsSent);
            var r1Inbox = (await r1Box.GetFoldersStructureAsync(default).ConfigureAwait(true)).First(f => f.IsInbox);
            var r2Inbox = (await r2Box.GetFoldersStructureAsync(default).ConfigureAwait(true)).First(f => f.IsInbox);
            var msg = CreateMessage(senderEmail, a1Email, 2001);
            msg.To.Add(a2Email);
            msg.Folder = sentFolder;

            await senderBox.SendMessageAsync(msg, default).ConfigureAwait(true);
            var r1Messages = await r1Box.GetMessagesAsync(r1Inbox, 10, default).ConfigureAwait(true);
            var r2Messages = await r2Box.GetMessagesAsync(r2Inbox, 10, default).ConfigureAwait(true);
            Assert.That(r1Messages.Count, Is.EqualTo(1));
            Assert.That(r2Messages.Count, Is.EqualTo(1));
            Assert.That(resolver.CallCount, Is.EqualTo(2));
        }

        [Test]
        [Category("DecAlias")]
        public async Task MultipleAliasesOneMissingSendFails()
        {
            var (senderStorage, senderMaster) = CreateKeyStorageMock();
            var (r1Storage, r1Master) = CreateKeyStorageMock();
            int r1Index = 12;
            string alias1 = "alias-present", alias2 = "alias-missing-send";
            var key1 = DerivePubKey(r1Master, r1Index);
            var resolver = new CountingResolver(new Dictionary<string, string> { { alias1, key1 } }); // alias2 absent
            var svc = CreateCountingService(resolver);
            var senderEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, DerivePubKey(senderMaster, 14));
            var a1Email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias1);
            var a2Email = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias2);
            var client = CreateClient();
            using var senderBox = new DecMailBox(CreateAccount(senderEmail, 14), senderStorage.Object, client.Object, new PgpDecProtector(senderStorage.Object, svc), svc);
            var sentFolder = (await senderBox.GetFoldersStructureAsync(default).ConfigureAwait(true)).First(f => f.IsSent);
            var msg = CreateMessage(senderEmail, a1Email, 3001);
            msg.To.Add(a2Email);
            msg.Folder = sentFolder;

            AsyncTestDelegate act = () => senderBox.SendMessageAsync(msg, default);
            Assert.ThrowsAsync<NoPublicKeyException>(act);
            Assert.That(resolver.CallCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        [Category("DecAlias")]
        public async Task TryToAddDecPublicKeysAsyncAddsAliasPublicKey()
        {
            var (storageMock, masterKey) = CreateKeyStorageMock();
            int aliasIndex = 15;
            string alias = "alias-import";
            var derivedKey = DerivePubKey(masterKey, aliasIndex);
            var resolver = new CountingResolver(new Dictionary<string, string> { { alias, derivedKey } });
            var svc = CreateCountingService(resolver);
            var aliasEmail = EmailAddress.CreateDecentralizedAddress(NetworkType.Eppie, alias);
            using var pgpContext = await TemporalKeyStorage.GetTemporalContextAsync(storageMock.Object).ConfigureAwait(true);
            Assert.That(pgpContext.GetPublicKeysInfo().Count, Is.Zero);
            await pgpContext.TryToAddDecPublicKeysAsync(new[] { aliasEmail }, svc, default).ConfigureAwait(true);
            Assert.That(pgpContext.GetPublicKeysInfo().Count, Is.Positive);
            Assert.That(resolver.CallCount, Is.EqualTo(2));
        }
    }
}
