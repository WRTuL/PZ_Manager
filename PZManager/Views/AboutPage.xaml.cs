// about_page.xaml.cs — what is this thing and why does it exist
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;

namespace PZManager.Views
{
    public partial class AboutPage : Page
    {
        public AboutPage()
        {
            InitializeComponent();
            TbSettingsPath.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PZManager", "settings.json");
            TbVersion.Text = GitInfo.FullVersion;
        }

        private void Discord_Click(object sender, MouseButtonEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://discord.com/users/335549600012435456") { UseShellExecute = true }); }
            catch { }
        }

        private void License_Click(object sender, MouseButtonEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://www.gnu.org/licenses/gpl-3.0.html") { UseShellExecute = true }); }
            catch { }
        }
    }
}
