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

namespace Tuvi.Core.Entities.Test
{
    public class FolderTests
    {
        [Test]
        public void EqualityTest()
        {
            var folder1 = new Folder("Folder1", FolderAttributes.Inbox);
            var folder2 = new Folder("Folder2", FolderAttributes.Inbox);
            var folder3 = new Folder("Folder1", FolderAttributes.Inbox);
            var folder4 = new Folder("Folder1", FolderAttributes.None);

            Assert.That(folder1, Is.Not.EqualTo(folder2));
            Assert.That(folder3, Is.Not.EqualTo(folder4));
            Assert.That(folder1, Is.Not.EqualTo(folder4));
            Assert.That(new Folder("Folder1", FolderAttributes.Inbox) { AccountEmail = new EmailAddress("address@test.t") },
             Is.Not.EqualTo(new Folder("Folder1", FolderAttributes.Inbox) { AccountEmail = new EmailAddress("address2@test.t") }));


            Assert.That(new Folder("Folder1", FolderAttributes.Inbox) { AccountEmail = new EmailAddress("address@test.t") },
             Is.Not.EqualTo(new Folder("Folder1", FolderAttributes.Draft) { AccountEmail = new EmailAddress("address@test.t") }));
            Assert.That(new Folder("Folder1", FolderAttributes.Inbox) { AccountEmail = new EmailAddress("address@test.t") },
             Is.Not.EqualTo(new Folder("Folder2", FolderAttributes.Inbox) { AccountEmail = new EmailAddress("address@test.t") }));

            Assert.That(folder1, Is.EqualTo(folder1));
            Assert.That(folder1, Is.EqualTo(folder3));
            Assert.That(new Folder("Folder1", FolderAttributes.Inbox) { AccountId = 1, Id = 40 },
             Is.EqualTo(new Folder("Folder1", FolderAttributes.Inbox) { AccountId = 1, Id = 40 }));
        }

    }
}
