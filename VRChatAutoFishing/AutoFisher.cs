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
        public delegate void OnFishCaughtHandler(int totalFishCaught);

        // Note: The handlers will not dispatch on the UI thread.
        public OnUpdateStatusHandler? OnUpdateStatus;
        public OnNotifyHandler? OnNotify;
        public OnCriticalErrorHandler? OnCriticalError;
        public OnFishCaughtHandler? OnFishCaught;

        private enum ActionState
        {
            kIdle = 0,
            kPreparing,
            kStartToCast,
            kCasting,
            kWaitForFish,
            kReeling,
            kReelingHasGotOutOfWater,
            kFinishedReel,
            kStopped,
            kReCasting,
            kReReeling,
            // Exceptions
            kTimeoutReel,
            kDistrubed,
        }

        private CancellationTokenSource _cts = new();
        private readonly SingleThreadSynchronizationContext _context = new("AutoFisherWorkerThread");

        private bool _alreadyRunning = false;
        private ActionState _currentAction = ActionState.kIdle;
        //private DateTime _lastCycleEnd;
        private DateTime _last_castTime;
        private DateTime _lastDisabledCastFishOnHook = DateTime.MinValue;
        private readonly System.Timers.Timer _timeoutTimer;
        private readonly System.Timers.Timer _statusDisplayTimer;
        private readonly System.Timers.Timer _reelBackTimer;
        private readonly System.Timers.Timer _reelTimeoutTimer;
        private readonly System.Timers.Timer _disabledCastReleaseTimer;

        private readonly OSCClient _oscClient;
        private readonly VRChatLogMonitor _logMonitor;
        private bool _firstCast = true;

        private double _castTime;
        public const double kDisabledCastTime = -1.0;
        public double CastTime
        {
            get { return _castTime; }
            set
            {
                // Cannot set to disabled during operation
                if (value == kDisabledCastTime)
                    return;
                _castTime = value;
            }
        }

        private const double TIMEOUT_MINUTES = 3.0;
        private const int MAX_REEL_TIME_SECONDS = 30;

        // 钓鱼统计相关变量
        private int _fishCount = 0;
        private bool _showingFishCount = false;
        private DateTime _lastStatusSwitchTime = DateTime.Now;

        // 特殊抛竿相关变量
        private double _actual_castTime = 0;
        private double _reelBackTime = 0;

        public AutoFisher(string ip, int port, double? initial_castTime)
        {
            _castTime = initial_castTime ?? kDisabledCastTime;
            _oscClient = new OSCClient(ip, port);
            _logMonitor = new VRChatLogMonitor(
                () => _context.Post(_ => FishOnHook(), null),
                () => _context.Post(_ => FishGotOut(), null)
            );

            _timeoutTimer = new System.Timers.Timer { AutoReset = false, SynchronizingObject = _context };
            _timeoutTimer.Elapsed += HandleTimeout;

            _statusDisplayTimer = new System.Timers.Timer { Interval = 100, AutoReset = true, SynchronizingObject = _context };
            _statusDisplayTimer.Elapsed += UpdateStatusDisplay;

            _reelBackTimer = new System.Timers.Timer { AutoReset = false, SynchronizingObject = _context };
            _reelBackTimer.Elapsed += PerformReelBack;

            _reelTimeoutTimer = new System.Timers.Timer { AutoReset = false, SynchronizingObject = _context };
            _reelTimeoutTimer.Elapsed += PerformReelingTimeout;

            _disabledCastReleaseTimer = new System.Timers.Timer { Interval = 30000, AutoReset = false, SynchronizingObject = _context };
            _disabledCastReleaseTimer.Elapsed += DisabledCastReleaseTimerElapsed;

            //_lastCycleEnd = DateTime.Now;
            _last_castTime = DateTime.MinValue;

            // Ensure click is released
            SendClick(false);
        }

        // Asynchronously start
        public void Start()
        {
            _context.Post(_ =>
            {
                if (_alreadyRunning) return;
                _alreadyRunning = true;
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
            _reelTimeoutTimer.Stop();
            _disabledCastReleaseTimer.Stop();
            SendClick(false); // Ensure click is released
            UpdateStatusText(ActionState.kStopped);
        }

        public void Dispose()
        {
            Stop();
            _context.Dispose();
            _cts.Dispose();
            _oscClient.Dispose();
            _timeoutTimer.Dispose();
            _statusDisplayTimer.Dispose();
            _reelBackTimer.Dispose();
            _reelTimeoutTimer.Dispose();
            _disabledCastReleaseTimer.Dispose();
        }

        private void UpdateStatusDisplay(object? sender, ElapsedEventArgs e)
        {
            var token = _cts.Token;
            if (token.IsCancellationRequested) return;

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
        private void UpdateStatusText(string text) => OnUpdateStatus?.Invoke(text);

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
                    ActionState.kReelingHasGotOutOfWater => "抄鱼中",
                    ActionState.kFinishedReel => "鱼+1",
                    ActionState.kStopped => "已停止",
                    ActionState.kReCasting => "重新抛竿",
                    ActionState.kReReeling => "重新收杆",
                    ActionState.kTimeoutReel => "收杆超时",
                    ActionState.kDistrubed => "被打断",
                    _ => "未知状态",
                }
            );
        }

        private void SendClick(bool press)
        {
            _oscClient.SendUseRight(press ? 1 : 0);
        }

        private void PressForDuration(int ms)
        {
            SendClick(true);
            _cts.Token.WaitHandle.WaitOne(ms);
            SendClick(false);
        }

        private void ReleaseForDuration(int ms)
        {
            SendClick(false);
            _cts.Token.WaitHandle.WaitOne(ms);
            SendClick(true);
        }

        private void HandleTimeout(object? sender, ElapsedEventArgs e)
        {
            var token = _cts.Token;
            if (_currentAction != ActionState.kWaitForFish || token.IsCancellationRequested) return;

            UpdateStatusText(ActionState.kTimeoutReel);
            PerformTimeoutReel();
        }

        private void PerformTimeoutReel()
        {
            var token = _cts.Token;
            if (token.IsCancellationRequested) return;

            // 重新抛竿并收杆，确保可以回到正常位置
            UpdateStatusText(ActionState.kReCasting);
            PressForDuration(2000);
            if (token.IsCancellationRequested) return;

            UpdateStatusText(ActionState.kReReeling);
            PressForDuration(20000);
            if (token.IsCancellationRequested) return;

            OnNotify?.Invoke("钓鱼超时，正在重试！如果此事件持续，请检查游戏状态。");
            PerformCast();
        }

        private void PerformReelingTimeout(object? sender, ElapsedEventArgs e)
        {
            // stop reeling
            SendClick(false);
            var token = _cts.Token;
            if (token.IsCancellationRequested) return;

            if (_currentAction == ActionState.kReeling)
            {
                Console.WriteLine("Reeling Timeout, Casting again");
                // No fish caught. We are distrubed by the unexpected log.
                UpdateStatusText(ActionState.kDistrubed);
                // Perform next cast immediately
                PerformCast();
            }
            else if (_currentAction == ActionState.kReelingHasGotOutOfWater)
            {
                Console.WriteLine("Reeling Timeout after out of water");
                PerformTimeoutReel();
            }
            else if (_currentAction == ActionState.kWaitForFish)
            {
                // 上次入桶后没有加经验，视为假入桶，继续收杆
                Console.WriteLine("Fake in bucket! Reset to OutOfWater");
                _fishCount--;
                OnFishCaught?.Invoke(_fishCount);
                SendClick(true);
                _reelBackTimer.Stop(); // 可能上次抛竿是短抛竿，停止回拉计时器，防止该函数退出后触发
                _reelTimeoutTimer.Interval = MAX_REEL_TIME_SECONDS * 1000;
                _reelTimeoutTimer.Start();
                UpdateStatusText(ActionState.kReelingHasGotOutOfWater);
            }
            else
            {
                Console.WriteLine("Reeling Timeout: Unexpected State!");
                OnCriticalError?.Invoke("收杆超时但状态异常！请检查程序状态。");
            }
        }

        private void PerformReelBack(object? sender, ElapsedEventArgs e)
        {
            if (_currentAction != ActionState.kWaitForFish) return;
            PressForDuration((int)(_reelBackTime * 1000));
        }

        // Don't call this in a separate timer directly to avoid dead loop in task queue
        private void PerformCast()
        {
            var token = _cts.Token;
            if (token.IsCancellationRequested) return;
            Console.WriteLine("PerformCast");

            if (!_firstCast)
            {
                UpdateStatusText(ActionState.kPreparing);
                token.WaitHandle.WaitOne(500);
                if (token.IsCancellationRequested) return;
            }
            else
            {
                _firstCast = false;
                if (_castTime == kDisabledCastTime)
                {
                    Console.WriteLine("CastTime is disabled, first cast should be SendClick");
                    SendClick(true);
                    UpdateStatusText(ActionState.kWaitForFish);
                    return;
                }
            }

            UpdateStatusText(ActionState.kCasting);

            double castDuration = _castTime;

            if (castDuration < 0.2)
            {
                _actual_castTime = 0.2;
                _reelBackTime = (castDuration < 0.1) ? 0.5 : 0.3;

                PressForDuration((int)(_actual_castTime * 1000));
                if (token.IsCancellationRequested) return;

                _reelBackTimer.Interval = 1000;
                _reelBackTimer.Start();
                // Delay starting the timeout timer until after the reel back action is complete.
                double delay = 1000 + (_reelBackTime * 1000) + 100;
                StartTimeoutTimer(delay);
            }
            else
            {
                PressForDuration((int)(castDuration * 1000));
                if (token.IsCancellationRequested) return;
                StartTimeoutTimer();
            }

            if (token.IsCancellationRequested) return;
            UpdateStatusText(ActionState.kWaitForFish);
            _lastStatusSwitchTime = DateTime.Now;
            _last_castTime = DateTime.Now;

        }

        private void StartTimeoutTimer(double delay = 0)
        {
            _timeoutTimer.Stop();
            double interval = TIMEOUT_MINUTES * 60 * 1000;
            if (delay > 0)
            {
                interval += delay;
            }
            _timeoutTimer.Interval = Math.Max(1, interval);
            _timeoutTimer.Start();
        }

        private void FishOnHook()
        {
            var token = _cts.Token;
            if (token.IsCancellationRequested) return;
            if (_castTime == kDisabledCastTime)
            {
                // 防抖: 3秒内不重复处理
                if ((DateTime.Now - _lastDisabledCastFishOnHook).TotalSeconds < 3.0)
                {
                    Console.WriteLine("FishOnHook: disabled cast, debounced (within 3s)");
                    return;
                }
                Console.WriteLine("FishOnHook: disabled cast, just release for a while");
                _lastDisabledCastFishOnHook = DateTime.Now;
                ReleaseForDuration(50);
                // 重置计时器，这个计时器的作用是兜底，确保30s有一次收杆
                // 为什么是30s？因为根据实测，不抛竿钓鱼效率平均在33s左右每条，其中5~12s是收杆时间，实际等待时间是20s左右，以30s作为兜底是合理的
                _disabledCastReleaseTimer.Stop();
                _disabledCastReleaseTimer.Start();
                return;
            }
            Console.WriteLine("FishOnHook");
            if ((DateTime.Now - _last_castTime).TotalSeconds < 3.0)
            {
                // 抛竿后3秒内上钩，视为分数统计保存事件，此时可知上次钓鱼是真的入了桶
                if (_currentAction == ActionState.kWaitForFish)
                {
                    Console.WriteLine("Last fish is OK");
                    _reelTimeoutTimer.Stop();
                }
                return;
            }
            //if ((DateTime.Now - _lastCycleEnd).TotalSeconds < 2) return;

            _timeoutTimer.Stop();
            //_lastCycleEnd = DateTime.Now;

            if (_currentAction == ActionState.kWaitForFish)
            {
                Console.WriteLine("Start to reel");
                UpdateStatusText(ActionState.kReeling);
                SendClick(true); // We do Reeling until we got fish out of water or timeout
                _reelTimeoutTimer.Stop();
                _reelTimeoutTimer.Interval = MAX_REEL_TIME_SECONDS * 1000;
                _reelTimeoutTimer.Start();
                return;
            }
            if (_currentAction == ActionState.kReelingHasGotOutOfWater)
            {
                Console.WriteLine("(Assume) Fish in bucket");
                _reelTimeoutTimer.Stop();
                SendClick(false);
                UpdateStatusText(ActionState.kFinishedReel);
                ++_fishCount;
                OnFishCaught?.Invoke(_fishCount);
                // 不等XP了，直接下一杆
                DateTime timeBeforCast = DateTime.Now;
                PerformCast();
                TimeSpan timeElapsed = DateTime.Now - timeBeforCast;
                // 如果我们错了，那么等于我们放开了 500ms 然后又拉了至少200ms
                // 此时再放一会，应该不至于逃脱（如果逃脱，那么重新收杆会失败，走抛竿程序）
                _reelTimeoutTimer.Interval = double.Max(100, 3000.0 - timeElapsed.TotalMilliseconds);
                _reelTimeoutTimer.Start();
                return;
            }

            Console.WriteLine("FishOnHook: Unexpected State!");
            //PerformCast();
        }

        private void FishGotOut()
        {
            var token = _cts.Token;
            if (token.IsCancellationRequested) return;
            if (_castTime == kDisabledCastTime)
            {
                Console.WriteLine("FishGotOut: disabled cast, treat as got fish");
                ++_fishCount;
                OnFishCaught?.Invoke(_fishCount);
                return;
            }
            Console.WriteLine("FishGotOut");
            if (_currentAction == ActionState.kReeling)
            {
                Console.WriteLine("Fish got out of water during reeling");
                UpdateStatusText(ActionState.kReelingHasGotOutOfWater);
            }
        }

        private void DisabledCastReleaseTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            var token = _cts.Token;
            if (token.IsCancellationRequested) return;
            if (_castTime != kDisabledCastTime) return;
            Console.WriteLine("Disabled cast release timer elapsed, releasing for a while");
            ReleaseForDuration(50);
        }
    }
}
