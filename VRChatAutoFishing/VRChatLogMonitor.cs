using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VRChatAutoFishing
{
    public class VRChatLogMonitor
    {
        private event Action OnDataSaved;
        private event Action OnFishPickup;

        private FileSystemWatcher? _watcher;
        private string? _currentLogPath;
        private long _filePosition;
        private readonly object _lockObject = new object();
        private CancellationTokenSource? _cancellationTokenSource;

        public VRChatLogMonitor(Action onDataSaved, Action onFishPickup)
        {
            _filePosition = 0;
            OnDataSaved = onDataSaved;
            OnFishPickup = onFishPickup;
        }

        public void StartMonitoring()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                return; // Already monitoring
            }

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            string logDir = GetVRChatLogDirectory();
            if (!Directory.Exists(logDir))
            {
                Console.WriteLine("VRChat日志目录不存在");
                return;
            }

            _watcher = new FileSystemWatcher(logDir, "output_log_*.txt");
            _watcher.Created += (sender, e) =>
            {
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    UpdateLogFile();
                }
            };
            _watcher.Changed += (sender, e) =>
            {
                if (e.FullPath == _currentLogPath)
                {
                    ProcessLogChanges();
                }
            };
            
            // Initial check and skip existing content
            UpdateLogFile();
            ReadNewContent();

            // Start a background task to periodically check for new log files
            Task.Run(() => PeriodicLogFileCheck(token), token);
            _watcher.EnableRaisingEvents = true;
        }

        public void StopMonitoring()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
        }

        private async Task PeriodicLogFileCheck(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    UpdateLogFile();
                    ProcessLogChanges();
                    await Task.Delay(TimeSpan.FromMilliseconds(250), token);
                }
                catch (TaskCanceledException)
                {
                    break; // Task was cancelled, exit loop
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"日志文件定期检查出错: {ex.Message}");
                }
            }
        }

        private void ProcessLogChanges()
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
                return;

            string content = ReadNewContent();
            if (string.IsNullOrEmpty(content))
                return;

            // 使用StringReader逐行处理新内容，确保每个事件都被捕获
            using (var reader = new StringReader(content))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("SAVED DATA"))
                    {
                        OnDataSaved?.Invoke();
                    }

                    if (line.Contains("Fish Pickup attached to rod Toggles(True)"))
                    {
                        OnFishPickup?.Invoke();
                    }
                }
            }
        }

        private string GetVRChatLogDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.GetFullPath(Path.Combine(appData, @"..\LocalLow\VRChat\VRChat"));
        }

        // Thread-safe
        private bool UpdateLogFile()
        {
            string? newLog = FindLatestLog();
            lock (_lockObject)
            {
                if (newLog != null && newLog != _currentLogPath)
                {
                    Console.WriteLine($"检测到新日志文件: {newLog}");
                    _currentLogPath = newLog;
                    _filePosition = 0;
                    return true;
                }
            }
            return false;
        }

        private string? FindLatestLog()
        {
            string logDir = GetVRChatLogDirectory();
            if (!Directory.Exists(logDir))
                return null;

            var logFiles = Directory.GetFiles(logDir, "output_log_*.txt");
            if (logFiles.Length == 0)
                return null;

            string? latestFile = null;
            DateTime latestTime = DateTime.MinValue;

            foreach (string file in logFiles)
            {
                DateTime writeTime = File.GetLastWriteTime(file);
                if (writeTime > latestTime)
                {
                    latestTime = writeTime;
                    latestFile = file;
                }
            }

            return latestFile;
        }

        // Thread-safe
        public string ReadNewContent()
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(_currentLogPath) || !File.Exists(_currentLogPath))
                    return string.Empty;

                try
                {
                    using (var stream = new FileStream(_currentLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (_filePosition > stream.Length)
                            _filePosition = 0;

                        stream.Seek(_filePosition, SeekOrigin.Begin);

                        // 使用StreamReader来处理文本和编码
                        using (var reader = new StreamReader(stream))
                        {
                            string newContent = reader.ReadToEnd();
                            if (string.IsNullOrEmpty(newContent))
                                return string.Empty;

                            int lastNewLineIndex = newContent.LastIndexOf('\n');

                            // 如果没有找到换行符，说明新内容只包含一个不完整的行
                            // 我们暂时不处理它，等待下一次读取
                            if (lastNewLineIndex == -1)
                                return string.Empty;

                            // 提取直到最后一个换行符为止的所有完整行
                            string contentToProcess = newContent.Substring(0, lastNewLineIndex + 1);

                            // 计算这些完整行所占的字节数，并更新文件位置指针
                            // 这确保了多字节字符（如中文）被正确处理
                            int bytesToAdvance = reader.CurrentEncoding.GetByteCount(contentToProcess);
                            _filePosition += bytesToAdvance;

                            return contentToProcess;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                    {
                        Console.WriteLine($"读取日志失败: {ex.Message}");
                    }
                    return string.Empty;
                }
            }
        }
    }
}