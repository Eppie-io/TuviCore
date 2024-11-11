using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tuvi.Core.Dec.Azure.Tests
{
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

        [Test]
        public async Task SendFunctionTest()
        {
            var hash = await Client.SendAsync(Address, ByteData).ConfigureAwait(false);
            Assert.IsTrue(hash == DataHash);
        }

        [Test]
        public async Task ListFunctionTest()
        {
            var hash = await Client.SendAsync(Address, ByteData).ConfigureAwait(false);
            Assert.IsTrue(hash == DataHash);

            var list = await Client.ListAsync(Address).ConfigureAwait(false);
            Assert.IsTrue(list.Contains(hash));
        }

        [Test]
        public async Task GetFunctionTest()
        {
            var hash = await Client.SendAsync(Address, ByteData).ConfigureAwait(false);
            Assert.IsTrue(hash == DataHash);

            var data = await Client.GetAsync(Address, hash).ConfigureAwait(false);
            Assert.IsTrue(data.SequenceEqual(ByteData));
        }
    }
}
