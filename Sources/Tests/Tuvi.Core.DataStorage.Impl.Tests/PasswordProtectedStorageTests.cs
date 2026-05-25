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
        public void StorageNotExist()
        {
            using (var storage = GetDataStorage())
            {
                Func<Task> openStorage = () => storage.OpenAsync(Password);
                Func<Task> createStorage = () => storage.CreateAsync(Password);

                Assert.ThrowsAsync<DataBaseNotCreatedException>(openStorage);
                Assert.DoesNotThrowAsync(createStorage);
            }
        }

        [Test]
        public void StorageExist()
        {
            using (var storage = GetDataStorage())
            {
                Func<Task> createStorage = () => storage.CreateAsync(Password);
                Func<Task> openStorage = () => storage.OpenAsync(Password);

                Assert.DoesNotThrowAsync(createStorage);
                Assert.ThrowsAsync<DataBaseAlreadyExistsException>(createStorage);
                Assert.DoesNotThrowAsync(openStorage);
            }
        }

        [Test]
        public void OpenAndSetPassword()
        {
            using (var storage = GetDataStorage())
            {
                Func<Task> createStorage = () => storage.CreateAsync(Password);
                Func<Task> openStorage = () => storage.OpenAsync(Password);

                Assert.DoesNotThrowAsync(createStorage);
                Assert.DoesNotThrowAsync(openStorage);
            }
        }

        [Test]
        public void OpenWithCorrectPassword()
        {
            OpenAndSetPassword();

            using (var storage = GetDataStorage())
            {
                Func<Task> openStorage = () => storage.OpenAsync(Password);

                Assert.DoesNotThrowAsync(openStorage);
            }
        }

        [Test]
        public void OpenWithIncorrectPassword()
        {
            OpenAndSetPassword();

            using (var storage = GetDataStorage())
            {
                Func<Task> openStorage = () => storage.OpenAsync(IncorrectPassword);

                Assert.ThrowsAsync<DataBasePasswordException>(openStorage);
            }
        }

        [Test]
        public void ChangePassword()
        {
            using (var storage = GetDataStorage())
            {
                Func<Task> createStorage = () => storage.CreateAsync(Password);

                Assert.DoesNotThrowAsync(createStorage);
            }


            using (var storage = GetDataStorage())
            {
                Func<Task> changePassword = () => storage.ChangePasswordAsync(Password, NewPassword);

                Assert.DoesNotThrowAsync(changePassword);
            }

            using (var storage = GetDataStorage())
            {
                Func<Task> openWithNewPassword = () => storage.OpenAsync(NewPassword);

                Assert.DoesNotThrowAsync(openWithNewPassword);
            }

            using (var storage = GetDataStorage())
            {
                Func<Task> openWithOldPassword = () => storage.OpenAsync(Password);
                Func<Task> openWithNewPassword = () => storage.OpenAsync(NewPassword);

                Assert.ThrowsAsync<DataBasePasswordException>(openWithOldPassword);
                Assert.DoesNotThrowAsync(openWithNewPassword);
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
        public void MultiplePasswordChange()
        {
            const string NewPassword1 = "newPass1";
            const string NewPassword2 = "newPass2";
            const string NewPassword3 = "newPass3";

            using (var storage = GetDataStorage())
            {
                Func<Task> createStorage = () => storage.CreateAsync(Password);

                Assert.DoesNotThrowAsync(createStorage);
            }

            using (var storage = GetDataStorage())
            {
                Func<Task> changeToNewPassword1 = () => storage.ChangePasswordAsync(Password, NewPassword1);

                Assert.DoesNotThrowAsync(changeToNewPassword1);
            }

            using (var storage = GetDataStorage())
            {
                Func<Task> openWithOldPassword = () => storage.OpenAsync(Password);
                Func<Task> openWithNewPassword1 = () => storage.OpenAsync(NewPassword1);
                Func<Task> changeToNewPassword2 = () => storage.ChangePasswordAsync(NewPassword1, NewPassword2);

                Assert.ThrowsAsync<DataBasePasswordException>(openWithOldPassword);
                Assert.DoesNotThrowAsync(openWithNewPassword1);
                Assert.DoesNotThrowAsync(changeToNewPassword2);
            }

            using (var storage = GetDataStorage())
            {
                Func<Task> openWithOldPassword = () => storage.OpenAsync(Password);
                Func<Task> openWithNewPassword1 = () => storage.OpenAsync(NewPassword1);
                Func<Task> openWithNewPassword2 = () => storage.OpenAsync(NewPassword2);
                Func<Task> changeToNewPassword3 = () => storage.ChangePasswordAsync(NewPassword2, NewPassword3);

                Assert.ThrowsAsync<DataBasePasswordException>(openWithOldPassword);
                Assert.ThrowsAsync<DataBasePasswordException>(openWithNewPassword1);
                Assert.DoesNotThrowAsync(openWithNewPassword2);
                Assert.DoesNotThrowAsync(changeToNewPassword3);
            }
        }

        [Test]
        public void MultiplePasswordChangeWithReset()
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

                Assert.DoesNotThrowAsync(createStorage);
                Assert.DoesNotThrowAsync(openWithPassword);
                Assert.DoesNotThrowAsync(changeToNewPassword1);
                Assert.DoesNotThrowAsync(changeToNewPassword2);
                Assert.DoesNotThrowAsync(changeToNewPassword3);

                Assert.DoesNotThrowAsync(openWithNewPassword3);
                Assert.DoesNotThrowAsync(resetStorage);

                // Verify storage file removed and can be recreated again after reset
                Assert.That(DatabaseFileExists(), Is.False);
            }

            using (var storage = GetDataStorage())
            {
                Func<Task> openWithNewPassword3 = () => storage.OpenAsync(NewPassword3);
                Func<Task> createWithNewPassword3 = () => storage.CreateAsync(NewPassword3);

                Assert.ThrowsAsync<DataBaseNotCreatedException>(openWithNewPassword3);
                Assert.DoesNotThrowAsync(createWithNewPassword3);
                Assert.DoesNotThrowAsync(openWithNewPassword3);
            }
        }
    }
}
