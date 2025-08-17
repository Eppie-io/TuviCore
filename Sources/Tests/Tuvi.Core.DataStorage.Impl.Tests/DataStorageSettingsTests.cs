using NUnit.Framework;
using System.Threading.Tasks;

namespace Tuvi.Core.DataStorage.Tests
{
    public class SettingsTests : TestWithStorageBase
    {
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

                Assert.That(settings.EppieAccountCounter == 0, Is.True);
                Assert.That(settings.BitcoinAccountCounter == 0, Is.True);

                settings.EppieAccountCounter = 123;
                settings.BitcoinAccountCounter = 321;
                await db.SetSettingsAsync(settings).ConfigureAwait(true);

                var settings2 = await db.GetSettingsAsync().ConfigureAwait(true);
                Assert.That(settings2.EppieAccountCounter == 123, Is.True);
                Assert.That(settings2.BitcoinAccountCounter == 321, Is.True);
            }
        }
    }
}
