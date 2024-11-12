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
