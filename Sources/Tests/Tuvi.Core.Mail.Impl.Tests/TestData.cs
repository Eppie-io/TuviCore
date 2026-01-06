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

using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KeyDerivation;
using KeyDerivation.Keys;
using KeyDerivationLib;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Mail.Impl.Tests
{
    internal class AccountInfo
    {
        public const string Name = "Mail Test";
        public const string Email = "test@mail.box";

        public const string IncomingServerAddress = "imap.mail.box";
        public const int IncomingServerPort = 993;
        public const MailProtocol IncomingMailProtocol = MailProtocol.IMAP;

        public const string OutgoingServerAddress = "smtp.mail.box";
        public const int OutgoingServerPort = 465;
        public const MailProtocol OutgoingMailProtocol = MailProtocol.SMTP;

        public const string Password = "Pass123";
        public const string DataBasePassword = "123456";

        public static readonly ICredentialsProvider CredentialsProvider = new TestBasicCredentialsProvider()
        {
            BasicCredentials = new BasicCredentials()
            {
                UserName = Email,
                Password = Password
            }
        };

        public const string Name2 = " Mail Test 2";
        public const string Email2 = "mail2@mail.box";
        public const string Password2 = "Pass123";

        public static Account GetAccount()
        {
            var account = new Account
            {
                Email = new EmailAddress(Email, Name),

                IncomingServerAddress = IncomingServerAddress,
                IncomingServerPort = IncomingServerPort,
                IncomingMailProtocol = IncomingMailProtocol,

                OutgoingServerAddress = OutgoingServerAddress,
                OutgoingServerPort = OutgoingServerPort,
                OutgoingMailProtocol = OutgoingMailProtocol,

                AuthData = new BasicAuthData() { Password = Password },
            };

            return account;
        }

        internal class TestBasicCredentialsProvider : ICredentialsProvider
        {
            public BasicCredentials BasicCredentials { get; set; }

            public Task<AccountCredentials> GetCredentialsAsync(HashSet<string> supportedAuthMechanisms, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<AccountCredentials>(BasicCredentials);
            }
        }

        public static Account GetAccount2()
        {
            return new Account
            {
                Email = new EmailAddress(Email2, Name2),

                IncomingServerAddress = IncomingServerAddress,
                IncomingServerPort = IncomingServerPort,
                IncomingMailProtocol = IncomingMailProtocol,

                OutgoingServerAddress = OutgoingServerAddress,
                OutgoingServerPort = OutgoingServerPort,
                OutgoingMailProtocol = OutgoingMailProtocol,

                AuthData = new BasicAuthData() { Password = Password2 },
            };
        }
    }

    internal class EncryptionTestsData
    {
        // This is a test seed phrase, don't use it for anything but tests.
        public static readonly MasterKey ReceiverMasterKey = CreateTestMasterKeyForSeed(
            new string[]
            {
                "leopard", "vintage", "clinic", "bread", "edit", "way",
                "talk", "chapter", "topic", "exile", "naive", "dutch"
            });

        // This is a test seed phrase, don't use it for anything but tests.
        public static readonly MasterKey SenderMasterKey = CreateTestMasterKeyForSeed(
            new string[]
            {
                "soul", "guilt", "angle", "neck", "tuition", "usage",
                "clump", "mind", "neck", "kick", "island", "glove"
            });

        public const string Subject = "This is test Pgp encrypted and signed message";
        public const string PlainText = "Text of the test message";
        public const string HtmlText = "<p><strong><em>Hello world!</em></strong></p><ol><li>text 1</li><li>text 2</li></ol>";
        public static readonly Attachment Attachment = new Attachment { FileName = "text_file.txt", Data = Encoding.ASCII.GetBytes("text of file") };

        public static MasterKey CreateTestMasterKeyForSeed(string[] seed)
        {
            var factory = new MasterKeyFactory(new TestKeyDerivationDetailsProvider());
            factory.RestoreSeedPhrase(seed);
            return factory.GetMasterKey();
        }

        internal class TestKeyDerivationDetailsProvider : IKeyDerivationDetailsProvider
        {
            public string GetSaltPhrase()
            {
                return "Tuvi seed";
            }

            public int GetSeedPhraseLength()
            {
                return 12;
            }

            public Dictionary<SpecialPgpKeyType, string> GetSpecialPgpKeyIdentities()
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
