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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Impl;
using static Tuvi.Core.Tests.Infrastructure.CoreTestSupport;

namespace Tuvi.Core.Tests
{
    [NonParallelizable]
    public class SchedulerWithTimerTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task ObjectDisposalIsReportedOnlyWhenTheSchedulerWasNotStopping(bool stopping)
        {
            var entered = Signal();
            var release = Signal();
            var failure = new ObjectDisposedException("Unexpected mailbox disposal");
            var notifications = new List<Exception>();
            int runs = 0;
            using var scheduler = new SchedulerWithTimer(async _ =>
            {
                if (Interlocked.Increment(ref runs) != 1) return;
                entered.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                throw failure;
            }, 0);
            scheduler.ExceptionOccurred += (_, args) => notifications.Add(args.Exception);
            var action = scheduler.ExecuteActionForceAsync();
            try
            {
                await entered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                if (stopping) scheduler.Cancel();
                release.TrySetResult(true);
                await action.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(notifications, stopping ? Is.Empty : Is.EqualTo(new[] { failure }));
                await scheduler.ExecuteActionForceAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(runs, Is.EqualTo(stopping ? 1 : 2));
                Assert.That(notifications.Count, Is.EqualTo(stopping ? 0 : 1));
            }
            finally
            {
                release.TrySetResult(true);
                await action.WaitAsync(TestTimeout).ConfigureAwait(false);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task TimerObservesOnlyItsOwnRunAndManualRetryStartsANewRun(bool busyTick)
        {
            var entered = Signal();
            var release = Signal();
            var failure = new IOException("Check failed");
            var subscriberFailure = new InvalidOperationException("Subscriber failed");
            int calls = 0;
            int notifications = 0;
            using var scheduler = new SchedulerWithTimer(async _ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    throw failure;
                }
            }, 0);
            scheduler.ExceptionOccurred += (_, _) =>
            {
                Interlocked.Increment(ref notifications);
                throw subscriberFailure;
            };
            Task manual = null;
            try
            {
                Task tick;
                if (busyTick)
                {
                    manual = scheduler.ExecuteActionForceAsync();
                    await entered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                    var active = scheduler.GetActionTask();
                    tick = Tick(scheduler);
                    await tick.WaitAsync(TestTimeout).ConfigureAwait(false);
                    Assert.That(scheduler.GetActionTask(), Is.SameAs(active));
                    Assert.That(active.IsCompleted, Is.False);
                }
                else
                {
                    tick = Tick(scheduler);
                    await entered.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                }
                release.TrySetResult(true);
                if (manual != null)
                {
                    var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                        await manual.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                    Assert.That(actual, Is.SameAs(subscriberFailure));
                }
                await tick.WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(notifications, Is.EqualTo(1));
                await scheduler.ExecuteActionForceAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.That(calls, Is.EqualTo(2));
                Assert.That(notifications, Is.EqualTo(1));
            }
            finally
            {
                release.TrySetResult(true);
                if (manual != null)
                {
                    await Assert.CatchAsync<Exception>(async () =>
                        await manual.WaitAsync(TestTimeout).ConfigureAwait(false)).ConfigureAwait(false);
                }
            }
        }
    }
}
