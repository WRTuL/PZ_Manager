using PZManager.Models;
using PZManager.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PZManager.Views
{
    public partial class ServerConfigPage : Page
    {
        private ServerConfig _config = new();
        public ServerConfigPage() { InitializeComponent(); TryAutoLoad(); }

        private string GetIniPath() => Path.Combine(MainWindow.AppSettings.ConfigDirectory, MainWindow.AppSettings.ServerConfigName + ".ini");

        private void TryAutoLoad()
        {
            var path = GetIniPath();
            if (File.Exists(path)) { _config = IniService.LoadServerConfig(path); PopulateFields(); TbConfigStatus.Text = $"loaded from {Path.GetFileName(path)}"; }
        }

        private void PopulateFields()
        {
            TbPublicName.Text = _config.PublicName; TbPublicDesc.Text = _config.PublicDescription;
            TbWelcome.Text = _config.ServerWelcomeMessage; CbPublic.IsChecked = _config.PublicServer;
            TbPassword.Text = _config.Password; TbAdminPw.Text = _config.AdminPassword;
            TbMaxPlayers.Text = _config.MaxPlayers.ToString(); TbPort.Text = _config.Port.ToString();
            TbUdpPort.Text = _config.UdpPort.ToString(); TbRconPort.Text = _config.RconPort.ToString();
            TbRconPw.Text = _config.RconPassword; TbJvm.Text = _config.JvmArgs;
            CbPauseEmpty.IsChecked = _config.PauseEmpty;
        }

        private void ReadFields()
        {
            _config.PublicName = TbPublicName.Text; _config.PublicDescription = TbPublicDesc.Text;
            _config.ServerWelcomeMessage = TbWelcome.Text; _config.PublicServer = CbPublic.IsChecked == true;
            _config.Password = TbPassword.Text; _config.AdminPassword = TbAdminPw.Text; _config.JvmArgs = TbJvm.Text;
            _config.PauseEmpty = CbPauseEmpty.IsChecked == true;
            _config.MaxPlayers = int.TryParse(TbMaxPlayers.Text, out var mp)  ? mp  : _config.MaxPlayers;
            _config.Port       = int.TryParse(TbPort.Text,       out var pt)  ? pt  : _config.Port;
            _config.UdpPort    = int.TryParse(TbUdpPort.Text,    out var udp) ? udp : _config.UdpPort;
            _config.RconPort   = int.TryParse(TbRconPort.Text,   out var rp)  ? rp  : _config.RconPort;
            _config.RconPassword = TbRconPw.Text;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ReadFields();
            try { IniService.SaveServerConfig(GetIniPath(), _config); TbConfigStatus.Text = "saved ✓"; }
            catch (Exception ex) { MessageBox.Show($"save failed:\n{ex.Message}", "error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void Reload_Click(object sender, RoutedEventArgs e) => TryAutoLoad();
    }
}
