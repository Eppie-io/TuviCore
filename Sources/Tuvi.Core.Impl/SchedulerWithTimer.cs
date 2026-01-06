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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tuvi.Core.Entities;
using Tuvi.Core.Logging;

namespace Tuvi.Core.Impl
{
    public class SchedulerWithTimer : IDisposable
    {
        private System.Timers.Timer _timer { get; }

        private readonly object _lock = new object();
        private bool _isBusy { get; set; }
        private Func<CancellationToken, Task> _actionAsync;
        private CancellationTokenSource _actionCancellationSource;
        private Task _currentTask;

        public event EventHandler<ExceptionEventArgs> ExceptionOccurred;

        private double _interval;
        /// <summary>
        /// Gets or sets the interval, expressed in milliseconds, at which to raise the Timer.Elapsed event.
        /// </summary>
        public double Interval
        {
            get => _interval;
            set
            {
                _interval = value;
                Restart();
            }
        }

        private bool _isDisposed;

        /// <summary>
        /// Constructor for scheduler with timer
        /// </summary>
        /// <param name="actionAsync">Asynchronous action to execute when timer ticks</param>
        /// <param name="interval">Initial timer interval in milliseconds</param>
        public SchedulerWithTimer(Func<CancellationToken, Task> actionAsync, double interval)
        {
            _actionAsync = actionAsync;
            _interval = interval;

            _timer = new System.Timers.Timer();
            _timer.Elapsed += (sender, args) => ExecuteAction();
        }

        private async void ExecuteAction()
        {
            _currentTask = ExecuteActionAsync();
            try
            {
                await _currentTask.ConfigureAwait(false);
            }
            finally
            {
                _currentTask = null;
            }
        }

        public Task GetActionTask()
        {
            var task = _currentTask;
            if (task is null)
            {
                return Task.CompletedTask;
            }
            return task;
        }

        private async Task ExecuteActionAsync()
        {
            // If previous action is not finished, ignore this one
            lock (_lock)
            {
                if (_isBusy) return;
                _isBusy = true;
            }

            try
            {
                _actionCancellationSource = new CancellationTokenSource();
                await _actionAsync(_actionCancellationSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                this.Log().LogError(ex, "An error occurred while executing the action");
                ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex));
            }
            finally
            {
                _actionCancellationSource.Dispose();
                _actionCancellationSource = null;
                _isBusy = false;
            }
        }

        /// <summary>
        /// Force execution of the action.
        /// </summary>
        public async Task ExecuteActionForceAsync()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(SchedulerWithTimer));
            }

            await GetActionTask().ConfigureAwait(false);
            await ExecuteActionAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Execute action and start scheduler's timer if Interval is greater than zero
        /// </summary>
        public void Start()
        {
            ExecuteAction();
            StartTimerIfNeeded();
        }

        private void StartTimerIfNeeded()
        {
            if (Interval > 0)
            {
                _timer.Interval = Interval;
                _timer.Start();
            }
        }

        /// <summary>
        /// Stop scheduler's timer
        /// </summary>
        public void Stop()
        {
            _timer.Stop();
        }


        private void Restart()
        {
            Stop();
            StartTimerIfNeeded();
        }

        /// <summary>
        /// Stop timer and cancel all asynchronious actions.
        /// Should be run before Dispose.
        /// </summary>
        public void Cancel()
        {
            Stop();
            _actionCancellationSource?.Cancel();
        }

        /// <summary>
        /// Dispose all resources. Make sure you called Cancel() before.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                _timer.Dispose();
                _actionCancellationSource?.Dispose();
            }

            _isDisposed = true;
        }

        ~SchedulerWithTimer()
        {
            Dispose(false);
        }
    }
}
