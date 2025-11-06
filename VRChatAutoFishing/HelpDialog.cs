using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Diagnostics;

namespace VRChatAutoFishing
{
    [DesignerCategory("Code")]
    public class HelpDialog : Form
    {
        public HelpDialog()
        {
            InitializeComponent();
            SetupHelpForm();
        }

        private void SetupHelpForm()
        {
            this.Text = "使用说明";
            this.ClientSize = new Size(610, 1000);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            Panel mainPanel = new Panel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.AutoScroll = true;
            mainPanel.BackColor = Color.White;
            this.Controls.Add(mainPanel);

            // 标题
            Label authorLabel = new Label();
            authorLabel.Location = new Point(0, 20);
            authorLabel.Size = new Size(580, 30);
            authorLabel.Text = "使用说明";
            authorLabel.Font = new Font("微软雅黑", 14, FontStyle.Bold);
            authorLabel.TextAlign = ContentAlignment.MiddleCenter;
            authorLabel.ForeColor = Color.DarkBlue;
            mainPanel.Controls.Add(authorLabel);

            int currentY = 60;

            // 加载三张图片
            LoadImageWithFallback(mainPanel, "help_image1.jpg", "1.打开OSC", 1064, 341, ref currentY);
            LoadImageWithFallback(mainPanel, "help_image2.jpg", "2.打开日志", 1064, 745, ref currentY);
            LoadImageWithFallback(mainPanel, "help_image3.jpg", "3.把鱼钩静置到桶上", 1064, 745, ref currentY);

            // 第四步 - 只有文字说明
            Label step4Label = new Label();
            step4Label.Location = new Point(25, currentY);
            step4Label.Size = new Size(550, 25);
            step4Label.Text = "4.点击本软件的开始按钮";
            step4Label.Font = new Font("微软雅黑", 11, FontStyle.Bold);
            step4Label.TextAlign = ContentAlignment.MiddleLeft;
            mainPanel.Controls.Add(step4Label);

            currentY += 40;

            // 其它说明
            Label step4DescLabel = new Label();
            step4DescLabel.Location = new Point(25, currentY);
            step4DescLabel.Size = new Size(550, 60);
            step4DescLabel.Text = "通常，做完这些就可以开始自动钓鱼了，可以后台挂着VRC玩别的游戏去。软件可能会随着世界更新而失效，注意更新，没有更新要么懒得要么真没办法了。本软件以合法性为主，仅仅只是个OSC程序，安全性较高。";
            step4DescLabel.Font = new Font("微软雅黑", 10, FontStyle.Regular);
            step4DescLabel.TextAlign = ContentAlignment.TopLeft;
            mainPanel.Controls.Add(step4DescLabel);

            currentY += 80;

            // 第五步排查
            Label step5DescLabel = new Label();
            step5DescLabel.Location = new Point(25, currentY);
            step5DescLabel.Size = new Size(550, 350);
            step5DescLabel.Text = "排错指南\n" +
                "[钓鱼状态有变化但无抛竿]OSC问题\n" +
                "1.确保已打开OSC\n" +
                "2.确保OSC端口为9000，打开OSC调试可以看到端口\n" +
                "3.如果你的OSC端口不是9000，到本软件设置里改成相应的端口\n" +
                "4.确保其它软件不会占用端口，例如VRCFT、个别加速器\n\n" +
                "[钓鱼状态无变化]无法读取日志\n" +
                "1.确保日志调到Full\n" +
                "2.尝试右键以管理员身份运行\n" +
                "3.确保世界没有发生错误，详见天空是否出现错误提示\n\n" +
                "[其它注意事项]\n" +
                "1.建议不要双开VRCHAT客户端\n" +
                "2.建议不要开其它OSC软件\n\n" +
                "功能提议/报告问题请发 Issues：";
            step5DescLabel.Font = new Font("微软雅黑", 10, FontStyle.Regular);
            step5DescLabel.TextAlign = ContentAlignment.TopLeft;
            mainPanel.Controls.Add(step5DescLabel);

            currentY += 350;

            // 可点击的超链接
            LinkLabel linkLabel = new LinkLabel();
            linkLabel.Location = new Point(25, currentY);
            linkLabel.Size = new Size(550, 35);
            linkLabel.Text = "https://github.com/arcxingye/AutoFisher-VRC";
            linkLabel.LinkColor = Color.Blue;
            linkLabel.Font = new Font("微软雅黑", 10, FontStyle.Underline);
            linkLabel.Links.Add(0, linkLabel.Text.Length, linkLabel.Text);
            linkLabel.LinkClicked += (sender, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(e.Link!.LinkData!.ToString()!) { UseShellExecute = true });
                }
                catch
                {
                    MessageBox.Show("无法打开链接，请手动访问。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            mainPanel.Controls.Add(linkLabel);
        }

        private void LoadImageWithFallback(Panel parent, string imageName, string description, int originalWidth, int originalHeight, ref int currentY)
        {
            int displayWidth = 550;
            int displayHeight = (int)((double)originalHeight / originalWidth * displayWidth);

            // 说明文字
            Label descLabel = new Label();
            descLabel.Location = new Point(25, currentY);
            descLabel.Size = new Size(displayWidth, 25);
            descLabel.Text = description;
            descLabel.Font = new Font("微软雅黑", 11, FontStyle.Bold);
            descLabel.TextAlign = ContentAlignment.MiddleLeft;
            parent.Controls.Add(descLabel);

            currentY += 30;

            // 图片容器
            Panel imageContainer = new Panel();
            imageContainer.Location = new Point(25, currentY);
            imageContainer.Size = new Size(displayWidth, displayHeight);
            imageContainer.BorderStyle = BorderStyle.FixedSingle;
            imageContainer.BackColor = Color.White;
            parent.Controls.Add(imageContainer);

            PictureBox pictureBox = new PictureBox();
            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox.Dock = DockStyle.Fill;

            bool imageLoaded = LoadImageFromEmbeddedResource(pictureBox, imageName);

            if (!imageLoaded)
            {
                ShowErrorImage(pictureBox, displayWidth, displayHeight, $"未找到图片: {imageName}");
            }

            imageContainer.Controls.Add(pictureBox);
            currentY += displayHeight + 20;
        }

        private bool LoadImageFromEmbeddedResource(PictureBox pictureBox, string imageName)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();

                // 尝试不同的资源名称格式
                string[] possibleResourceNames = {
                    $"VRChatAutoFishing.Resources.{imageName}",
                    $"VRChatAutoFishing.{imageName}",
                    $"Resources.{imageName}",
                    imageName
                };

                foreach (string resourceName in possibleResourceNames)
                {
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            // 从流创建 Image，然后复制到新的 Bitmap，这样可以立即关闭流并释放原始 Image
                            using (var img = Image.FromStream(stream))
                            {
                                pictureBox.Image = new Bitmap(img);
                            }
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // 忽略错误，继续尝试其他方法
            }
            return false;
        }

        private void ShowErrorImage(PictureBox pictureBox, int width, int height, string errorMessage)
        {
            Bitmap errorImage = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(errorImage))
            {
                g.Clear(Color.LightGray);
                using (Font font = new Font("微软雅黑", 9))
                using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(errorMessage, font, Brushes.Red, new RectangleF(0, 0, width, height), sf);
                }
            }
            pictureBox.Image = errorImage;
        }

        // 在 Dispose 时释放所有 PictureBox 的 Image，防止 GDI 句柄泄露
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    DisposeImagesRecursive(this);
                }
                catch
                {
                    // 忽略释放时的任何异常，仍然继续释放基类资源
                }
            }
            base.Dispose(disposing);
        }

        private void DisposeImagesRecursive(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is PictureBox pb)
                {
                    if (pb.Image != null)
                    {
                        try
                        {
                            pb.Image.Dispose();
                        }
                        catch { }
                        pb.Image = null;
                    }
                }
                // 递归处理子控件
                if (c.HasChildren)
                {
                    DisposeImagesRecursive(c);
                }
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "HelpForm";
            this.ResumeLayout(false);
        }
    }
}
