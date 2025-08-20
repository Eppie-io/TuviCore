using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tuvi.Core.Dec.Azure.Tests
{
    [Ignore("Integration tests for the WebDecStorageClient are disabled for ci.")]
    public class WebDecStorageTests
    {
        private IDecStorageClient Client;

        private byte[] ByteData = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x1A, 0x1B, 0x1C, 0x1D, 0x1F, 0x20, 0x21, 0x22, 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x1A, 0x1B, 0x1C, 0x1D, 0x1F, 0x20, 0x21, 0x22 };
        private string Address = "3C805EA90F9D0D54EC907B040B47DD";
        private string DataHash = "A81F9D5B6713C805EA90F9D0D54EC907B040B47DD482A47222EB9B9D1A7D32AA";
        // private string Url = "http://localhost:7071/api";
        private Uri Url = new Uri("https://testnet2.eppie.io/api");

        [SetUp]
        public void Setup()
        {
            Client = DecStorageBuilder.CreateAzureClient(Url);
        }

        [TearDown]
        public void Teardown()
        {
            Client.Dispose();
        }

        [Test]
        public async Task SendFunctionTest()
        {
            var hash = await Client.PutAsync(ByteData).ConfigureAwait(false);
            Assert.That(hash == DataHash, Is.True);
        }

        [Test]
        public async Task ListFunctionTest()
        {
            var hash = await Client.PutAsync(ByteData).ConfigureAwait(false);
            Assert.That(hash == DataHash, Is.True);

            await Client.SendAsync(Address, hash).ConfigureAwait(false);

            var list = await Client.ListAsync(Address).ConfigureAwait(false);
            Assert.That(list.Contains(hash), Is.True);
        }

        [Test]
        public async Task GetFunctionTest()
        {
            var hash = await Client.PutAsync(ByteData).ConfigureAwait(false);
            Assert.That(hash == DataHash, Is.True);

            var data = await Client.GetAsync(hash).ConfigureAwait(false);
            Assert.That(data.SequenceEqual(ByteData), Is.True);
        }
    }
}
