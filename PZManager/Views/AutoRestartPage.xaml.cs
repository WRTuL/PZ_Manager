// auto_restart_page.xaml.cs
// configures and controls the scheduled server restart.
// because mod updates wait for no one, and neither does this timer.
using PZManager.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PZManager.Views
{
    public partial class AutoRestartPage : Page
    {
        // shared service instances — injected from ConsolePage via static accessors
        // slight hack but avoids threading nightmares with passing instances around pages
        private static AutoRestartService? _restart_svc;
        private static RconService?        _rcon_svc;
        private static ServerLaunchService? _launcher_svc;

        // these get set by ConsolePage when it creates its services
        public static void RegisterServices(RconService rcon, ServerLaunchService launcher)
        {
            _rcon_svc     = rcon;
            _launcher_svc = launcher;
            _restart_svc ??= new AutoRestartService();
        }

        /// Exposes the launcher so other pages (e.g. Settings force-kill) can reach it.
        public static ServerLaunchService? GetLauncher() => _launcher_svc;

        public AutoRestartPage()
        {
            InitializeComponent();
            LoadConfig();
        }

        private void LoadConfig()
        {
            var s = MainWindow.AppSettings;
            TbIntervalHours.Text = s.AutoRestartHours.ToString();
            TbWarningMsg.Text    = s.RestartWarningMsg;
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            ApplyConfigToSettings();
            PZManager.Services.SettingsService.Save(MainWindow.AppSettings);
            AppendLog("config saved.");
        }

        private void ApplyConfigToSettings()
        {
            var s = MainWindow.AppSettings;
            if (double.TryParse(TbIntervalHours.Text, out var h) && h > 0) s.AutoRestartHours = h;
            s.RestartWarningMsg = TbWarningMsg.Text.Trim();
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_restart_svc == null || _rcon_svc == null || _launcher_svc == null)
            {
                AppendLog("ERROR — open the RCON Console tab first to initialise the server services.");
                return;
            }

            if (_restart_svc.IsRunning)
            {
                // turn it off
                _restart_svc.Stop();
                SetStatus(false);
                AppendLog("auto-restart disabled.");
            }
            else
            {
                // turn it on
                ApplyConfigToSettings();
                var s = MainWindow.AppSettings;

                _restart_svc.StatusMessage  -= OnRestartMessage;
                _restart_svc.RestartImminent -= OnRestartImminent;
                _restart_svc.StatusMessage  += OnRestartMessage;
                _restart_svc.RestartImminent += OnRestartImminent;

                _restart_svc.Start(
                    s.AutoRestartHours, _rcon_svc, _launcher_svc,
                    s.ServerDirectory, s.ServerConfigName, "-Xmx4g",
                    s.RestartWarningMsg);

                SetStatus(true);
                UpdateNextRestart();
            }
        }

        private void OnRestartMessage(string msg, bool is_error)
            => Dispatcher.Invoke(() => AppendLog(msg));

        private void OnRestartImminent()
            => Dispatcher.Invoke(() => { AppendLog("restarting now…"); UpdateNextRestart(); });

        private void UpdateNextRestart()
        {
            if (_restart_svc?.IsRunning == true)
                TbNextRestart.Text = $"next restart: {_restart_svc.NextRestart:HH:mm:ss}  ({_restart_svc.NextRestart:ddd dd MMM})";
            else
                TbNextRestart.Text = "next restart: —";
        }

        private void SetStatus(bool enabled)
        {
            BtnStartRestart.Content = enabled ? "Disable Auto-Restart" : "Enable Auto-Restart";
            BtnStartRestart.Style   = (Style)FindResource(enabled ? "DangerButton" : "GreenButton");
            TbRestartStatus.Text    = enabled ? "● enabled" : "● disabled";
            TbRestartStatus.Foreground = enabled
                ? new SolidColorBrush(Color.FromRgb(0x4F, 0x9E, 0x6F))
                : new SolidColorBrush(Color.FromRgb(0xBF, 0x8C, 0x30));
            StatusBadge.Background = enabled
                ? new SolidColorBrush(Color.FromRgb(0x16, 0x2A, 0x1E))
                : new SolidColorBrush(Color.FromRgb(0x1E, 0x18, 0x10));
            StatusBadge.BorderBrush = enabled
                ? new SolidColorBrush(Color.FromRgb(0x23, 0x4A, 0x30))
                : new SolidColorBrush(Color.FromRgb(0x3A, 0x30, 0x10));
        }

        private void AppendLog(string msg)
        {
            var ts   = DateTime.Now.ToString("HH:mm:ss");
            TbLog.Text += $"\n[{ts}] {msg}";
            LogScroller.ScrollToEnd();
        }
    }
}
