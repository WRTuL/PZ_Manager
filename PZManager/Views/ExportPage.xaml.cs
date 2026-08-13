using Microsoft.Win32;
using PZManager.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PZManager.Views
{
    public partial class ExportPage : Page
    {
        public ExportPage() => InitializeComponent();
        private string Cfg(string suffix) => Path.Combine(MainWindow.AppSettings.ConfigDirectory, MainWindow.AppSettings.ServerConfigName + suffix);
        private void ExportMods_Click(object sender, RoutedEventArgs e)
        {
            var src = Cfg(".ini"); if (!File.Exists(src)) { ShowMissing(src); return; }
            var (mods, ws, _, _) = IniService.ReadModLines(src);
            var dlg = new SaveFileDialog { Filter = "Text|*.txt", FileName = "modlist.txt" };
            if (dlg.ShowDialog() == true) { File.WriteAllLines(dlg.FileName, new[] { $"Mods={mods}", $"WorkshopItems={ws}" }); TbExportStatus.Text = "exported mod list ✓"; }
        }
        private void ExportIni_Click(object sender, RoutedEventArgs e)
        {
            var src = Cfg(".ini"); if (!File.Exists(src)) { ShowMissing(src); return; }
            var dlg = new SaveFileDialog { Filter = "INI|*.ini", FileName = "serverconfig_backup.ini" };
            if (dlg.ShowDialog() == true) { File.Copy(src, dlg.FileName, true); TbExportStatus.Text = "exported config ✓"; }
        }
        private void ExportSandbox_Click(object sender, RoutedEventArgs e)
        {
            var src = Cfg("_SandboxVars.lua"); if (!File.Exists(src)) { ShowMissing(src); return; }
            var dlg = new SaveFileDialog { Filter = "Lua|*.lua", FileName = "SandboxVars_backup.lua" };
            if (dlg.ShowDialog() == true) { File.Copy(src, dlg.FileName, true); TbExportStatus.Text = "exported sandbox ✓"; }
        }
        private void ShowMissing(string p) => MessageBox.Show($"file not found:\n{p}\n\ncheck Settings.", "not found", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
