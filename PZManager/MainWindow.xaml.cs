// main_window.xaml.cs — the shell. sidebar nav, title bar, page cache.
using PZManager.Models;
using PZManager.Services;
using PZManager.Views;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PZManager
{
    public partial class MainWindow : Window
    {
        public static AppSettings AppSettings { get; private set; } = SettingsService.Load();

        private readonly Dictionary<string, Page> _page_cache = new();
        private readonly List<Button> _nav_buttons = new();

        public MainWindow()
        {
            InitializeComponent();
            _nav_buttons.AddRange(new[] { NavMods, NavSandbox, NavModSandbox, NavConfig, NavConsole, NavRestart, NavBackups, NavExport, NavSettings, NavAbout });
            TbConfigName.Text = AppSettings.ServerConfigName;
            NavigateTo("Mods");

            // check for updates in the background — don't block startup for a network call
            _ = CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            var info = await UpdateService.CheckAsync();
            if (info == null || !info.UpdateAvailable) return;

            // bring the banner in on the UI thread
            Dispatcher.Invoke(() =>
            {
                TbUpdateMsg.Text    = $"Update available  —  {info.LatestTag}  (you have {info.CurrentVersion})";
                UpdateBanner.Visibility = Visibility.Visible;
            });
        }

        private void UpdateBanner_Click(object sender, MouseButtonEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(UpdateService.ReleasesPage) { UseShellExecute = true }); }
            catch { }
        }

        private void NavigateTo(string tag)
        {
            if (!_page_cache.TryGetValue(tag, out var page))
            {
                page = tag switch
                {
                    "Mods"       => new ModsPage(),
                    "Sandbox"    => new SandboxPage(),
                    "ModSandbox" => new ModSandboxPage(),
                    "Config"     => new ServerConfigPage(),
                    "Console"  => new ConsolePage(),
                    "Restart"    => new AutoRestartPage(),
                    "Backups"    => new BackupPage(),
                    "Export"     => new ExportPage(),
                    "Settings" => new SettingsPage(),
                    "About"    => new AboutPage(),
                    _          => new ModsPage()
                };
                _page_cache[tag] = page;
            }
            MainFrame.Navigate(page);
            foreach (var btn in _nav_buttons)
                btn.Style = btn.Tag?.ToString() == tag
                    ? (Style)FindResource("NavButtonActive")
                    : (Style)FindResource("NavButton");
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag) NavigateTo(tag);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void CloseBtn_Click(object sender, RoutedEventArgs e)    => Application.Current.Shutdown();
    }
}
