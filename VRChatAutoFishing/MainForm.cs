using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Timers;
using System.Windows.Forms;

namespace VRChatAutoFishing
{
    public partial class MainForm : Form
    {
        private SettingsForm _settingsForm = new();
        private System.Timers.Timer _delaySaveTimer;
        private AutoFisher? _autoFisher;
        private bool _isFisherRunning = false;
        private Managers? _managers;
        private string _fullTitle;
        private DateTime _startTime = DateTime.MaxValue;
        private DateTime _endTime = DateTime.MinValue;
        int _errorCount = 0;
        int _fishCount = 0;
        private System.Timers.Timer _updateAnalysisTimer;

        public MainForm()
        {
            InitializeComponent();
            _fullTitle = GetTitleWithVersion();
            Text = _fullTitle;

            AppSettings appSettings = _settingsForm.InitializeSavedValues();
            _delaySaveTimer = new();
            _delaySaveTimer.AutoReset = false;
            _delaySaveTimer.Elapsed += DelaySaveTimer_Elapsed;
            _delaySaveTimer.SynchronizingObject = this;

            _updateAnalysisTimer = new();
            _updateAnalysisTimer.Interval = 1000;
            _updateAnalysisTimer.AutoReset = true;
            _updateAnalysisTimer.Elapsed += (s, e) => Invoke(DoUpdateAnalysis);

            trackBarCastTime.Minimum = 0;
            trackBarCastTime.Maximum = 17;
            trackBarCastTime.Value = (int)((appSettings.CastingTime ?? AppSettings.DefaultCastingTime) * 10.0);
            chbCast.Checked = appSettings.Cast ?? true;
            ClearDelayToSaveSettings();
            UpdateCastTimeLabel();
        }

        private string GetTitleWithVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                return $"自动钓鱼 v{version.Major}.{version.Minor}.{version.Build}";
            }
            return "自动钓鱼";
        }

        private void CreateFisher()
        {
            Console.WriteLine("Recreating AutoFisher");
            if (_autoFisher != null)
            {
                _autoFisher.Dispose();
            }

            var appSettings = _settingsForm.GetOverridenAppSettings(GetOverridesOfAppSettings());
            string ip = appSettings.OSCIPAddr ?? AppSettings.DefaultOSCIPAddr;
            int port = appSettings.OSCPort ?? AppSettings.DefaultOSCPort;
            double? castTime = chbCast.Checked ? (appSettings.CastingTime ?? AppSettings.DefaultCastingTime) : null;

            _autoFisher = new AutoFisher(ip, port, castTime);

            // Subscribe to events from AutoFisher
            _autoFisher.OnUpdateStatus += status => Invoke(() => UpdateStatusText(status));
            _autoFisher.OnNotify += message => Invoke(() => { _errorCount++; _managers?.notificationManager.NotifyAll(message); });

            _autoFisher.OnCriticalError += errorMessage => Invoke(() =>
                {
                    _errorCount++;
                    if (_managers?.notificationManager.NotifyAll(errorMessage).success ?? false)
                        return;
                    MessageBox.Show(this, errorMessage, "严重错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });

            _autoFisher.OnFishCaught += fishCount => Invoke( () => { _fishCount = fishCount; DoUpdateAnalysis(); });
        }

        private void DoUpdateAutoFisherConfiguration()
        {
            if (!_isFisherRunning || _autoFisher == null) return;

            if ((_autoFisher.CastTime == AutoFisher.kDisabledCastTime && chbCast.Checked) ||
                (_autoFisher.CastTime != AutoFisher.kDisabledCastTime && !chbCast.Checked))
            {
                // 此属性不支持动态更改，重新创建一个AutoFisher实例
                CreateFisher();
                _autoFisher?.Start();
                return;
            }
            if (chbCast.Checked)
                _autoFisher.CastTime = trackBarCastTime.Value / 10.0;

        }

        private void DoUpdateAnalysis()
        {
            if (_endTime < _startTime) return;
            _endTime = DateTime.Now;
            TimeSpan totalTime = _endTime - _startTime;
            double avgTimePerFish = _fishCount > 0 ? totalTime.TotalSeconds / _fishCount : 0;

            txtAnalysis.Text = $"已钓：{_fishCount} 条，共用时：{totalTime:hh\\:mm\\:ss}\r\n" +
                $"平均耗时：{avgTimePerFish:F1} 秒/条，错误次数：{_errorCount}";
        }

        private void DelaySaveTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            SettingsForm.SaveSettingsToFile(_settingsForm.GetOverridenAppSettings(GetOverridesOfAppSettings()));
        }

        private void DelayToSaveSettings()
        {
            if (_delaySaveTimer.Enabled)
            {
                _delaySaveTimer.Stop();
            }
            _delaySaveTimer.Interval = 2000;
            _delaySaveTimer.Start();
        }

        private void ClearDelayToSaveSettings()
        {
            if (_delaySaveTimer.Enabled)
                _delaySaveTimer.Stop();
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            // The AutoFisher will be created and started on button click.
        }

        private AppSettings GetOverridesOfAppSettings()
        {
            return new AppSettings
            {
                CastingTime = trackBarCastTime.Value / 10.0,
                Cast = chbCast.Checked,
            };
        }

        private void btnToggle_Click(object? sender, EventArgs e)
        {
            _isFisherRunning = !_isFisherRunning;

            if (_isFisherRunning)
            {
                btnSettings.Enabled = false;
                _managers = _settingsForm.GetManagers();
                _startTime = DateTime.Now;
                _endTime = DateTime.Now;
                _fishCount = 0;
                _errorCount = 0;
                _updateAnalysisTimer.Start();
                DoUpdateAnalysis();
                CreateFisher();
                _autoFisher?.Start();
            }
            else
            {
                _autoFisher?.Dispose();
                _autoFisher = null;
                _updateAnalysisTimer.Stop();
                btnSettings.Enabled = true;
            }
            btnToggle.Text = _isFisherRunning ? "  停止" : "  开始";
            btnToggle.ImageIndex = _isFisherRunning ? 4 : 2;
        }

        private void UpdateStatusText(string text)
        {
            Text = $"[{text}] - {GetTitleWithVersion()}";
        }

        private void btnHelp_Click(object? sender, EventArgs e)
        {
            HelpDialog helpDialog = new();
            helpDialog.ShowDialog();
            helpDialog.Dispose();
        }

        private void UpdateCastTimeLabel()
        {
            lblCastValue.Text = $"{trackBarCastTime.Value / 10.0:0.0}秒";
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _autoFisher?.Dispose();
            _delaySaveTimer?.Dispose();
        }

        private void trackBarCastTime_Scroll(object? sender, EventArgs e)
        {
            UpdateCastTimeLabel();
            DoUpdateAutoFisherConfiguration();
            DelayToSaveSettings();
        }

        private void chbCast_CheckedChanged(object? sender, EventArgs e)
        {
            trackBarCastTime.Enabled = chbCast.Checked;
            DoUpdateAutoFisherConfiguration();
            DelayToSaveSettings();
        }

        private void btnSettings_Click(object? sender, EventArgs e)
        {
            ClearDelayToSaveSettings();
            _settingsForm.ShowDialog();
            var appSettings = _settingsForm.GetOverridenAppSettings(GetOverridesOfAppSettings());
            SettingsForm.SaveSettingsToFile(appSettings);
        }

        private void btnAnalysis_Click(object sender, EventArgs e)
        {
            if (btnAnalysis.FlatStyle == FlatStyle.Standard)
            {
                btnAnalysis.FlatStyle = FlatStyle.Flat;
                chbCast.Visible = false;
                trackBarCastTime.Visible = false;
                lblCastValue.Visible = false;
                txtAnalysis.Visible = true;
            }
            else
            {
                btnAnalysis.FlatStyle = FlatStyle.Standard;
                chbCast.Visible = true;
                trackBarCastTime.Visible = true;
                lblCastValue.Visible = true;
                txtAnalysis.Visible = false;
            }
        }
    }
}