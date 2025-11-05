using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VRChatAutoFishing
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
            tbOSCAddr.Validating += TbOSCIPAddr_Validating;
            tbOSCPort.Validating += TbOSCPort_Validating;
        }

        private void TbOSCPort_Validating(object? sender, CancelEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbOSCPort.Text))
            {
                errorProvider.SetError(tbOSCPort, "端口为空，将使用默认端口 " + AppSettings.DefaultOSCPort.ToString());
            }
            else if (!int.TryParse(tbOSCPort.Text, out int port) || port < 1 || port > 65535)
            {
                errorProvider.SetError(tbOSCPort, "请输入有效的端口号 (1-65535)。");
            }
            else
            {
                errorProvider.SetError(tbOSCPort, "");
            }
        }

        private void TbOSCIPAddr_Validating(object? sender, CancelEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbOSCAddr.Text))
            {
                errorProvider.SetError(tbOSCAddr, "IP地址为空，将使用默认IP " + AppSettings.DefaultOSCIPAddr);
            }
            else if (!System.Net.IPAddress.TryParse(tbOSCAddr.Text, out _))
            {
                errorProvider.SetError(tbOSCAddr, "请输入有效的IP地址。");
                //e.Cancel = true;
            }
            else
            {
                errorProvider.SetError(tbOSCAddr, "");
            }
        }

        private const string ConfigureFileName = "AutoFisherVRC.json";
        public AppSettings GetOverridenAppSettings(AppSettings overrides) { 
            return new AppSettings
            {
                Cast = overrides.Cast ?? true,
                CastingTime = overrides.CastingTime ?? AppSettings.DefaultCastingTime,
                OSCIPAddr = overrides.OSCIPAddr ?? (System.Net.IPAddress.TryParse(tbOSCAddr.Text, out _) ? tbOSCAddr.Text : AppSettings.DefaultOSCIPAddr),
                OSCPort = overrides.OSCPort ?? (int.TryParse(tbOSCPort.Text, out _) ? int.Parse(tbOSCPort.Text) : AppSettings.DefaultOSCPort),
                WebhookSettings = overrides.WebhookSettings ?? webhookNotificationSettings.SaveSettings()
            };
        }

        public static void SaveSettingsToFile(AppSettings appSettings)
        {
            string settingsJson = JsonSerializer.Serialize(appSettings);
            var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigureFileName);
            var userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ConfigureFileName);

            try
            {
                File.WriteAllText(localPath, settingsJson);
                return;
            }
            catch (Exception)
            {
                // 流下去，尝试写入用户目录
            }

            try
            {
                File.WriteAllText(userProfilePath, settingsJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法保存设置（这不会影响当前设置，但是设置项将在下次启动程序时还原）: {ex.Message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public AppSettings InitializeSavedValues()
        {
            var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigureFileName);
            var userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ConfigureFileName);
            string? settingsJson = null;

            try
            {
                if (File.Exists(localPath))
                {
                    settingsJson = File.ReadAllText(localPath);
                    return DoLoadAppSettings(settingsJson) ?? new AppSettings();
                }
            }
            catch (Exception) { }

            try
            {
                if (File.Exists(userProfilePath))
                {
                    settingsJson = File.ReadAllText(userProfilePath);
                    return DoLoadAppSettings(settingsJson) ?? new AppSettings();
                }
            }
            catch (Exception) { }
            return new AppSettings();
        }

        private AppSettings? DoLoadAppSettings(string settingsJson)
        {
            AppSettings? appSettings = JsonSerializer.Deserialize<AppSettings>(settingsJson ?? "");
            if (appSettings == null)
                return null;

            tbOSCAddr.Text = System.Net.IPAddress.TryParse(appSettings.OSCIPAddr ?? "", out _) ? appSettings.OSCIPAddr : AppSettings.DefaultOSCIPAddr;
            tbOSCPort.Text = (appSettings.OSCPort != null && appSettings.OSCPort > 0 && appSettings.OSCPort < 65536 ? appSettings.OSCPort : AppSettings.DefaultOSCPort).ToString();
            webhookNotificationSettings.LoadSettings(appSettings.WebhookSettings);
            return appSettings;
        }

        private Settings GetSettings()
        {
            Settings result = new();
            result.webhookNotificationHandler = webhookNotificationSettings.GetNotificationHandler();
            return result;
        }

        public Managers GetManagers()
        {
            Settings settings = GetSettings();
            Managers managers = new();
            managers.notificationManager = SetupNotifications(settings);
            return managers;
        }

        static private NotificationManager SetupNotifications(Settings settings)
        {
            NotificationManager result = new();

            if (settings.webhookNotificationHandler != null)
                result.AddHandler(settings.webhookNotificationHandler);

            if (!result.HasHandlers())
                return result;

            // Test Notifications
            var rc = result.NotifyAll("自动钓鱼程序已启动！");
            if (!rc.success)
            {
                MessageBox.Show($"通知功能测试失败（这不会阻塞钓鱼控制启动）：{rc.message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return result;
        }

    }

    internal class Settings
    {
        public INotificationHandler? webhookNotificationHandler;
    }

    public class AppSettings
    {
        public const double DefaultCastingTime = 1.7;
        public const string DefaultOSCIPAddr = "127.0.0.1";
        public const int DefaultOSCPort = 9000;

        // 和 AutoFisher 相关的属性直接展平，不要封装
        public bool? Cast { get; set; }
        public double? CastingTime { get; set; }
        public string? OSCIPAddr { get; set; }
        public int? OSCPort { get; set; }
        public WebhookNotificationSettingsControl.WebhookSettings? WebhookSettings { get; set; }
    }

    public class Managers
    {
        public NotificationManager notificationManager = new();
    }
}
