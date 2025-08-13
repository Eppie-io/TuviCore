using KeyDerivation.Keys;
using Moq;
using Moq.Protected;
using NBitcoin;
using NUnit.Framework;
using System.Net;
using Tuvi.Base32EConverterLib;
using Tuvi.Core.Dec.Bitcoin.TestNet4;

namespace Tuvi.Core.Dec.Bitcoin.Tests
{
    public sealed class ToolsTest
    {
        private const string HexSeed = "14a3235efb14b096e8cc3082b89e0b629ec5c7b2c6621343b2657cb61853b0830623e97b8aeac416d3377b4da90a4838d9aea4d83e0117fd833049305af46f10";
        private static ExtKey _extKey = new ExtKey(HexSeed);
        private static MasterKey _masterKey = GetMasterKey(_extKey);

        private const string ExpectedAddress = "mrKe9eFoPoWew4hrxZumYieDuoHFdavoTc";
        private const string ExpectedWif = "cUTh5cswLhjA2so2meJAHNWGUVmXsgPrT45adxr7RQm3ApVrHp7C";

        private static MasterKey GetMasterKey(ExtKey extKey)
        {
            var privateKey = KeySerialization.ToPrivateDerivationKey(
                                extKey.PrivateKey.ToBytes(),
                                extKey.ChainCode
                            );
            var masterKey = privateKey.ToByteBuffer().ToMasterKey();
            return masterKey;
        }

        [Test]
        public void GetBitcoinAddressFromMasterKeyThrowsArgumentNullExceptionWhenMasterKeyIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => Tools.DeriveBitcoinAddress(null, 0, 0));
        }

        [Test]
        public void GetBitcoinAddressFromMasterKeyThrowsArgumentOutOfRangeExceptionWhenAccountIsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Tools.DeriveBitcoinAddress(_masterKey, -1, 0));
        }

        [Test]
        public void GetBitcoinAddressFromMasterKeyThrowsArgumentOutOfRangeExceptionWhenIndexIsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Tools.DeriveBitcoinAddress(_masterKey, 0, -1));
        }

        [Test]
        public void GetBitcoinAddressFromMasterKeyReturnsCorrectAddress()
        {
            var account = 0;
            var index = 0;

            var address = Tools.DeriveBitcoinAddress(_masterKey, account, index);

            Assert.That(address, Is.EqualTo(ExpectedAddress), "Address does not match expected value.");

            var path = new KeyPath($"m/44'/0'/{account}'/0/{index}");
            var derivedKey = _extKey.Derive(path);
            var pubKey = derivedKey.PrivateKey.PubKey;
            var derivedAddress = pubKey.GetAddress(ScriptPubKeyType.Legacy, Network.TestNet4).ToString();

            Assert.That(address, Is.EqualTo(derivedAddress), "Address does not match NBitcoin-derived address.");
        }

        [Test]
        public void GetBitcoinSecretKeyWIFFromMasterKeyThrowsArgumentNullExceptionWhenMasterKeyIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => Tools.DeriveBitcoinSecretKeyWif(null, 0, 0));
        }

        [Test]
        public void GetBitcoinSecretKeyWIFFromMasterKeyThrowsArgumentOutOfRangeExceptionWhenAccountIsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Tools.DeriveBitcoinSecretKeyWif(_masterKey, -1, 0));
        }

        [Test]
        public void GetBitcoinSecretKeyWIFFromMasterKeyThrowsArgumentOutOfRangeExceptionWhenIndexIsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Tools.DeriveBitcoinSecretKeyWif(_masterKey, 0, -1));
        }

        [Test]
        public void GetBitcoinSecretKeyWIFFromMasterKeyDerivesCorrectBip44Wif()
        {
            const int account = 0;
            const int index = 0;

            var wif = Tools.DeriveBitcoinSecretKeyWif(_masterKey, account, index);

            Assert.That(wif, Is.EqualTo(ExpectedWif),
                $"WIF for account {account} and index {index} does not match expected value: {ExpectedWif}.");

            var path = new KeyPath($"m/44'/0'/{account}'/0/{index}");
            var derivedKey = _extKey.Derive(path);
            var derivedWif = derivedKey.PrivateKey.GetWif(Network.TestNet4).ToString();

            Assert.That(wif, Is.EqualTo(derivedWif),
                $"WIF for account {account} and index {index} does not match NBitcoin-derived WIF: {derivedWif}.");
        }

        [Test]
        public void GetPublicKeyAsyncThrowsArgumentNullExceptionWhenAddressIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() => Tools.RetrievePublicKeyAsync(null));
        }

        [Test]
        public void GetPublicKeyAsyncThrowsArgumentNullExceptionWhenAddressIsEmpty()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() => Tools.RetrievePublicKeyAsync(string.Empty));
        }

        [Test]
        public async Task GetPublicKeyAsyncReturnsNullWhenNoTransactionFound()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    var response = new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("[]") // Empty transactions list
                    };
                    return response;
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                var result = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, "mydsbvVx5sTpf7h2WD5KxjVKzUAXZtC77i", httpClient).ConfigureAwait(false);
                Assert.That(result is null);
            }
        }

        [Test]
        public async Task GetPublicKeyAsyncReturnsNullWhenNoMatchingInputFound()
        {
            const string address = "mydsbvVx5sTpf7h2WD5KxjVKzUAXZtC77i";
            const string differentaddress = "differentaddress";
            const string txId = "0d1bf0c97ad2095744563f37bd58a240941cd21b1d365b4fb85ba3e8bba93ddf";
            const string txsJson = "[{\"txid\": \"" + txId + "\", \"vin\": [{\"prevout\": {\"scriptpubkey_address\": \"" + differentaddress + "\"}}]}]";
            const string txHex = "02000000014d94a59e818782bf32a86bb227006fb786c5c71ed8871d98b4706133e1348354000000006a47304402205d18b933b3d1efcd46541522f088b66171edf86cc0522db216b36aefe2202ce30220218b4f845fc70eed74f9f6919dc9970d0d05d880c71bfdb5ba62fd9e95203c49012102b4ed0e866dd6dd8042255b8c94bb32ceabf8a3adda20487e38c73fbf9378c865fdffffff011dc10000000000001976a914c6c1425cf53c51829242716c811938751f9004fa88ac67760100";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(txsJson)
                    };
                })
                .ReturnsAsync(() =>
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("[]") // Empty transactions list
                    };
                })
                .ReturnsAsync(() =>
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(txHex)
                    };
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                var result = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, address, httpClient).ConfigureAwait(false);
                Assert.That(result is null);
            }
        }

        [Test]
        public async Task GetPublicKeyAsyncThrowFormatExceptionWhenTryToParseTransaction()
        {
            const string address = "mydsbvVx5sTpf7h2WD5KxjVKzUAXZtC77i";
            const string txId = "0d1bf0c97ad2095744563f37bd58a240941cd21b1d365b4fb85ba3e8bba93ddf";
            const string txsJson = "[{\"txid\": \"" + txId + "\", \"vin\": [{\"prevout\": {\"scriptpubkey_address\": \"" + address + "\"}}]}]";
            const string txHex = "wrong_transaction";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(txsJson)
                    };
                })
                .ReturnsAsync(() =>
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(txHex)
                    };
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                var result = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, address, httpClient).ConfigureAwait(false);
                Assert.That(result, Is.Null);
            }
        }

        [Test]
        public async Task GetPublicKeyAsyncReturnsPublicKeyWhenTransactionFound()
        {
            const string address = "mydsbvVx5sTpf7h2WD5KxjVKzUAXZtC77i";
            const string txId = "0d1bf0c97ad2095744563f37bd58a240941cd21b1d365b4fb85ba3e8bba93ddf";
            const string txsJson = "[{\"txid\": \"" + txId + "\", \"vin\": [{\"prevout\": {\"scriptpubkey_address\": \"" + address + "\"}}]}]";
            const string txHex = "02000000014d94a59e818782bf32a86bb227006fb786c5c71ed8871d98b4706133e1348354000000006a47304402205d18b933b3d1efcd46541522f088b66171edf86cc0522db216b36aefe2202ce30220218b4f845fc70eed74f9f6919dc9970d0d05d880c71bfdb5ba62fd9e95203c49012102b4ed0e866dd6dd8042255b8c94bb32ceabf8a3adda20487e38c73fbf9378c865fdffffff011dc10000000000001976a914c6c1425cf53c51829242716c811938751f9004fa88ac67760100";

            var pubKeyBytes = Convert.FromHexString("02b4ed0e866dd6dd8042255b8c94bb32ceabf8a3adda20487e38c73fbf9378c865");
            var expectedPublicKey = Base32EConverter.ConvertBytesToEmailName(pubKeyBytes);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(txsJson)
                    };
                })
                .ReturnsAsync(() =>
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(txHex)
                    };
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                var result = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, address, httpClient).ConfigureAwait(false);
                Assert.That(result, Is.EqualTo(expectedPublicKey));
            }
        }

        [Test]
        public async Task GetPublicKeyAsyncHandlesHttpErrorGracefully()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.NotFound
                    };
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                var result = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, "mydsbvVx5sTpf7h2WD5KxjVKzUAXZtC77i", httpClient).ConfigureAwait(false);
                Assert.That(result, Is.Null);
            }
        }

        [Test]
        public void DeriveBitcoinSecretKeyWifThrowsArgumentNullExceptionWhenConfigIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                BitcoinToolsImpl.DeriveBitcoinSecretKeyWif(null, _masterKey, 0, 0));
        }

        [Test]
        public void RetrievePublicKeyAsyncThrowsArgumentNullExceptionWhenHttpClientIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, "mydsbvVx5sTpf7h2WD5KxjVKzUAXZtC77i", null));
        }

        [Test]
        public void RetrievePublicKeyAsyncThrowsArgumentExceptionWhenAddressIsInvalid()
        {
            using (var httpClient = new HttpClient())
            {
                Assert.ThrowsAsync<ArgumentException>(() =>
                    BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, "invalid_address", httpClient));
            }
        }

        [Test]
        public void RetrievePublicKeyAsyncCancelsOperationWhenCancellationTokenIsTriggered()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    // Simulate a delay or long-running operation
                    throw new TaskCanceledException();
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                Assert.ThrowsAsync<OperationCanceledException>(() =>
                    BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, "mydsbvVx5sTpf7h2WD5KxjVKzUAXZtC77i", httpClient, cts.Token));
            }
        }
    }
}
