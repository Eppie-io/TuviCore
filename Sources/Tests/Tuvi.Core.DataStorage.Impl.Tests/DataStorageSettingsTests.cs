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
using NUnit.Framework;

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
