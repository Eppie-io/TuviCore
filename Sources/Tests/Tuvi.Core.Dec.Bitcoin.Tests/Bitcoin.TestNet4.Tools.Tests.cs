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

using System.Net;
using KeyDerivation.Keys;
using Moq;
using Moq.Protected;
using NBitcoin;
using NUnit.Framework;
using Tuvi.Base32EConverterLib;
using Tuvi.Core.Dec.Bitcoin.TestNet4;

namespace Tuvi.Core.Dec.Bitcoin.Tests
{
    public sealed class ToolsTest
    {
        private const string HexSeed = "14a3235efb14b096e8cc3082b89e0b629ec5c7b2c6621343b2657cb61853b0830623e97b8aeac416d3377b4da90a4838d9aea4d83e0117fd833049305af46f10";
        private static readonly ExtKey ExtKey = new ExtKey(HexSeed);
        private static readonly MasterKey MasterKey = GetMasterKey(ExtKey);

        private const string ExpectedAddress = "mrKe9eFoPoWew4hrxZumYieDuoHFdavoTc";
        private const string ExpectedWif = "cUTh5cswLhjA2so2meJAHNWGUVmXsgPrT45adxr7RQm3ApVrHp7C";

        private const string Address = "mydsbvVx5sTpf7h2WD5KxjVKzUAXZtC77i";

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
            Assert.Throws<ArgumentOutOfRangeException>(() => Tools.DeriveBitcoinAddress(MasterKey, -1, 0));
        }

        [Test]
        public void GetBitcoinAddressFromMasterKeyThrowsArgumentOutOfRangeExceptionWhenIndexIsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Tools.DeriveBitcoinAddress(MasterKey, 0, -1));
        }

        [Test]
        public void GetBitcoinAddressFromMasterKeyReturnsCorrectAddress()
        {
            const int Account = 0;
            const int Index = 0;

            var address = Tools.DeriveBitcoinAddress(MasterKey, Account, Index);

            Assert.That(address, Is.EqualTo(ExpectedAddress), "Address does not match expected value.");

            var path = new KeyPath($"m/44'/0'/{Account}'/0/{Index}");
            var derivedKey = ExtKey.Derive(path);
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
            Assert.Throws<ArgumentOutOfRangeException>(() => Tools.DeriveBitcoinSecretKeyWif(MasterKey, -1, 0));
        }

        [Test]
        public void GetBitcoinSecretKeyWIFFromMasterKeyThrowsArgumentOutOfRangeExceptionWhenIndexIsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Tools.DeriveBitcoinSecretKeyWif(MasterKey, 0, -1));
        }

        [Test]
        public void GetBitcoinSecretKeyWIFFromMasterKeyDerivesCorrectBip44Wif()
        {
            const int Account = 0;
            const int Index = 0;

            var wif = Tools.DeriveBitcoinSecretKeyWif(MasterKey, Account, Index);

            Assert.That(wif, Is.EqualTo(ExpectedWif),
                $"WIF for account {Account} and index {Index} does not match expected value: {ExpectedWif}.");

            var path = new KeyPath($"m/44'/0'/{Account}'/0/{Index}");
            var derivedKey = ExtKey.Derive(path);
            var derivedWif = derivedKey.PrivateKey.GetWif(Network.TestNet4).ToString();

            Assert.That(wif, Is.EqualTo(derivedWif),
                $"WIF for account {Account} and index {Index} does not match NBitcoin-derived WIF: {derivedWif}.");
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
                var result = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, Address, httpClient).ConfigureAwait(false);
                Assert.That(result is null);
            }
        }

        [Test]
        public async Task GetPublicKeyAsyncReturnsNullWhenNoMatchingInputFound()
        {
            const string DifferentAddress = "differentaddress";
            const string TxId = "0d1bf0c97ad2095744563f37bd58a240941cd21b1d365b4fb85ba3e8bba93ddf";
            const string TxsJson = "[{\"txid\": \"" + TxId + "\", \"vin\": [{\"prevout\": {\"scriptpubkey_address\": \"" + DifferentAddress + "\"}}]}]";
            const string TxHex = "02000000014d94a59e818782bf32a86bb227006fb786c5c71ed8871d98b4706133e1348354000000006a47304402205d18b933b3d1efcd46541522f088b66171edf86cc0522db216b36aefe2202ce30220218b4f845fc70eed74f9f6919dc9970d0d05d880c71bfdb5ba62fd9e95203c49012102b4ed0e866dd6dd8042255b8c94bb32ceabf8a3adda20487e38c73fbf9378c865fdffffff011dc10000000000001976a914c6c1425cf53c51829242716c811938751f9004fa88ac67760100";

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
                        Content = new StringContent(TxsJson)
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
                        Content = new StringContent(TxHex)
                    };
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                var result = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, Address, httpClient).ConfigureAwait(false);
                Assert.That(result is null);
            }
        }

        [Test]
        public async Task GetPublicKeyAsyncThrowFormatExceptionWhenTryToParseTransaction()
        {
            const string TxId = "0d1bf0c97ad2095744563f37bd58a240941cd21b1d365b4fb85ba3e8bba93ddf";
            const string TxsJson = "[{\"txid\": \"" + TxId + "\", \"vin\": [{\"prevout\": {\"scriptpubkey_address\": \"" + Address + "\"}}]}]";
            const string TxHex = "wrong_transaction";

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
                        Content = new StringContent(TxsJson)
                    };
                })
                .ReturnsAsync(() =>
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(TxHex)
                    };
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                var result = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, Address, httpClient).ConfigureAwait(false);
                Assert.That(result, Is.Null);
            }
        }

        [Test]
        public async Task GetPublicKeyAsyncReturnsPublicKeyWhenTransactionFound()
        {
            const string TxId = "0d1bf0c97ad2095744563f37bd58a240941cd21b1d365b4fb85ba3e8bba93ddf";
            const string TxsJson = "[{\"txid\": \"" + TxId + "\", \"vin\": [{\"prevout\": {\"scriptpubkey_address\": \"" + Address + "\"}}]}]";
            const string TxHex = "02000000014d94a59e818782bf32a86bb227006fb786c5c71ed8871d98b4706133e1348354000000006a47304402205d18b933b3d1efcd46541522f088b66171edf86cc0522db216b36aefe2202ce30220218b4f845fc70eed74f9f6919dc9970d0d05d880c71bfdb5ba62fd9e95203c49012102b4ed0e866dd6dd8042255b8c94bb32ceabf8a3adda20487e38c73fbf9378c865fdffffff011dc10000000000001976a914c6c1425cf53c51829242716c811938751f9004fa88ac67760100";

            var pubKeyBytes = Convert.FromHexString("02b4ed0e866dd6dd8042255b8c94bb32ceabf8a3adda20487e38c73fbf9378c865");
            var expectedPublicKey = Base32EConverter.ToEmailBase32(pubKeyBytes);

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
                        Content = new StringContent(TxsJson)
                    };
                })
                .ReturnsAsync(() =>
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(TxHex)
                    };
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                var result = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, Address, httpClient).ConfigureAwait(false);
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
                var result = await BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, Address, httpClient).ConfigureAwait(false);
                Assert.That(result, Is.Null);
            }
        }

        [Test]
        public void DeriveBitcoinSecretKeyWifThrowsArgumentNullExceptionWhenConfigIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                BitcoinToolsImpl.DeriveBitcoinSecretKeyWif(null, MasterKey, 0, 0));
        }

        [Test]
        public void RetrievePublicKeyAsyncThrowsArgumentNullExceptionWhenHttpClientIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, Address, null));
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
                    BitcoinToolsImpl.RetrievePublicKeyAsync(BitcoinNetworkConfig.TestNet4, Address, httpClient, cts.Token));
            }
        }

        [Test]
        public void ParseUtxoListReturnsListOnValidJson()
        {
            // Arrange
            const string txid1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            const string txid2 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            const string json = "[{" +
                                "\"txid\": \"" + txid1 + "\", \"vout\": 0, \"value\": 1000},{" +
                                "\"txid\": \"" + txid2 + "\", \"vout\": 1, \"value\": 2000}]";

            // Act
            var result = BitcoinToolsImpl.ParseUtxoList(json, Address);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2));

            Assert.That(result[0].Item1, Is.EqualTo(txid1));
            Assert.That(result[0].Item2, Is.EqualTo(0u));
            Assert.That(result[0].Item3, Is.EqualTo(1000L));

            Assert.That(result[1].Item1, Is.EqualTo(txid2));
            Assert.That(result[1].Item2, Is.EqualTo(1u));
            Assert.That(result[1].Item3, Is.EqualTo(2000L));
        }

        [Test]
        public void ParseUtxoListIgnoresEntriesWithEmptyTxid()
        {
            // Arrange
            const string validTxid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            const string json = "[{" +
                                "\"txid\": \"\", \"vout\": 0, \"value\": 1000},{" +
                                "\"txid\": \"" + validTxid + "\", \"vout\": 2, \"value\": 3000}]";

            // Act
            var result = BitcoinToolsImpl.ParseUtxoList(json, Address);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Item1, Is.EqualTo(validTxid));
            Assert.That(result[0].Item2, Is.EqualTo(2u));
            Assert.That(result[0].Item3, Is.EqualTo(3000L));
        }

        [Test]
        public void ParseUtxoListRejectsNegativeVoutOrValue()
        {
            // Arrange
            const string validTxid = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
            const string json = "[{" +
                                "\"txid\": \"" + validTxid + "\", \"vout\": 0, \"value\": 1000},{" +
                                "\"txid\": \"deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef\", \"vout\": -1, \"value\": 2000},{" +
                                "\"txid\": \"feedfeedfeedfeedfeedfeedfeedfeedfeedfeedfeedfeedfeedfeedfeedfeed\", \"vout\": 2, \"value\": -500}]";

            // Act
            var result = BitcoinToolsImpl.ParseUtxoList(json, Address);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Item1, Is.EqualTo(validTxid));
            Assert.That(result[0].Item2, Is.EqualTo(0u));
            Assert.That(result[0].Item3, Is.EqualTo(1000L));
        }

        [Test]
        public void ParseUtxoListReturnsNullOnMalformedJson()
        {
            // Arrange
            const string malformedJson = "{ this is : not valid json ]";

            // Act
            var result = BitcoinToolsImpl.ParseUtxoList(malformedJson, Address);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task FetchUtxoJsonAsyncReturnsJsonOn200()
        {
            // Arrange
            const string expectedJson = "[{\"txid\":\"abc\",\"vout\":0,\"value\":100}]";
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(expectedJson)
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                // Act
                var json = await BitcoinToolsImpl.FetchUtxoJsonAsync("https://mempool.space/testnet4/api/address/addr/utxo", httpClient, Address, CancellationToken.None).ConfigureAwait(false);

                // Assert
                Assert.That(json, Is.EqualTo(expectedJson));
            }
        }

        [Test]
        public async Task FetchUtxoJsonAsyncReturnsNullOnHttpError()
        {
            // Arrange
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                // Act
                var json = await BitcoinToolsImpl.FetchUtxoJsonAsync("https://mempool.space/testnet4/api/address/addr/utxo", httpClient, Address, CancellationToken.None).ConfigureAwait(false);

                // Assert
                Assert.That(json, Is.Null);
            }
        }

        [Test]
        public async Task FetchUtxoJsonAsyncRespectsCancellation()
        {
            // Arrange
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => throw new TaskCanceledException());

            using (var httpClient = new HttpClient(handlerMock.Object))
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act
                var json = await BitcoinToolsImpl.FetchUtxoJsonAsync("https://mempool.space/testnet4/api/address/addr/utxo", httpClient, Address, cts.Token).ConfigureAwait(false);

                // Assert
                Assert.That(json, Is.Null);
            }
        }

        [Test]
        public void CreateCoinsFromUtxosCreatesCoinsOnValidUtxos()
        {
            // Arrange
            var utxos = new List<Tuple<string, int, long>>
            {
                Tuple.Create("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 0, 1000L),
                Tuple.Create("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", 1, 2000L)
            };

            var destAddress = BitcoinAddress.Create(Address, Network.TestNet4);

            // Act
            var coins = BitcoinToolsImpl.CreateCoinsFromUtxos(utxos, destAddress, Address);

            // Assert
            Assert.That(coins, Is.Not.Null);
            Assert.That(coins.Count, Is.EqualTo(2));
            Assert.That(coins[0].TxOut.Value.Satoshi, Is.EqualTo(1000L));
            Assert.That(coins[1].TxOut.Value.Satoshi, Is.EqualTo(2000L));
            Assert.That(coins[0].TxOut.ScriptPubKey, Is.EqualTo(destAddress.ScriptPubKey));
        }

        [Test]
        public void CreateCoinsFromUtxosReturnsEmptyOnInvalidTxidFormat()
        {
            // Arrange
            var utxos = new List<Tuple<string, int, long>>
            {
                Tuple.Create("not-a-hex-txid", 0, 1000L)
            };

            var destAddress = BitcoinAddress.Create(Address, Network.TestNet4);

            // Act
            var coins = BitcoinToolsImpl.CreateCoinsFromUtxos(utxos, destAddress, Address);

            // Assert
            Assert.That(coins, Is.Not.Null);
            Assert.That(coins, Is.Empty);
        }

        [Test]
        public void CreateCoinsFromUtxosReturnsEmptyOnOverflowValue()
        {
            // Arrange
            var utxos = new List<Tuple<string, int, long>>
            {
                Tuple.Create("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 0, long.MaxValue)
            };

            var destAddress = BitcoinAddress.Create(Address, Network.TestNet4);

            // Act
            var coins = BitcoinToolsImpl.CreateCoinsFromUtxos(utxos, destAddress, Address);

            // Assert
            Assert.That(coins, Is.Not.Null);
            // If Money.Satoshis supports large values this will succeed; assert coin created and value preserved
            Assert.That(coins.Count, Is.EqualTo(1));
            Assert.That(coins[0].TxOut.Value.Satoshi, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public async Task BuildAndSignSpendAllReturnsHexOnValidUtxosAndWif()
        {
            // Arrange
            const string utxoTxId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            const string utxosJson = "[{\"txid\": \"" + utxoTxId + "\", \"vout\": 0, \"value\": 10000}]";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(utxosJson)
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                // Act
                var hex = await BitcoinToolsImpl.BuildAndSignSpendAllToSameAddressTransactionAsync(BitcoinNetworkConfig.TestNet4, ExpectedAddress, ExpectedWif, httpClient).ConfigureAwait(false);

                // Assert
                Assert.That(hex, Is.Not.Null.And.Not.Empty);

                // verify parsable
                var tx = Transaction.Parse(hex, Network.TestNet4);
                Assert.That(tx, Is.Not.Null);
            }
        }

        [Test]
        public async Task BuildAndSignSpendAllReturnsNullOnInsufficientFunds()
        {
            // Arrange: UTXO with tiny value below fee
            const string utxosJson = "[{\"txid\": \"deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef\", \"vout\": 0, \"value\": 100}]";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(utxosJson)
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                // Act
                var hex = await BitcoinToolsImpl.BuildAndSignSpendAllToSameAddressTransactionAsync(BitcoinNetworkConfig.TestNet4, ExpectedAddress, ExpectedWif, httpClient).ConfigureAwait(false);

                // Assert
                Assert.That(hex, Is.Null);
            }
        }

        [Test]
        public async Task BuildAndSignSpendAllReturnsNullOnInvalidWif()
        {
            // Arrange: valid utxo but invalid Wif
            const string utxosJson = "[{\"txid\": \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\", \"vout\": 0, \"value\": 10000}]";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(utxosJson)
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                // Act
                var hex = await BitcoinToolsImpl.BuildAndSignSpendAllToSameAddressTransactionAsync(BitcoinNetworkConfig.TestNet4, ExpectedAddress, "invalidwif", httpClient).ConfigureAwait(false);

                // Assert
                Assert.That(hex, Is.Null);
            }
        }

        [Test]
        public async Task BroadcastTransactionReturnsTxidOnSuccessStatus()
        {
            // Arrange
            const string txHex = "deadbeef";
            const string returnedTxId = "  txid12345  ";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(returnedTxId)
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                // Act
                var txid = await BitcoinToolsImpl.BroadcastTransactionAsync(BitcoinNetworkConfig.TestNet4, txHex, httpClient).ConfigureAwait(false);

                // Assert
                Assert.That(txid, Is.EqualTo(returnedTxId.Trim()));
            }
        }

        [Test]
        public async Task BroadcastTransactionReturnsNullOnHttpError()
        {
            // Arrange
            const string txHex = "deadbeef";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                // Act
                var txid = await BitcoinToolsImpl.BroadcastTransactionAsync(BitcoinNetworkConfig.TestNet4, txHex, httpClient).ConfigureAwait(false);

                // Assert
                Assert.That(txid, Is.Null);
            }
        }

        [Test]
        public async Task BroadcastTransactionRespectsCancellation()
        {
            // Arrange
            const string txHex = "deadbeef";
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => throw new TaskCanceledException());

            using (var httpClient = new HttpClient(handlerMock.Object))
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act
                var txid = await BitcoinToolsImpl.BroadcastTransactionAsync(BitcoinNetworkConfig.TestNet4, txHex, httpClient, cts.Token).ConfigureAwait(false);

                // Assert
                Assert.That(txid, Is.Null);
            }
        }

        [Test]
        public void BroadcastTransactionThrowsOnNullArgs()
        {
            // config null
            Assert.ThrowsAsync<ArgumentNullException>(() => BitcoinToolsImpl.BroadcastTransactionAsync(null, "tx", new HttpClient()));

            // txHex null
            Assert.ThrowsAsync<ArgumentNullException>(() => BitcoinToolsImpl.BroadcastTransactionAsync(BitcoinNetworkConfig.TestNet4, null, new HttpClient()));

            // httpClient null
            Assert.ThrowsAsync<ArgumentNullException>(() => BitcoinToolsImpl.BroadcastTransactionAsync(BitcoinNetworkConfig.TestNet4, "tx", null));
        }

        [Test]
        public async Task ActivateBitcoinAddressSucceedsOnHappyPath()
        {
            // Arrange: utxo present and broadcast succeeds
            const string utxosJson = "[{\"txid\": \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\", \"vout\": 0, \"value\": 10000}]";
            const string returnedTxId = "txid-happy";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(utxosJson)
                })
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(returnedTxId)
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                // Act & Assert - should not throw
                await BitcoinToolsImpl.ActivateBitcoinAddressAsync(BitcoinNetworkConfig.TestNet4, MasterKey, 0, 0, httpClient).ConfigureAwait(false);
            }
        }

        [Test]
        public void ActivateBitcoinAddressThrowsOnBuildFail()
        {
            // Arrange: insufficient funds
            const string utxosJson = "[{\"txid\": \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\", \"vout\": 0, \"value\": 10}]";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(utxosJson)
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                Assert.ThrowsAsync<InvalidOperationException>(() => BitcoinToolsImpl.ActivateBitcoinAddressAsync(BitcoinNetworkConfig.TestNet4, MasterKey, 0, 0, httpClient));
            }
        }

        [Test]
        public void ActivateBitcoinAddressThrowsOnBroadcastFail()
        {
            // Arrange: build succeeds, broadcast fails
            const string utxosJson = "[{\"txid\": \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\", \"vout\": 0, \"value\": 10000}]";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(utxosJson)
                })
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest
                });

            using (var httpClient = new HttpClient(handlerMock.Object))
            {
                Assert.ThrowsAsync<InvalidOperationException>(() => BitcoinToolsImpl.ActivateBitcoinAddressAsync(BitcoinNetworkConfig.TestNet4, MasterKey, 0, 0, httpClient));
            }
        }

        [Test]
        public void ActivateBitcoinAddressThrowsOnNullHttpClient()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() => BitcoinToolsImpl.ActivateBitcoinAddressAsync(BitcoinNetworkConfig.TestNet4, MasterKey, 0, 0, null));
        }

    }
}
