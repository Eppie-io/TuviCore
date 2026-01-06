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
                Assert.ThrowsAsync<DataBaseNotCreatedException>(() => storage.OpenAsync(Password));
                Assert.DoesNotThrowAsync(() => storage.CreateAsync(Password));
            }
        }

        [Test]
        public void StorageExist()
        {
            using (var storage = GetDataStorage())
            {
                Assert.DoesNotThrowAsync(() => storage.CreateAsync(Password));
                Assert.ThrowsAsync<DataBaseAlreadyExistsException>(() => storage.CreateAsync(Password));
                Assert.DoesNotThrowAsync(() => storage.OpenAsync(Password));
            }
        }

        [Test]
        public void OpenAndSetPassword()
        {
            using (var storage = GetDataStorage())
            {
                Assert.DoesNotThrowAsync(() => storage.CreateAsync(Password));
                Assert.DoesNotThrowAsync(() => storage.OpenAsync(Password));
            }
        }

        [Test]
        public void OpenWithCorrectPassword()
        {
            OpenAndSetPassword();

            using (var storage = GetDataStorage())
            {
                Assert.DoesNotThrowAsync(() => storage.OpenAsync(Password));
            }
        }

        [Test]
        public void OpenWithIncorrectPassword()
        {
            OpenAndSetPassword();

            using (var storage = GetDataStorage())
            {
                Assert.ThrowsAsync<DataBasePasswordException>(() => storage.OpenAsync(IncorrectPassword));
            }
        }

        [Test]
        public void ChangePassword()
        {
            using (var storage = GetDataStorage())
            {
                Assert.DoesNotThrowAsync(() => storage.CreateAsync(Password));
            }


            using (var storage = GetDataStorage())
            {
                Assert.DoesNotThrowAsync(() => storage.ChangePasswordAsync(Password, NewPassword));
            }

            using (var storage = GetDataStorage())
            {
                Assert.DoesNotThrowAsync(() => storage.OpenAsync(NewPassword));
            }

            using (var storage = GetDataStorage())
            {
                Assert.ThrowsAsync<DataBasePasswordException>(() => storage.OpenAsync(Password));
                Assert.DoesNotThrowAsync(() => storage.OpenAsync(NewPassword));
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
                Assert.DoesNotThrowAsync(() => storage.CreateAsync(Password));
            }

            using (var storage = GetDataStorage())
            {
                Assert.DoesNotThrowAsync(() => storage.ChangePasswordAsync(Password, NewPassword1));
            }

            using (var storage = GetDataStorage())
            {
                Assert.ThrowsAsync<DataBasePasswordException>(() => storage.OpenAsync(Password));
                Assert.DoesNotThrowAsync(() => storage.OpenAsync(NewPassword1));
                Assert.DoesNotThrowAsync(() => storage.ChangePasswordAsync(NewPassword1, NewPassword2));
            }

            using (var storage = GetDataStorage())
            {
                Assert.ThrowsAsync<DataBasePasswordException>(() => storage.OpenAsync(Password));
                Assert.ThrowsAsync<DataBasePasswordException>(() => storage.OpenAsync(NewPassword1));
                Assert.DoesNotThrowAsync(() => storage.OpenAsync(NewPassword2));
                Assert.DoesNotThrowAsync(() => storage.ChangePasswordAsync(NewPassword2, NewPassword3));
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
                Assert.DoesNotThrowAsync(() => storage.CreateAsync(Password));
                Assert.DoesNotThrowAsync(() => storage.OpenAsync(Password));
                Assert.DoesNotThrowAsync(() => storage.ChangePasswordAsync(Password, NewPassword1));
                Assert.DoesNotThrowAsync(() => storage.ChangePasswordAsync(NewPassword1, NewPassword2));
                Assert.DoesNotThrowAsync(() => storage.ChangePasswordAsync(NewPassword2, NewPassword3));

                Assert.DoesNotThrowAsync(() => storage.OpenAsync(NewPassword3));
                Assert.DoesNotThrowAsync(() => storage.ResetAsync());

                // Verify storage file removed and can be recreated again after reset
                Assert.That(DatabaseFileExists(), Is.False);
            }

            using (var storage = GetDataStorage())
            {
                Assert.ThrowsAsync<DataBaseNotCreatedException>(() => storage.OpenAsync(NewPassword3));
                Assert.DoesNotThrowAsync(() => storage.CreateAsync(NewPassword3));
                Assert.DoesNotThrowAsync(() => storage.OpenAsync(NewPassword3));
            }
        }
    }
}
