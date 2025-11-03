using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace VRChatAutoFishing
{
    class AutoFisher : IDisposable
    {
        // Delegates definations
        public delegate void OnUpdateStatusHandler(string text);
        public delegate void OnNotifyHandler(string message);
        public delegate void OnCriticalErrorHandler(string errorMessage);

        // Note: The handlers will not dispatch on the UI thread.
        public OnUpdateStatusHandler? OnUpdateStatus;
        public OnNotifyHandler? OnNotify;
        public OnCriticalErrorHandler? OnCriticalError;

        private enum ActionState
        {
            kIdle = 0,
            kPreparing,
            kStartToCast,
            kCasting,
            kWaitForFish,
            kReeling,
            kFinishedReel,
            kStopped,
            kReCasting,
            kReReeling,
            // Exceptions
            kTimeoutReelSingle,
            kTimeoutReel,
        }

        private CancellationTokenSource _cts = new();
        private readonly SingleThreadSynchronizationContext _context = new("AutoFisherWorkerThread");

        private bool _isRunning = false;
        private ActionState _currentAction = ActionState.kIdle;
        private DateTime _lastCycleEnd;
        private DateTime _last_castTime;
        private readonly System.Timers.Timer _timeoutTimer;
        private readonly System.Timers.Timer _statusDisplayTimer;
        private readonly System.Timers.Timer _reelBackTimer;

        private readonly OSCClient _oscClient;
        private readonly VRChatLogMonitor _logMonitor;
        private bool _firstCast = true;

        public double CastTime { get; set; }
        private const double TIMEOUT_MINUTES = 3.0;

        // 钓鱼统计相关变量
        private int _fishCount = 0;
        private bool _showingFishCount = false;
        private DateTime _lastStatusSwitchTime = DateTime.Now;

        // 收杆状态跟踪
        private int _savedDataCount = 0;
        private DateTime _firstSavedDataTime;

        // 特殊抛竿相关变量
        private double _actual_castTime = 0;
        private double _reelBackTime = 0;

        public AutoFisher(string ip, int port, double initial_castTime)
        {
            CastTime = initial_castTime;
            _oscClient = new OSCClient(ip, port);
            _logMonitor = new VRChatLogMonitor(
                () => _context.Post(_ => FishOnHook(), null),
                () => _context.Post(_ => OnFishPickupDetected(), null)
            );

            _timeoutTimer = new System.Timers.Timer { AutoReset = false, SynchronizingObject = _context };
            _timeoutTimer.Elapsed += HandleTimeout;

            _statusDisplayTimer = new System.Timers.Timer { Interval = 100, AutoReset = true, SynchronizingObject = _context };
            _statusDisplayTimer.Elapsed += UpdateStatusDisplay;

            _reelBackTimer = new System.Timers.Timer { AutoReset = false, SynchronizingObject = _context };
            _reelBackTimer.Elapsed += PerformReelBack;

            _lastCycleEnd = DateTime.Now;
            _last_castTime = DateTime.MinValue;

            // Ensure click is released
            SendClick(false);
        }

        // Asynchronously start
        public void Start()
        {
            _context.Post(_ =>
            {
                if (_isRunning) return;
                _isRunning = true;
                _logMonitor.StartMonitoring();
                _statusDisplayTimer.Start();
                _fishCount = 0;
                _firstCast = true;
                PerformCast();
            }, null);
        }

        // Synchronously stop
        public void Stop()
        {
            if (_cts.IsCancellationRequested)
                return;
            _cts.Cancel(); // Signal cancellation to all operations
            _context.Stop();
            _logMonitor.StopMonitoring();
            _statusDisplayTimer.Stop();
            _timeoutTimer.Stop();
            _reelBackTimer.Stop();
            SendClick(false); // Ensure click is released
            UpdateStatusText(ActionState.kStopped);
        }

        public void Dispose()
        {
            Stop();
            _context.Dispose();
            _oscClient.Dispose();
            _timeoutTimer.Dispose();
            _statusDisplayTimer.Dispose();
            _reelBackTimer.Dispose();
        }

        private void UpdateStatusDisplay(object? sender, ElapsedEventArgs e)
        {
            if (_cts.IsCancellationRequested) return;

            if (_currentAction == ActionState.kWaitForFish)
            {
                double elapsedSeconds = (DateTime.Now - _lastStatusSwitchTime).TotalSeconds;

                if (_showingFishCount)
                {
                    if (elapsedSeconds >= 2.0)
                    {
                        _showingFishCount = false;
                        _lastStatusSwitchTime = DateTime.Now;
                        UpdateStatusText(ActionState.kWaitForFish);
                    }
                }
                else
                {
                    if (elapsedSeconds >= 5.0)
                    {
                        _showingFishCount = true;
                        _lastStatusSwitchTime = DateTime.Now;
                        UpdateStatusText($"已钓:{_fishCount}");
                    }
                }
            }
        }

        // To display simple text status
        private void UpdateStatusText(string text)
        {
            OnUpdateStatus?.Invoke(text);
        }

        // This will also update _currentAction
        private void UpdateStatusText(ActionState state)
        {
            _currentAction = state;
            UpdateStatusText(
                state switch
                {
                    ActionState.kIdle => "空闲",
                    ActionState.kPreparing => "准备中",
                    ActionState.kStartToCast => "开始抛竿",
                    ActionState.kCasting => "抛竿中",
                    ActionState.kWaitForFish => "等待鱼上钩",
                    ActionState.kReeling => "收杆中",
                    ActionState.kFinishedReel => "收杆完成",
                    ActionState.kStopped => "已停止",
                    ActionState.kReCasting => "重新抛竿",
                    ActionState.kReReeling => "重新收杆",
                    ActionState.kTimeoutReelSingle => "收杆超时(单次)",
                    ActionState.kTimeoutReel => "收杆超时",
                    _ => "未知状态",
                }
            );
        }

        private void SendClick(bool press)
        {
            _oscClient.SendUseRight(press ? 1 : 0);
        }

        private void StartTimeoutTimer()
        {
            _timeoutTimer.Stop();
            _timeoutTimer.Interval = TIMEOUT_MINUTES * 60 * 1000;
            _timeoutTimer.Start();
        }

        private void HandleTimeout(object? sender, ElapsedEventArgs e)
        {
            if (_currentAction != ActionState.kWaitForFish || _cts.IsCancellationRequested) return;

            UpdateStatusText(ActionState.kTimeoutReel);
            PerformTimeoutReel();
        }

        private void PerformTimeoutReel()
        {
            var token = _cts.Token;
            if (token.IsCancellationRequested) return;

            UpdateStatusText(ActionState.kReCasting);
            SendClick(true);
            token.WaitHandle.WaitOne(2000);
            SendClick(false);
            if (token.IsCancellationRequested) return;

            UpdateStatusText(ActionState.kReReeling);
            SendClick(true);
            token.WaitHandle.WaitOne(20000);
            SendClick(false);
            if (token.IsCancellationRequested) return;

            OnNotify?.Invoke("钓鱼超时，正在重试！如果此事件持续，请检查游戏状态。");
            PerformCast();
        }

        private void PerformReel()
        {
            var token = _cts.Token;
            if (token.IsCancellationRequested) return;

            UpdateStatusText(ActionState.kReeling);
            SendClick(true);

            _savedDataCount = 0;
            _firstSavedDataTime = DateTime.MinValue;

            DateTime startTime = DateTime.Now;
            bool secondSavedDataDetected = false;

            while ((DateTime.Now - startTime).TotalSeconds < 30)
            {
                if (token.IsCancellationRequested) return;

                string content = _logMonitor.ReadNewContent();
                if (content.Contains("SAVED DATA"))
                {
                    if (_savedDataCount == 0)
                    {
                        _savedDataCount = 1;
                        _firstSavedDataTime = DateTime.Now;
                    }
                    else if (_savedDataCount == 1)
                    {
                        double interval = (DateTime.Now - _firstSavedDataTime).TotalSeconds;
                        if (interval >= 1.0)
                        {
                            _savedDataCount = 2;
                            secondSavedDataDetected = true;
                            break;
                        }
                    }
                }

                if (_savedDataCount == 1 && (DateTime.Now - _firstSavedDataTime).TotalSeconds > 10)
                {
                    break;
                }
                token.WaitHandle.WaitOne(100);
            }

            SendClick(false);

            if (token.IsCancellationRequested) return;

            if (secondSavedDataDetected)
            {
                UpdateStatusText(ActionState.kFinishedReel);
                _fishCount++;
            }
            else if (_savedDataCount == 1)
            {
                UpdateStatusText(ActionState.kTimeoutReelSingle);
            }
            else
            {
                UpdateStatusText(ActionState.kTimeoutReel);
                OnNotify?.Invoke("鱼已上钩但收杆仍超时！如果此事件持续，请检查游戏状态。");
            }
        }

        private void PerformReelBack(object? sender, ElapsedEventArgs e)
        {
            var token = _cts.Token;
            if (token.IsCancellationRequested) return;
            SendClick(true);
            token.WaitHandle.WaitOne((int)(_reelBackTime * 1000));
            SendClick(false);
        }

        private void OnFishPickupDetected()
        {
            if (_currentAction != ActionState.kWaitForFish || _cts.IsCancellationRequested) return;
            if ((DateTime.Now - _lastCycleEnd).TotalSeconds < 2) return;
            FishOnHook();
        }

        // Don't call this in a separate timer directly to avoid dead loop in task queue
        private void PerformCast()
        {
            var token = _cts.Token;
            if (token.IsCancellationRequested) return;

            if (!_firstCast)
            {
                UpdateStatusText(ActionState.kPreparing);
                token.WaitHandle.WaitOne(500);
                if (token.IsCancellationRequested) return;
            }
            else
            {
                _firstCast = false;
            }

            UpdateStatusText(ActionState.kCasting);

            double castDuration = CastTime;

            if (castDuration < 0.2)
            {
                _actual_castTime = 0.2;
                _reelBackTime = (castDuration < 0.1) ? 0.5 : 0.3;

                SendClick(true);
                token.WaitHandle.WaitOne((int)(_actual_castTime * 1000));
                SendClick(false);

                if (!token.IsCancellationRequested)
                {
                    _reelBackTimer.Interval = 1000;
                    _reelBackTimer.Start();
                    StartTimeoutTimerAfterReelBack();
                }
            }
            else
            {
                SendClick(true);
                token.WaitHandle.WaitOne((int)(castDuration * 1000));
                SendClick(false);

                if (!token.IsCancellationRequested)
                {
                    StartTimeoutTimer();
                }
            }

            if (!token.IsCancellationRequested)
            {
                UpdateStatusText(ActionState.kWaitForFish);
                _lastStatusSwitchTime = DateTime.Now;
                _last_castTime = DateTime.Now;
            }
        }

        private void StartTimeoutTimerAfterReelBack()
        {
            double totalWaitTime = 1000 + (_reelBackTime * 1000) + 100;
            var delayTimer = new System.Timers.Timer(totalWaitTime)
            {
                AutoReset = false,
                SynchronizingObject = _context
            };
            delayTimer.Elapsed += (s, e) =>
            {
                if (!_cts.IsCancellationRequested)
                {
                    StartTimeoutTimer();
                }
                delayTimer.Dispose();
            };
            delayTimer.Start();
        }

        private void FishOnHook()
        {
            if (_currentAction != ActionState.kWaitForFish || _cts.IsCancellationRequested) return;
            if ((DateTime.Now - _last_castTime).TotalSeconds < 3.0) return;
            if ((DateTime.Now - _lastCycleEnd).TotalSeconds < 2) return;

            _timeoutTimer.Stop();
            _lastCycleEnd = DateTime.Now;

            PerformReel();

            if (!_cts.IsCancellationRequested)
            {
                PerformCast();
            }
        }
    }
}
