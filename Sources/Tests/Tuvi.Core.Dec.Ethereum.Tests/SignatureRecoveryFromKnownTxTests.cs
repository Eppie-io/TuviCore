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

using System.Net;
using Nethereum.Signer;
using Nethereum.Util;
using NUnit.Framework;
using Tuvi.Core.Dec.Ethereum.Explorer;

namespace Tuvi.Core.Dec.Ethereum.Tests
{
    [TestFixture]
    public sealed class SignatureRecoveryFromKnownTxTests
    {
        private const string SampleTxHash = "0xb3e9f5f24182d9c865f08dd295ff9d159498889a20247d337e37af634d1bc9af";
        private const string SamplePubUncompressed = "04e95ba0b752d75197a8bad8d2e6ed4b9eb60a1e8b08d257927d0df4f3ea6860992aac5e614a83f1ebe4019300373591268da38871df019f694f8e3190e493e711";
        private const string SampleAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";

        private sealed class StaticJsonHandler : HttpMessageHandler
        {
            private readonly string _json;
            public StaticJsonHandler(string json) { _json = json ?? "{}"; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_json)
                });
            }
        }

        // Verifies recovery for a known EIP-1559 (type 0x02) transaction
        [Test]
        public async Task RecoversPublicKeyFromKnownType2Transaction()
        {
            const string RpcJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"blockHash\":\"0x5370c29d85ddbfcd1bd2173ca3f39e74d763106bc4cdd2af3922ef649e6377dc\",\"blockNumber\":\"0x163b600\",\"from\":\"0xd8da6bf26964af9d7eed9e03e53415d37aa96045\",\"gas\":\"0xb1e4\",\"gasPrice\":\"0xd80299a\",\"maxFeePerGas\":\"0xe212755\",\"maxPriorityFeePerGas\":\"0x39b2820\",\"hash\":\"0xb3e9f5f24182d9c865f08dd295ff9d159498889a20247d337e37af634d1bc9af\",\"input\":\"0xa9059cbb000000000000000000000000406d08142ab2d7580ac49ab7ab8d880208c2cdbc00000000000000000000000000000000000000000000000000000000fcd7c880\",\"nonce\":\"0x62f\",\"to\":\"0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48\",\"transactionIndex\":\"0x68\",\"value\":\"0x0\",\"type\":\"0x2\",\"accessList\":[],\"chainId\":\"0x1\",\"v\":\"0x0\",\"r\":\"0x4ea77ebbdbc2194a8603b1d8e13a1a50a871a50d3155ee31ed74f7a320a98dab\",\"s\":\"0x23ec6ee5ce4a69ed11161570d6c464c1454eb016adb90d7e0aea772d319c3038\",\"yParity\":\"0x0\"}}";

            using var handler = new StaticJsonHandler(RpcJson);
            using var httpClient = new HttpClient(handler, disposeHandler: true);
            var client = new EtherscanExplorerClient(httpClient, EthereumNetworkConfig.MainNet.ExplorerApiBaseUrl, string.Empty);

            var sigInfo = await client.TryGetSignatureInfoAsync(SampleTxHash, CancellationToken.None).ConfigureAwait(false);
            Assert.That(sigInfo, Is.Not.Null, "Signature info null");
            Assert.That(sigInfo!.MessageHash, Is.Not.Null.And.Not.Empty, "Message hash empty");
            Assert.That(sigInfo.R, Is.Not.Null.And.Not.Empty, "R empty");
            Assert.That(sigInfo.S, Is.Not.Null.And.Not.Empty, "S empty");

            var addr = RecoverAddress(sigInfo);
            Assert.That(addr, Is.EqualTo(SampleAddress.ToUpperInvariant()), "Address mismatch");

            var uncompressed = GetUncompressedHex(sigInfo);
            Assert.That(uncompressed, Is.EqualTo(SamplePubUncompressed).IgnoreCase, "Uncompressed pubkey mismatch");
        }

        // yParity should take precedence over v if both present
        [Test]
        public async Task RecoversPublicKeyWhenBothYParityAndVPresentVIgnored()
        {
            const string RpcJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"blockHash\":\"0x5370c29d85ddbfcd1bd2173ca3f39e74d763106bc4cdd2af3922ef649e6377dc\",\"blockNumber\":\"0x163b600\",\"from\":\"0xd8da6bf26964af9d7eed9e03e53415d37aa96045\",\"gas\":\"0xb1e4\",\"gasPrice\":\"0xd80299a\",\"maxFeePerGas\":\"0xe212755\",\"maxPriorityFeePerGas\":\"0x39b2820\",\"hash\":\"0xb3e9f5f24182d9c865f08dd295ff9d159498889a20247d337e37af634d1bc9af\",\"input\":\"0xa9059cbb000000000000000000000000406d08142ab2d7580ac49ab7ab8d880208c2cdbc00000000000000000000000000000000000000000000000000000000fcd7c880\",\"nonce\":\"0x62f\",\"to\":\"0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48\",\"transactionIndex\":\"0x68\",\"value\":\"0x0\",\"type\":\"0x2\",\"accessList\":[],\"chainId\":\"0x1\",\"v\":\"0x1b\",\"r\":\"0x4ea77ebbdbc2194a8603b1d8e13a1a50a871a50d3155ee31ed74f7a320a98dab\",\"s\":\"0x23ec6ee5ce4a69ed11161570d6c464c1454eb016adb90d7e0aea772d319c3038\",\"yParity\":\"0x0\"}}";

            using var handler = new StaticJsonHandler(RpcJson);
            using var httpClient = new HttpClient(handler, disposeHandler: true);
            var client = new EtherscanExplorerClient(httpClient, EthereumNetworkConfig.MainNet.ExplorerApiBaseUrl, string.Empty);

            var sigInfo = await client.TryGetSignatureInfoAsync(SampleTxHash, CancellationToken.None).ConfigureAwait(false);
            Assert.That(sigInfo, Is.Not.Null);
            Assert.That(sigInfo!.RecoveryId, Is.Zero);

            var uncompressed = GetUncompressedHex(sigInfo);
            Assert.That(uncompressed, Is.EqualTo(SamplePubUncompressed).IgnoreCase);
        }

        // Smoke tests for hashing paths (EIP-2930 and legacy EIP-155)
        [Test]
        public async Task RecoversPublicKeyFromEip2930WithAccessList()
        {
            const string RpcJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"type\":\"0x1\",\"chainId\":\"0x1\",\"nonce\":\"0x1\",\"gas\":\"0x5208\",\"gasPrice\":\"0x3b9aca00\",\"to\":\"0x0000000000000000000000000000000000000001\",\"value\":\"0x0\",\"input\":\"0x\",\"accessList\":[{\"address\":\"0x0000000000000000000000000000000000000002\",\"storageKeys\":[\"0x0000000000000000000000000000000000000000000000000000000000000000\"]}],\"v\":\"0x0\",\"r\":\"0x4ea77ebbdbc2194a8603b1d8e13a1a50a871a50d3155ee31ed74f7a320a98dab\",\"s\":\"0x23ec6ee5ce4a69ed11161570d6c464c1454eb016adb90d7e0aea772d319c3038\"}}";

            using var handler = new StaticJsonHandler(RpcJson);
            using var httpClient = new HttpClient(handler, disposeHandler: true);
            var client = new EtherscanExplorerClient(httpClient, EthereumNetworkConfig.MainNet.ExplorerApiBaseUrl, string.Empty);

            var sigInfo = await client.TryGetSignatureInfoAsync("0xdeadbeef", CancellationToken.None).ConfigureAwait(false);
            Assert.That(sigInfo, Is.Not.Null);
            Assert.That(sigInfo!.MessageHash, Is.Not.Empty);
        }

        [Test]
        public async Task RecoversPublicKeyFromLegacyEip155V()
        {
            const string RpcJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"nonce\":\"0x1\",\"gas\":\"0x5208\",\"gasPrice\":\"0x3b9aca00\",\"to\":\"0x0000000000000000000000000000000000000001\",\"value\":\"0x0\",\"input\":\"0x\",\"v\":\"0x25\",\"r\":\"0x4ea77ebbdbc2194a8603b1d8e13a1a50a871a50d3155ee31ed74f7a320a98dab\",\"s\":\"0x23ec6ee5ce4a69ed11161570d6c464c1454eb016adb90d7e0aea772d319c3038\"}}";

            using var handler = new StaticJsonHandler(RpcJson);
            using var httpClient = new HttpClient(handler, disposeHandler: true);
            var client = new EtherscanExplorerClient(httpClient, EthereumNetworkConfig.MainNet.ExplorerApiBaseUrl, string.Empty);

            var sigInfo = await client.TryGetSignatureInfoAsync("0xdeadbeef", CancellationToken.None).ConfigureAwait(false);
            Assert.That(sigInfo, Is.Not.Null);
            Assert.That(sigInfo!.MessageHash, Is.Not.Empty);
        }

        private static string GetUncompressedHex(EtherscanExplorerClient.SignatureInfo sigInfo)
        {
            var v = (byte)(27 + (sigInfo.RecoveryId & 0x01));
            var sig = EthECDSASignatureFactory.FromComponents(sigInfo.R, sigInfo.S, new[] { v });
            var key = EthECKey.RecoverFromSignature(sig, sigInfo.MessageHash);
            var pub = key!.GetPubKey();
            return pub.Length == 64
                ? ("04" + BitConverter.ToString(pub).Replace("-", string.Empty, StringComparison.Ordinal)).ToUpperInvariant()
                : BitConverter.ToString(pub).Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        }

        private static string RecoverAddress(EtherscanExplorerClient.SignatureInfo sigInfo)
        {
            var v = (byte)(27 + (sigInfo.RecoveryId & 0x01));
            var sig = EthECDSASignatureFactory.FromComponents(sigInfo.R, sigInfo.S, new[] { v });
            var key = EthECKey.RecoverFromSignature(sig, sigInfo.MessageHash);
            var pub = key!.GetPubKey();
            var xy = pub.Length == 65 ? new Span<byte>(pub, 1, 64).ToArray() : pub.Length == 64 ? pub : Array.Empty<byte>();
            var hash = Sha3Keccack.Current.CalculateHash(xy);
            return ("0x" + BitConverter.ToString(hash, 12, 20).Replace("-", string.Empty, StringComparison.Ordinal)).ToUpperInvariant();
        }
    }
}
