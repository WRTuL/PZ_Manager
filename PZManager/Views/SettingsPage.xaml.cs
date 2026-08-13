using Microsoft.Win32;
using PZManager.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PZManager.Views
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            var s = MainWindow.AppSettings;
            TbServerDir.Text    = s.ServerDirectory;
            TbConfigDir.Text    = s.ConfigDirectory;
            TbConfigName.Text   = s.ServerConfigName;
            TbRconHost.Text     = s.RconHost;
            TbSteamApiKey.Text  = s.SteamApiKey;
            CbAutoFetch.IsChecked = s.AutoFetchModNames;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            var tag          = btn.Tag?.ToString() ?? "server";
            var current_path = tag == "server" ? TbServerDir.Text : TbConfigDir.Text;
            var dlg = new OpenFileDialog
            {
                CheckFileExists = false, ValidateNames = false,
                FileName = "Select this folder", Filter = "Folder|*.none",
                Title = tag == "server" ? "Select PZ Server Install Directory" : "Select PZ Config Directory"
            };
            if (Directory.Exists(current_path)) dlg.InitialDirectory = current_path;
            if (dlg.ShowDialog() == true)
            {
                var selected = Path.GetDirectoryName(dlg.FileName) ?? current_path;
                if (tag == "server") TbServerDir.Text = selected;
                else TbConfigDir.Text = selected;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var s = MainWindow.AppSettings;
            s.ServerDirectory  = TbServerDir.Text.Trim();
            s.ConfigDirectory  = TbConfigDir.Text.Trim();
            s.ServerConfigName = TbConfigName.Text.Trim();
            s.RconHost         = TbRconHost.Text.Trim();
            s.SteamApiKey      = TbSteamApiKey.Text.Trim();
            s.AutoFetchModNames = CbAutoFetch.IsChecked == true;
            SettingsService.Save(s);
            TbSaveStatus.Text = "settings saved ✓";
        }

        private void ForceKill_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "This will immediately kill the server process and all child processes.\n\n" +
                "No save will be performed. Players will be disconnected instantly.\n\n" +
                "Continue?",
                "Force Kill Server",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            // reach into ConsolePage's launcher via the registered services
            var launcher = PZManager.Views.AutoRestartPage.GetLauncher();
            if (launcher == null)
            {
                TbSaveStatus.Text = "no launcher found — open RCON Console tab first.";
                return;
            }
            launcher.ForceKill();
            TbSaveStatus.Text = "force kill sent ✓";
        }
    }
}
