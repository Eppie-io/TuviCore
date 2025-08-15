using Moq;
using Moq.Protected;
using NUnit.Framework;
using System.Net;

namespace Tuvi.Core.Dec.Bitcoin.Tests
{
    [TestFixture]
    public sealed class FetchSpentTransactionTests
    {
        [Test]
        public async Task FetchSpentTransactionAsyncReturnsTransactionWhenJsonIsValid()
        {
            const string address = "mydsbvVx5sTpf7h2WD5KxjVKzUAXZtC77i";
            const string txId = "0d1bf0c97ad2095744563f37bd58a240941cd21b1d365b4fb85ba3e8bba93ddf";
            const string txsJson = "[{\"txid\": \"" + txId + "\", \"vin\": [{\"prevout\": {\"scriptpubkey_address\": \"" + address + "\"}}]}]";
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
                        Content = new StringContent(txHex)
                    };
                });

            using var httpClient = new HttpClient(handlerMock.Object);

            var result = await BitcoinToolsImpl.FetchSpentTransactionAsync(BitcoinNetworkConfig.TestNet4, "mydsbvVx5sTpf7h2WD5KxjVKzUAXZtC77i", httpClient, default).ConfigureAwait(false);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.GetHash().ToString(), Is.EqualTo(txId));
        }

        [Test]
        public async Task FetchSpentTransactionAsyncReturnsNullWhenApiReturnsEmpty()
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
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("{}")
                    };
                });

            using var httpClient = new HttpClient(handlerMock.Object);

            var result = await BitcoinToolsImpl.FetchSpentTransactionAsync(BitcoinNetworkConfig.TestNet4, "abc123", httpClient, default).ConfigureAwait(false);
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task FetchSpentTransactionAsyncThrowsJsonExceptionWhenJsonIsBroken()
        {
            const string brokenJson = "{ invalid json }";

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
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(brokenJson)
                    };
                });

            using var httpClient = new HttpClient(handlerMock.Object);

            var result = await BitcoinToolsImpl.FetchSpentTransactionAsync(BitcoinNetworkConfig.TestNet4, "abc123", httpClient, default).ConfigureAwait(false);
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task FetchSpentTransactionAsyncReturnsNullOnHttpError()
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

            using var httpClient = new HttpClient(handlerMock.Object);

            var result = await BitcoinToolsImpl.FetchSpentTransactionAsync(BitcoinNetworkConfig.TestNet4, "abc123", httpClient, default).ConfigureAwait(false);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void FetchSpentTransactionAsyncCancelsWhenTokenIsTriggered()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException());

            using var httpClient = new HttpClient(handlerMock.Object);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                 BitcoinToolsImpl.FetchSpentTransactionAsync(BitcoinNetworkConfig.TestNet4, "abc123", httpClient, cts.Token));
        }
    }

}
