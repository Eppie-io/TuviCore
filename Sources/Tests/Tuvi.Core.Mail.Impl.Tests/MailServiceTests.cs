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

using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Tuvi.Core.Entities;
using Tuvi.Core.Mail.Impl.Protocols;
using Tuvi.Core.Mail.Impl.Protocols.IMAP;

namespace Tuvi.Core.Mail.Impl.Tests
{
    public class MailServiceTests
    {
        private const string ServerAddress = "mail.test.com.test";
        private const int ServerPort = 12345;

        [SetUp]
        public void Setup()
        {

        }

        private static Mock<MailKit.Net.Imap.ImapClient> CreateClientMockForConnectTests()
        {
            var imapClientMock = new Mock<MailKit.Net.Imap.ImapClient>();
            imapClientMock.Setup(a => a.ConnectAsync(ServerAddress,
                                                     ServerPort,
                                                     MailKit.Security.SecureSocketOptions.Auto,
                                                     default));

            imapClientMock.Setup(a => a.ConnectAsync(It.Is<string>(s => s != ServerAddress),
                                                     ServerPort,
                                                     MailKit.Security.SecureSocketOptions.Auto,
                                                     default)).Throws(new System.Net.Sockets.SocketException());

            imapClientMock.Setup(a => a.ConnectAsync(ServerAddress,
                                                     It.Is<int>(i => i != ServerPort),
                                                     MailKit.Security.SecureSocketOptions.Auto,
                                                     default)).Throws(new System.Net.Sockets.SocketException());

            imapClientMock.Setup(a => a.ConnectAsync(It.Is<string>(s => s != ServerAddress),
                                                     It.Is<int>(i => i != ServerPort),
                                                     MailKit.Security.SecureSocketOptions.Auto,
                                                     default)).Throws(new System.Net.Sockets.SocketException());

            return imapClientMock;
        }

        [Test]
        public async Task ConnectWithCorrectParametersTest()
        {
            var imapClientMock = CreateClientMockForConnectTests();
            imapClientMock.Setup(a => a.IsConnected).Returns(true);
            var credentialsProviderMock = new Mock<ICredentialsProvider>();
            using var service = new IMAPMailService(imapClientMock.Object, ServerAddress, ServerPort, credentialsProviderMock.Object);
            await service.ConnectAsync(default).ConfigureAwait(false);
            Assert.That(service.IsConnected, Is.True);
        }

        [Test]
        public void ConnectWithIncorrectServerAddressTest()
        {
            var imapClientMock = CreateClientMockForConnectTests();
            var credentialsProviderMock = new Mock<ICredentialsProvider>();
            using var service = new IMAPMailService(imapClientMock.Object, "mail.test.com.mail.test", ServerPort, credentialsProviderMock.Object);
            Assert.ThrowsAsync<ConnectionException>(async () => await service.ConnectAsync(default).ConfigureAwait(false));
        }

        [Test]
        public void ConnectWithIncorrectPortNumberTest()
        {
            var imapClientMock = CreateClientMockForConnectTests();
            var credentialsProviderMock = new Mock<ICredentialsProvider>();
            using var service = new IMAPMailService(imapClientMock.Object, ServerAddress, 54321, credentialsProviderMock.Object);
            Assert.ThrowsAsync<ConnectionException>(async () => await service.ConnectAsync(default).ConfigureAwait(false));
        }

        [Test]
        public void ConnectWithIncorrectAddressNameAndPortNumberTest()
        {
            var imapClientMock = CreateClientMockForConnectTests();
            var credentialsProviderMock = new Mock<ICredentialsProvider>();
            using var service = new IMAPMailService(imapClientMock.Object, "mail.test.com.mail.test", 54321, credentialsProviderMock.Object);
            Assert.ThrowsAsync<ConnectionException>(async () => await service.ConnectAsync(default).ConfigureAwait(false));
        }


    }
}
