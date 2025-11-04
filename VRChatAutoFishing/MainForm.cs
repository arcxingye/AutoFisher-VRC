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
        private ImageList imageListIcon;
        private System.ComponentModel.IContainer components;
        private Managers? _managers;

        public MainForm()
        {
            InitializeComponent();
            InitializeFisherComponents();
            Text = GetTitleWithVersion();
        }

        private string GetTitleWithVersion() {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                return $"自动钓鱼 v{version.Major}.{version.Minor}.{version.Build}";
            }
            return "自动钓鱼";
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

        private void CreateFisher(string ip, int port, double castTime)
        {
            if (_autoFisher != null)
            {
                _autoFisher.Dispose();
            }

            _autoFisher = new AutoFisher(ip, port, castTime);

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

            SettingsForm.SaveSettingsToFile(_settingsForm.GetOverridenAppSettings(new AppSettings
            {
                castingTime = trackBarCastTime.Value / 10.0,
            }));
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
                var appSettings = _settingsForm.GetOverridenAppSettings(new AppSettings
                {
                    castingTime = trackBarCastTime.Value / 10.0,
                });

                CreateFisher(appSettings.OSCIPAddr ?? AppSettings.DefaultOSCIPAddr,
                             appSettings.OSCPort ?? AppSettings.DefaultOSCPort,
                             appSettings.castingTime ?? AppSettings.DefaultCastingTime);
                _autoFisher?.Start();
            }
            else
            {
                _autoFisher?.Dispose();
                _autoFisher = null;
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
            var appSettings = _settingsForm.GetOverridenAppSettings(new AppSettings
            {
                castingTime = trackBarCastTime.Value / 10.0,
            });
            SettingsForm.SaveSettingsToFile(appSettings);
        }

        // Windows Form Designer generated code
        private TrackBar trackBarCastTime;
        private Label lblCastValue;
        private Button btnToggle;
        private Button btnHelp;
        private Label label1;
        private Button btnSettings;
        private Button btnAnalysis;

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            trackBarCastTime = new TrackBar();
            lblCastValue = new Label();
            btnToggle = new Button();
            btnHelp = new Button();
            label1 = new Label();
            btnSettings = new Button();
            btnAnalysis = new Button();
            imageListIcon = new ImageList(components);
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
            btnToggle.ImageIndex = 2;
            btnToggle.ImageList = imageListIcon;
            btnToggle.Location = new Point(159, 62);
            btnToggle.Name = "btnToggle";
            btnToggle.Size = new Size(89, 30);
            btnToggle.TabIndex = 4;
            btnToggle.Text = "  开始";
            btnToggle.TextImageRelation = TextImageRelation.ImageBeforeText;
            btnToggle.Click += btnToggle_Click;
            // 
            // btnHelp
            // 
            btnHelp.ImageKey = "help (Custom).png";
            btnHelp.ImageList = imageListIcon;
            btnHelp.Location = new Point(15, 62);
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new Size(42, 30);
            btnHelp.TabIndex = 3;
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
            btnSettings.ImageKey = "cog (Custom).png";
            btnSettings.ImageList = imageListIcon;
            btnSettings.Location = new Point(63, 62);
            btnSettings.Name = "btnSettings";
            btnSettings.Size = new Size(42, 30);
            btnSettings.TabIndex = 11;
            btnSettings.UseVisualStyleBackColor = true;
            btnSettings.Click += btnSettings_Click;
            // 
            // btnAnalysis
            // 
            btnAnalysis.ImageKey = "poll (Custom).png";
            btnAnalysis.ImageList = imageListIcon;
            btnAnalysis.Location = new Point(111, 62);
            btnAnalysis.Name = "btnAnalysis";
            btnAnalysis.Size = new Size(42, 30);
            btnAnalysis.TabIndex = 12;
            btnAnalysis.UseVisualStyleBackColor = true;
            btnAnalysis.Visible = false;
            // 
            // imageListIcon
            // 
            imageListIcon.ColorDepth = ColorDepth.Depth32Bit;
            imageListIcon.ImageStream = (ImageListStreamer)resources.GetObject("imageListIcon.ImageStream");
            imageListIcon.TransparentColor = Color.Transparent;
            imageListIcon.Images.SetKeyName(0, "cog (Custom).png");
            imageListIcon.Images.SetKeyName(1, "help (Custom).png");
            imageListIcon.Images.SetKeyName(2, "play (Custom).png");
            imageListIcon.Images.SetKeyName(3, "poll (Custom).png");
            imageListIcon.Images.SetKeyName(4, "stop-circle (Custom).png");
            // 
            // MainForm
            // 
            BackgroundImageLayout = ImageLayout.None;
            ClientSize = new Size(260, 104);
            Controls.Add(btnAnalysis);
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
            Text = "自动钓鱼";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            ((System.ComponentModel.ISupportInitialize)trackBarCastTime).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

    }
}