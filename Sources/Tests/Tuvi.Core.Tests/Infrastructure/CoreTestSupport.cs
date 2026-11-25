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
using Tuvi.Core.Entities;
using Tuvi.Core.Impl;

namespace Tuvi.Core.Tests.Infrastructure
{
    internal static class CoreTestSupport
    {
        internal static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(20);

        internal static TaskCompletionSource<bool> Signal()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal static Account CreateAccount(string name = "account")
        {
            var account = CoreTestBase.GetTestEmailAccount(new EmailAddress(name + "@example.test"));
            account.SynchronizationInterval = 0;
            account.FoldersStructure.RemoveAt(1);
            account.DefaultInboxFolder.Account = account;
            return account;
        }

        internal static SchedulerWithTimer Scheduler(TuviMail core, Account account)
        {
            return core.GetAccountScheduler(account.Email);
        }

        internal static int OwnedCount(TuviMail core)
        {
            return core.OwnedSchedulerCount;
        }

        internal static Task Tick(SchedulerWithTimer scheduler)
        {
            return scheduler.ExecuteAction();
        }

    }
}
