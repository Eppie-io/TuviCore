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

using NUnit.Framework;

namespace Tuvi.Core.Dec.Ethereum.Tests
{
    [TestFixture]
    public class PublicKeyEncodingTests
    {
        private const string UncompressedHex = "04e95ba0b752d75197a8bad8d2e6ed4b9eb60a1e8b08d257927d0df4f3ea6860992aac5e614a83f1ebe4019300373591268da38871df019f694f8e3190e493e711";
        private static byte[] UncompressedBytes => Convert.FromHexString(UncompressedHex);

        [Test]
        public void EncodePublicKeyUncompressed65CompressesTo33()
        {
            var input = UncompressedBytes;

            var base32 = EthereumClient.EncodePublicKey(input);
            var decoded = Tuvi.Base32EConverterLib.Base32EConverter.FromEmailBase32(base32);

            Assert.That(decoded.Length, Is.EqualTo(33));
            Assert.That(decoded[0] == 0x02 || decoded[0] == 0x03);
        }

        [Test]
        public void EncodePublicKeyRawXY64CompressesTo33()
        {
            var rawXY = new byte[64];
            Buffer.BlockCopy(UncompressedBytes, 1, rawXY, 0, 64);

            var base32 = EthereumClient.EncodePublicKey(rawXY);
            var decoded = Tuvi.Base32EConverterLib.Base32EConverter.FromEmailBase32(base32);

            Assert.That(decoded.Length, Is.EqualTo(33));
        }

        [Test]
        public void EncodePublicKeyCompressed33PassesThrough()
        {
            var rawXY = new byte[64];
            Buffer.BlockCopy(UncompressedBytes, 1, rawXY, 0, 64);
            var compressed = Compress(rawXY);

            var base32 = EthereumClient.EncodePublicKey(compressed);
            var decoded = Tuvi.Base32EConverterLib.Base32EConverter.FromEmailBase32(base32);

            Assert.That(decoded, Is.EqualTo(compressed));
        }

        [Test]
        public void EncodePublicKeyInvalidLengthThrows()
        {
            Assert.Throws<ArgumentNullException>(() => EthereumClient.EncodePublicKey(Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() => EthereumClient.EncodePublicKey(new byte[10]));
        }

        private static byte[] Compress(byte[] xy)
        {
            var x = new byte[32]; var y = new byte[32]; Buffer.BlockCopy(xy, 0, x, 0, 32); Buffer.BlockCopy(xy, 32, y, 0, 32);
            var prefix = (y[31] & 1) == 0 ? (byte)0x02 : (byte)0x03;
            var res = new byte[33];
            res[0] = prefix;
            Buffer.BlockCopy(x, 0, res, 1, 32);
            return res;
        }
    }
}
