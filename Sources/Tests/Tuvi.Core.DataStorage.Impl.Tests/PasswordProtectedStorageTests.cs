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

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Entities;

namespace Tuvi.Core.DataStorage.Tests
{
    // These tests are synchronous.
    // Storage acts like a state machine.
    // Use separate storage files for each test if parallel execution is needed.
    public class PasswordProtectedStorageTests : TestWithStorageBase
    {
        [SetUp]
        public void SetupTest()
        {
            DeleteStorage();
            TestData.Setup();
        }

        [Test]
        public async Task StorageNotExist()
        {
            using (var storage = GetDataStorage())
            {
                Func<Task> openStorage = () => storage.OpenAsync(Password);
                Func<Task> createStorage = () => storage.CreateAsync(Password);

                await Assert.ThrowsAsync<DataBaseNotCreatedException>(openStorage).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(createStorage).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task StorageExist()
        {
            using (var storage = GetDataStorage())
            {
                Func<Task> createStorage = () => storage.CreateAsync(Password);
                Func<Task> openStorage = () => storage.OpenAsync(Password);

                await Assert.DoesNotThrowAsync(createStorage).ConfigureAwait(false);
                await Assert.ThrowsAsync<DataBaseAlreadyExistsException>(createStorage).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(openStorage).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task OpenAndSetPassword()
        {
            using (var storage = GetDataStorage())
            {
                Func<Task> createStorage = () => storage.CreateAsync(Password);
                Func<Task> openStorage = () => storage.OpenAsync(Password);

                await Assert.DoesNotThrowAsync(createStorage).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(openStorage).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task OpenWithCorrectPassword()
        {
            await OpenAndSetPassword().ConfigureAwait(false);

            using (var storage = GetDataStorage())
            {
                Func<Task> openStorage = () => storage.OpenAsync(Password);

                await Assert.DoesNotThrowAsync(openStorage).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task OpenWithIncorrectPassword()
        {
            await OpenAndSetPassword().ConfigureAwait(false);

            using (var storage = GetDataStorage())
            {
                Func<Task> openStorage = () => storage.OpenAsync(IncorrectPassword);

                await Assert.ThrowsAsync<DataBasePasswordException>(openStorage).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task ChangePassword()
        {
            using (var storage = GetDataStorage())
            {
                Func<Task> createStorage = () => storage.CreateAsync(Password);

                await Assert.DoesNotThrowAsync(createStorage).ConfigureAwait(false);
            }


            using (var storage = GetDataStorage())
            {
                Func<Task> changePassword = () => storage.ChangePasswordAsync(Password, NewPassword);

                await Assert.DoesNotThrowAsync(changePassword).ConfigureAwait(false);
            }

            using (var storage = GetDataStorage())
            {
                Func<Task> openWithNewPassword = () => storage.OpenAsync(NewPassword);

                await Assert.DoesNotThrowAsync(openWithNewPassword).ConfigureAwait(false);
            }

            using (var storage = GetDataStorage())
            {
                Func<Task> openWithOldPassword = () => storage.OpenAsync(Password);
                Func<Task> openWithNewPassword = () => storage.OpenAsync(NewPassword);

                await Assert.ThrowsAsync<DataBasePasswordException>(openWithOldPassword).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(openWithNewPassword).ConfigureAwait(false);
            }
        }

        [Test]
        public void ResetStorage()
        {
            using (var storage = GetDataStorage())
            {
                storage.CreateAsync(Password).Wait();
                storage.ResetAsync().Wait();
                Assert.That(DatabaseFileExists(), Is.False);
            }
        }

        [Test]
        public async Task MultiplePasswordChange()
        {
            const string NewPassword1 = "newPass1";
            const string NewPassword2 = "newPass2";
            const string NewPassword3 = "newPass3";

            using (var storage = GetDataStorage())
            {
                Func<Task> createStorage = () => storage.CreateAsync(Password);

                await Assert.DoesNotThrowAsync(createStorage).ConfigureAwait(false);
            }

            using (var storage = GetDataStorage())
            {
                Func<Task> changeToNewPassword1 = () => storage.ChangePasswordAsync(Password, NewPassword1);

                await Assert.DoesNotThrowAsync(changeToNewPassword1).ConfigureAwait(false);
            }

            using (var storage = GetDataStorage())
            {
                Func<Task> openWithOldPassword = () => storage.OpenAsync(Password);
                Func<Task> openWithNewPassword1 = () => storage.OpenAsync(NewPassword1);
                Func<Task> changeToNewPassword2 = () => storage.ChangePasswordAsync(NewPassword1, NewPassword2);

                await Assert.ThrowsAsync<DataBasePasswordException>(openWithOldPassword).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(openWithNewPassword1).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(changeToNewPassword2).ConfigureAwait(false);
            }

            using (var storage = GetDataStorage())
            {
                Func<Task> openWithOldPassword = () => storage.OpenAsync(Password);
                Func<Task> openWithNewPassword1 = () => storage.OpenAsync(NewPassword1);
                Func<Task> openWithNewPassword2 = () => storage.OpenAsync(NewPassword2);
                Func<Task> changeToNewPassword3 = () => storage.ChangePasswordAsync(NewPassword2, NewPassword3);

                await Assert.ThrowsAsync<DataBasePasswordException>(openWithOldPassword).ConfigureAwait(false);
                await Assert.ThrowsAsync<DataBasePasswordException>(openWithNewPassword1).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(openWithNewPassword2).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(changeToNewPassword3).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task MultiplePasswordChangeWithReset()
        {
            const string NewPassword1 = "newPass1";
            const string NewPassword2 = "newPass2";
            const string NewPassword3 = "newPass3";

            // Change Password -> newPass1 -> newPass2 -> newPass3
            using (var storage = GetDataStorage())
            {
                Func<Task> createStorage = () => storage.CreateAsync(Password);
                Func<Task> openWithPassword = () => storage.OpenAsync(Password);
                Func<Task> changeToNewPassword1 = () => storage.ChangePasswordAsync(Password, NewPassword1);
                Func<Task> changeToNewPassword2 = () => storage.ChangePasswordAsync(NewPassword1, NewPassword2);
                Func<Task> changeToNewPassword3 = () => storage.ChangePasswordAsync(NewPassword2, NewPassword3);
                Func<Task> openWithNewPassword3 = () => storage.OpenAsync(NewPassword3);
                Func<Task> resetStorage = () => storage.ResetAsync();

                await Assert.DoesNotThrowAsync(createStorage).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(openWithPassword).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(changeToNewPassword1).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(changeToNewPassword2).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(changeToNewPassword3).ConfigureAwait(false);

                await Assert.DoesNotThrowAsync(openWithNewPassword3).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(resetStorage).ConfigureAwait(false);

                // Verify storage file removed and can be recreated again after reset
                Assert.That(DatabaseFileExists(), Is.False);
            }

            using (var storage = GetDataStorage())
            {
                Func<Task> openWithNewPassword3 = () => storage.OpenAsync(NewPassword3);
                Func<Task> createWithNewPassword3 = () => storage.CreateAsync(NewPassword3);

                await Assert.ThrowsAsync<DataBaseNotCreatedException>(openWithNewPassword3).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(createWithNewPassword3).ConfigureAwait(false);
                await Assert.DoesNotThrowAsync(openWithNewPassword3).ConfigureAwait(false);
            }
        }
    }
}
