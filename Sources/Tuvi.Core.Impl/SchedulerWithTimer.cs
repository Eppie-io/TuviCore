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
        private static readonly AsyncLocal<CancellationTokenSource> _executingAction = new AsyncLocal<CancellationTokenSource>();
        [ThreadStatic]
        private static CancellationScope _cancelling;
        private bool _isBusy { get; set; }
        private Func<CancellationToken, Task> _actionAsync;
        private CancellationTokenSource _actionCancellationSource;
        private Task _currentTask;
        private Task _stoppedAction;
        private int _cancellationCalls;

        private sealed class CancellationScope
        {
            public readonly SchedulerWithTimer Scheduler;
            public readonly CancellationScope Previous;

            public CancellationScope(SchedulerWithTimer scheduler, CancellationScope previous)
            {
                Scheduler = scheduler;
                Previous = previous;
            }
        }

        public event EventHandler<ExceptionEventArgs> ExceptionOccurred;

        internal event EventHandler Disposed
        {
            add { _timer.Disposed += value; }
            remove { _timer.Disposed -= value; }
        }

        private double _interval;
        /// <summary>
        /// Gets or sets the interval, expressed in milliseconds, at which to raise the Timer.Elapsed event.
        /// </summary>
        public double Interval
        {
            get { lock (_lock) { return _interval; } }
            set
            {
                lock (_lock)
                {
                    if (_isDisposed) throw new ObjectDisposedException(nameof(SchedulerWithTimer));
                    _interval = value;
                    if (!_isStopping)
                    {
                        _timer.Stop();
                        StartTimerIfNeeded();
                    }
                }
            }
        }

        private bool _isDisposed;
        private bool _isStopping;

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
            _timer.Elapsed += (sender, args) => _ = ExecuteAction();
        }

        internal async Task ExecuteAction()
        {
            var action = StartTrackedAction(skipIfBusy: true);
            if (action == null) return;
            try
            {
                await action.ConfigureAwait(false);
            }
#pragma warning disable CA1031 // A timer observes failures from the run it started.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                this.Log().LogError(ex, "An error occurred in the scheduled check's exception handler");
            }
        }

        private Task StartTrackedAction(bool skipIfBusy)
        {
            lock (_lock)
            {
                if (_isDisposed || _isStopping) return null;
                if (_currentTask != null && !_currentTask.IsCompleted)
                {
                    return skipIfBusy ? null : _currentTask;
                }
                _isBusy = true;
                _actionCancellationSource = new CancellationTokenSource();
                var cancellation = _actionCancellationSource;
                _currentTask = Task.Run(() => ExecuteActionAsync(cancellation));
                return _currentTask;
            }
        }

        public Task GetActionTask()
        {
            lock (_lock) { return _currentTask ?? Task.CompletedTask; }
        }

        internal bool IsExecutingAction
        {
            get
            {
                lock (_lock)
                {
                    return _isBusy && _executingAction.Value != null && ReferenceEquals(_executingAction.Value, _actionCancellationSource);
                }
            }
        }

        internal bool IsExecutingCancellation
        {
            get
            {
                // Cancellation callbacks may restore an older ExecutionContext after the action ended.
                for (var scope = _cancelling; scope != null; scope = scope.Previous)
                {
                    if (ReferenceEquals(scope.Scheduler, this)) return true;
                }
                lock (_lock)
                {
                    return _cancellationCalls != 0 && _executingAction.Value != null &&
                        ReferenceEquals(_executingAction.Value, _actionCancellationSource);
                }
            }
        }

        private async Task ExecuteActionAsync(CancellationTokenSource cancellation)
        {
            _executingAction.Value = cancellation;
            try
            {
                var cancellationToken = cancellation.Token;
                cancellationToken.ThrowIfCancellationRequested();
                await _actionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (DataBaseResetException)
            {
                // Storage reset interrupted the scheduled action.
            }
            catch (ObjectDisposedException) when (IsStopping())
            {
                // Core disposal interrupted the scheduled action.
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
                _executingAction.Value = null;
                lock (_lock)
                {
                    _isBusy = false;
                    if (!_isStopping)
                    {
                        cancellation.Dispose();
                        _actionCancellationSource = null;
                    }
                }
            }
        }

        internal bool IsStopping()
        {
            lock (_lock) { return _isStopping; }
        }

        /// <summary>
        /// Force execution of the action.
        /// </summary>
        public async Task ExecuteActionForceAsync()
        {
            Task previous;
            lock (_lock)
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(SchedulerWithTimer));
                if (_isStopping) return;
                previous = _currentTask != null && !_currentTask.IsCompleted ? _currentTask : null;
            }
            if (previous != null)
            {
                try
                {
                    await previous.ConfigureAwait(false);
                }
#pragma warning disable CA1031 // The preceding run belongs to its caller or timer.
                catch (Exception)
#pragma warning restore CA1031
                {
                    // This call may start a new attempt after the preceding run failed.
                }
            }
            var action = StartTrackedAction(skipIfBusy: false);
            if (action != null)
            {
                await action.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Execute action and start scheduler's timer if Interval is greater than zero
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(SchedulerWithTimer));
                if (_isStopping) return;
                StartTimerIfNeeded();
            }
            _ = ExecuteAction();
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
            lock (_lock)
            {
                if (!_isDisposed) _timer.Stop();
            }
        }

        internal virtual Task PrepareStop()
        {
            lock (_lock)
            {
                if (!_isStopping)
                {
                    _isStopping = true;
                    if (!_isDisposed) _timer.Stop();
                    _stoppedAction = _currentTask != null && !_currentTask.IsCompleted ? _currentTask : Task.CompletedTask;
                }
                return _stoppedAction ?? Task.CompletedTask;
            }
        }

        /// <summary>
        /// Permanently stop admission and cancel the current action.
        /// Await GetActionTask after cancellation returns, then call Dispose.
        /// </summary>
        public void Cancel()
        {
            CancellationTokenSource cancellation;
            lock (_lock)
            {
                if (_isDisposed) return;
                PrepareStop();
                cancellation = _actionCancellationSource;
                ++_cancellationCalls;
            }
            var previous = _cancelling;
            _cancelling = new CancellationScope(this, previous);
            try { cancellation?.Cancel(); }
            catch (ObjectDisposedException) { /* The tracked action already completed. */ }
            finally
            {
                _cancelling = previous;
                lock (_lock) { --_cancellationCalls; }
            }
        }

        /// <summary>
        /// Dispose resources after Cancel has returned and the action has completed.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (_lock)
            {
                if (_isDisposed) return;

                if (disposing)
                {
                    _timer.Dispose();
                    _actionCancellationSource?.Dispose();
                }

                _isDisposed = true;
                _isStopping = true;
            }
        }

        ~SchedulerWithTimer()
        {
            Dispose(false);
        }
    }
}
