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

using Nethereum.Util;
using NUnit.Framework;

namespace Tuvi.Core.Dec.Ethereum.Tests
{
    [TestFixture]
    public sealed class KnownTransactionsPublicKeyTests
    {
        [Test]
        public void LegacySamplePublicKeyCompressionAndAddress()
        {
            const string UncompressedHex = "041402c65b8eec5727f591c5ae72a050860f1778b341ce6f8812c49220708f0a41a7800f5bc4b727f05c1350059ff59f2471fe4a8b68361ca323f1281d0ca8d866";
            const string CompressedHexExpected = "0x021402c65b8eec5727f591c5ae72a050860f1778b341ce6f8812c49220708f0a41";
            const string PubHashExpected = "0x8251a3c2345e2fcb3982a7ed3c02cebb49f6e8f1fc96158099ffa064bbfee38b";
            const string AddressExpected = "0x3c02cebB49F6e8f1FC96158099fFA064bBfeE38B";
            const int V = 38;

            var uncompressed = HexToBytes(UncompressedHex);
            Assert.That(uncompressed.Length, Is.EqualTo(65));
            Assert.That(uncompressed[0], Is.EqualTo(0x04));

            // Compress and compare
            var xy = new byte[64]; Buffer.BlockCopy(uncompressed, 1, xy, 0, 64);
            var compressed = Compress(xy);
            var compressedHex = "0x" + BitConverter.ToString(compressed).Replace("-", string.Empty, StringComparison.Ordinal);
            Assert.That(compressedHex, Is.EqualTo(CompressedHexExpected).IgnoreCase);

            // Hash and address
            var hash = Sha3Keccack.Current.CalculateHash(xy);
            var hashHex = "0x" + BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal);
            Assert.That(hashHex, Is.EqualTo(PubHashExpected).IgnoreCase);

            var addr = "0x" + BitConverter.ToString(hash, 12, 20).Replace("-", string.Empty, StringComparison.Ordinal);
            Assert.That(addr, Is.EqualTo(AddressExpected).IgnoreCase);

            // recId expectation from v (legacy EIP-155)
            var recId = (byte)((V >= 35) ? ((V - 35) % 2) : (V >= 27 ? V - 27 : V));
            Assert.That(recId, Is.EqualTo(1));
        }

        [Test]
        public void Type1SamplePublicKeyCompressionAndAddress()
        {
            const string UncompressedHex = "04b5f95b207b1d83cd604750760d4f5b3f855530524f3ab7447052574625a78a7cfd1a5560970556ee8f2f6401cdcb1a0b900d393e5b13ae7230ac26c547092b6e";
            const string CompressedHexExpected = "0x02b5f95b207b1d83cd604750760d4f5b3f855530524f3ab7447052574625a78a7c";
            const string PubHashExpected = "0x843db2c2f1d8735008e0b26a5f57effa42d10cd4b00c25d34a96f176c4c28ef8";
            const string AddressExpected = "0x5F57efFa42d10cD4B00C25d34A96f176c4C28Ef8";
            const int V = 0;

            var uncompressed = HexToBytes(UncompressedHex);
            Assert.That(uncompressed.Length, Is.EqualTo(65));
            Assert.That(uncompressed[0], Is.EqualTo(0x04));

            var xy = new byte[64]; Buffer.BlockCopy(uncompressed, 1, xy, 0, 64);
            var compressed = Compress(xy);
            var compressedHex = "0x" + BitConverter.ToString(compressed).Replace("-", string.Empty, StringComparison.Ordinal);
            Assert.That(compressedHex, Is.EqualTo(CompressedHexExpected).IgnoreCase);

            var hash = Sha3Keccack.Current.CalculateHash(xy);
            var hashHex = "0x" + BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal);
            Assert.That(hashHex, Is.EqualTo(PubHashExpected).IgnoreCase);

            var addr = "0x" + BitConverter.ToString(hash, 12, 20).Replace("-", string.Empty, StringComparison.Ordinal);
            Assert.That(addr, Is.EqualTo(AddressExpected).IgnoreCase);

            // recId expectation from v (typed)
            var recId = (byte)((V >= 27) ? (V % 2) : V);
            Assert.That(recId, Is.Zero);
        }

        private static byte[] HexToBytes(string hex)
        {
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private static byte[] Compress(byte[] xy)
        {
            var x = new byte[32]; var y = new byte[32]; Buffer.BlockCopy(xy, 0, x, 0, 32); Buffer.BlockCopy(xy, 32, y, 0, 32);
            var prefix = (y[31] & 1) == 0 ? (byte)0x02 : (byte)0x03; var res = new byte[33]; res[0] = prefix; Buffer.BlockCopy(x, 0, res, 1, 32); return res;
        }
    }
}
