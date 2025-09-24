using NUnit.Framework;
using System.Threading.Tasks;

namespace Tuvi.Core.DataStorage.Tests
{
    public class SettingsTests : TestWithStorageBase
    {
        private const int Const123 = 123;
        private const int Const321 = 321;
        private const int Const42 = 42;

        [SetUp]
        public async Task SetupAsync()
        {
            DeleteStorage();
            TestData.Setup();

            await CreateDataStorageAsync().ConfigureAwait(true);
        }

        [TearDown]
        public void Teardown()
        {
            DeleteStorage();
        }

        [Test]
        public async Task SetDecentralizedAccountCounter()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var settings = await db.GetSettingsAsync().ConfigureAwait(true);

                Assert.That(settings.EppieAccountCounter, Is.Zero);
                Assert.That(settings.BitcoinAccountCounter, Is.Zero);
                Assert.That(settings.EthereumAccountCounter, Is.Zero);

                settings.EppieAccountCounter = Const123;
                settings.BitcoinAccountCounter = Const321;
                settings.EthereumAccountCounter = Const42;
                await db.SetSettingsAsync(settings).ConfigureAwait(true);

                var settings2 = await db.GetSettingsAsync().ConfigureAwait(true);
                Assert.That(settings2.EppieAccountCounter, Is.EqualTo(Const123));
                Assert.That(settings2.BitcoinAccountCounter, Is.EqualTo(Const321));
                Assert.That(settings2.EthereumAccountCounter, Is.EqualTo(Const42));
            }
        }
    }
}
