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

        public const string ProtonBody1 = "0sCAAWFptg6Jk8Z0bMUZ0B8sMe3lqPNSsBuYizitEfxyVPBhDXD/P4p0PVyM1KER1XqK0W3jl2tGTp6uFUqSARCZZBp8Bb3VllP3WrEDWyy3o8EGRc6PrYno+TpZ1v6HHxqI9IHMJb0CwFNqGFtI5c6xZ/u4Lt2vEW/Vfp53GigSg0QKSWb0FMdaGcMtnef3wuGJxMAAEB+Tz+1J8scTF22RdJkrgV77aC+X2UlcxMiP4B8IUDptjPt/K5K+ltMHKH4e9ZX5eRww2sAHIlBHHZewQOI8Qk0q8Tf9DEmqRcF+Ih6cWblbPvAr8Ijs9am2G3DHnV2RyDZRH/ex4ioxuCgYmROOw5qYorEQArp5cTmkGyA1gJX86SLZ3cU4J5cZoC7H+b0fc7hr7QsXf73x4PwD8cAIGJs/vjXou3B7D2R3D7g=";

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
