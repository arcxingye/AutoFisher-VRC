namespace VRChatAutoFishing
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        /// 
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            trackBarCastTime = new TrackBar();
            lblCastValue = new Label();
            btnToggle = new Button();
            imageListIcon = new ImageList(components);
            btnHelp = new Button();
            btnSettings = new Button();
            btnAnalysis = new Button();
            chbCast = new CheckBox();
            txtAnalysis = new TextBox();
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
            btnAnalysis.Click += btnAnalysis_Click;
            // 
            // chbCast
            // 
            chbCast.AutoSize = true;
            chbCast.Checked = true;
            chbCast.CheckState = CheckState.Checked;
            chbCast.Location = new Point(15, 15);
            chbCast.Name = "chbCast";
            chbCast.Size = new Size(63, 21);
            chbCast.TabIndex = 13;
            chbCast.Text = "蓄力：";
            chbCast.UseVisualStyleBackColor = true;
            chbCast.CheckedChanged += chbCast_CheckedChanged;
            // 
            // txtAnalysis
            // 
            txtAnalysis.BackColor = SystemColors.Control;
            txtAnalysis.BorderStyle = BorderStyle.FixedSingle;
            txtAnalysis.Location = new Point(12, 12);
            txtAnalysis.Multiline = true;
            txtAnalysis.Name = "txtAnalysis";
            txtAnalysis.Size = new Size(236, 44);
            txtAnalysis.TabIndex = 14;
            txtAnalysis.Text = "暂无统计信息";
            txtAnalysis.Visible = false;
            // 
            // MainForm
            // 
            BackgroundImageLayout = ImageLayout.None;
            ClientSize = new Size(260, 104);
            Controls.Add(chbCast);
            Controls.Add(btnAnalysis);
            Controls.Add(btnSettings);
            Controls.Add(trackBarCastTime);
            Controls.Add(lblCastValue);
            Controls.Add(btnHelp);
            Controls.Add(btnToggle);
            Controls.Add(txtAnalysis);
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
        #endregion

        private TrackBar trackBarCastTime;
        private Label lblCastValue;
        private Button btnToggle;
        private Button btnHelp;
        private Button btnSettings;
        private Button btnAnalysis;
        private CheckBox chbCast;
        private ImageList imageListIcon;
        private TextBox txtAnalysis;
    }
}
