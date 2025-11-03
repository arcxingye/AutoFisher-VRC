using System;
using System.Drawing;
using System.IO;
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

        public MainForm()
        {
            InitializeComponent();
            InitializeFisherComponents();
        }

        private void InitializeFisherComponents()
        {
            AppSettings appSettings = _settingsForm.InitializeSavedValues();
            _delaySaveTimer = new();
            _delaySaveTimer.AutoReset = false;
            _delaySaveTimer.Elapsed += DelaySaveTimer_Elapsed;
            _delaySaveTimer.SynchronizingObject = this;

            trackBarCastTime.Minimum = 0;
            trackBarCastTime.Maximum = 17;
            trackBarCastTime.Value = (int)((appSettings.castingTime ?? AppSettings.DefaultCastingTime) * 10.0);
            UpdateCastTimeLabel();
        }

        private void CreateFisher()
        {
            if (_autoFisher != null)
            {
                _autoFisher.Dispose();
            }

            var castTime = trackBarCastTime.Value / 10.0;
            _autoFisher = new AutoFisher("127.0.0.1", 9000, castTime);

            // Subscribe to events from AutoFisher
            _autoFisher.OnUpdateStatus += status => Invoke(() => UpdateStatusText(status));
            _autoFisher.OnNotify += message => Invoke(() => _managers?.notificationManager.NotifyAll(message));

            _autoFisher.OnCriticalError += errorMessage => Invoke(() =>
                {
                    if (_managers?.notificationManager.NotifyAll(errorMessage).success ?? false)
                        return;
                    MessageBox.Show(this, errorMessage, "严重错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });

        }

        private void DelaySaveTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            _settingsForm.SaveSettingsToFile(new AppSettings
            {
                castingTime = trackBarCastTime.Value / 10.0,
            });
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

        private void btnToggle_Click(object? sender, EventArgs e)
        {
            _isFisherRunning = !_isFisherRunning;

            if (_isFisherRunning)
            {
                btnSettings.Enabled = false;
                _managers = _settingsForm.GetManagers();

                CreateFisher();
                _autoFisher?.Start();
            }
            else
            {
                _autoFisher?.Dispose();
                _autoFisher = null;
                btnSettings.Enabled = true;
            }
            btnToggle.Text = _isFisherRunning ? "停止" : "开始";
        }

        private void UpdateStatusText(string text)
        {
            Text = $"[{text}] - 自动钓鱼";
        }

        private void btnHelp_Click(object? sender, EventArgs e)
        {
            HelpForm helpForm = new();
            helpForm.ShowDialog();
            helpForm.Dispose();
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
            if (_autoFisher != null)
            {
                _autoFisher.CastTime = trackBarCastTime.Value / 10.0;
            }
            DelayToSaveSettings();
        }

        private void btnSettings_Click(object? sender, EventArgs e)
        {
            ClearDelayToSaveSettings();
            _settingsForm.ShowDialog();
        }

        // Windows Form Designer generated code
        private TrackBar trackBarCastTime;
        private Label lblCastValue;
        private Button btnToggle;
        private Button btnHelp;
        private Label label1;
        private Button btnSettings;

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            trackBarCastTime = new TrackBar();
            lblCastValue = new Label();
            btnToggle = new Button();
            btnHelp = new Button();
            label1 = new Label();
            btnSettings = new Button();
            ((System.ComponentModel.ISupportInitialize)trackBarCastTime).BeginInit();
            SuspendLayout();
            // 
            // trackBarCastTime
            // 
            trackBarCastTime.Location = new Point(80, 12);
            trackBarCastTime.Maximum = 17;
            trackBarCastTime.Name = "trackBarCastTime";
            trackBarCastTime.Size = new Size(130, 45);
            trackBarCastTime.TabIndex = 1;
            trackBarCastTime.Value = 17;
            trackBarCastTime.Scroll += trackBarCastTime_Scroll;
            // 
            // lblCastValue
            // 
            lblCastValue.Location = new Point(215, 15);
            lblCastValue.Name = "lblCastValue";
            lblCastValue.Size = new Size(40, 20);
            lblCastValue.TabIndex = 2;
            lblCastValue.Text = "1.7秒";
            // 
            // btnToggle
            // 
            btnToggle.Location = new Point(91, 65);
            btnToggle.Name = "btnToggle";
            btnToggle.Size = new Size(70, 30);
            btnToggle.TabIndex = 4;
            btnToggle.Text = "开始";
            btnToggle.Click += btnToggle_Click;
            // 
            // btnHelp
            // 
            btnHelp.Location = new Point(15, 65);
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new Size(70, 30);
            btnHelp.TabIndex = 3;
            btnHelp.Text = "说明";
            btnHelp.Click += btnHelp_Click;
            // 
            // label1
            // 
            label1.Location = new Point(15, 15);
            label1.Name = "label1";
            label1.Size = new Size(60, 20);
            label1.TabIndex = 0;
            label1.Text = "蓄力时间:";
            // 
            // btnSettings
            // 
            btnSettings.Location = new Point(167, 65);
            btnSettings.Name = "btnSettings";
            btnSettings.Size = new Size(77, 30);
            btnSettings.TabIndex = 11;
            btnSettings.Text = "高级设置";
            btnSettings.UseVisualStyleBackColor = true;
            btnSettings.Click += btnSettings_Click;
            // 
            // MainForm
            // 
            BackgroundImageLayout = ImageLayout.None;
            ClientSize = new Size(260, 104);
            Controls.Add(btnSettings);
            Controls.Add(label1);
            Controls.Add(trackBarCastTime);
            Controls.Add(lblCastValue);
            Controls.Add(btnHelp);
            Controls.Add(btnToggle);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "自动钓鱼v1.5.3";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            ((System.ComponentModel.ISupportInitialize)trackBarCastTime).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

    }
}