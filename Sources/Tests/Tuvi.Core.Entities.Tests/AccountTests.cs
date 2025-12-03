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

namespace Tuvi.Core.Entities.Test
{
    public class AccountTests
    {
        [Test]
        public void AccountDefaultHasCorrectExternalContentPolicy()
        {
            var account = Account.Default;

            Assert.That(account.ExternalContentPolicy, Is.EqualTo(ExternalContentPolicy.AlwaysAllow));
        }

        [Test]
        public void AccountEqualityWithSameExternalContentPolicy()
        {
            var account1 = new Account
            {
                Id = 1,
                Email = new EmailAddress("test@test.com"),
                ExternalContentPolicy = ExternalContentPolicy.Block,
                IncomingServerAddress = "imap.test.com",
                IncomingServerPort = 993,
                IncomingMailProtocol = MailProtocol.IMAP,
                OutgoingServerAddress = "smtp.test.com",
                OutgoingServerPort = 465,
                OutgoingMailProtocol = MailProtocol.SMTP,
                AuthData = new BasicAuthData { Password = "test" }
            };

            var account2 = new Account
            {
                Id = 1,
                Email = new EmailAddress("test@test.com"),
                ExternalContentPolicy = ExternalContentPolicy.Block,
                IncomingServerAddress = "imap.test.com",
                IncomingServerPort = 993,
                IncomingMailProtocol = MailProtocol.IMAP,
                OutgoingServerAddress = "smtp.test.com",
                OutgoingServerPort = 465,
                OutgoingMailProtocol = MailProtocol.SMTP,
                AuthData = new BasicAuthData { Password = "test" }
            };

            Assert.That(account1, Is.EqualTo(account2));
        }

        [Test]
        public void AccountEqualityWithDifferentExternalContentPolicy()
        {
            var account1 = new Account
            {
                Id = 1,
                Email = new EmailAddress("test@test.com"),
                ExternalContentPolicy = ExternalContentPolicy.AlwaysAllow,
                IncomingServerAddress = "imap.test.com",
                IncomingServerPort = 993,
                IncomingMailProtocol = MailProtocol.IMAP,
                OutgoingServerAddress = "smtp.test.com",
                OutgoingServerPort = 465,
                OutgoingMailProtocol = MailProtocol.SMTP,
                AuthData = new BasicAuthData { Password = "test" }
            };

            var account2 = new Account
            {
                Id = 1,
                Email = new EmailAddress("test@test.com"),
                ExternalContentPolicy = ExternalContentPolicy.Block,
                IncomingServerAddress = "imap.test.com",
                IncomingServerPort = 993,
                IncomingMailProtocol = MailProtocol.IMAP,
                OutgoingServerAddress = "smtp.test.com",
                OutgoingServerPort = 465,
                OutgoingMailProtocol = MailProtocol.SMTP,
                AuthData = new BasicAuthData { Password = "test" }
            };

            Assert.That(account1, Is.Not.EqualTo(account2));
        }

        [Test]
        public void ExternalContentPolicyEnumValues()
        {
            Assert.That((int)ExternalContentPolicy.AlwaysAllow, Is.EqualTo(0));
            Assert.That((int)ExternalContentPolicy.AskEachTime, Is.EqualTo(1));
            Assert.That((int)ExternalContentPolicy.Block, Is.EqualTo(2));
        }

        [Test]
        public void AccountConstructorSetsDefaultExternalContentPolicy()
        {
            var account = new Account();

            Assert.That(account.ExternalContentPolicy, Is.EqualTo(ExternalContentPolicy.AlwaysAllow));
        }

        [Test]
        public void AccountCanSetExternalContentPolicy()
        {
            var account = new Account
            {
                ExternalContentPolicy = ExternalContentPolicy.AskEachTime
            };

            Assert.That(account.ExternalContentPolicy, Is.EqualTo(ExternalContentPolicy.AskEachTime));

            account.ExternalContentPolicy = ExternalContentPolicy.Block;

            Assert.That(account.ExternalContentPolicy, Is.EqualTo(ExternalContentPolicy.Block));
        }
    }
}
