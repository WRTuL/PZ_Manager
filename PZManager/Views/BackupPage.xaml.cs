// backup_page.xaml.cs
// lists all backups newest-first, lets you restore or delete individual ones,
// and fires a manual backup whenever you want.
using PZManager.Models;
using PZManager.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PZManager.Views
{
    public partial class BackupPage : Page
    {
        // static so AutoRestartService can call TakeAutoBackup() without a page reference
        private static int  _keep_count   = 10;
        private static bool _auto_enabled = true;

        public BackupPage()
        {
            InitializeComponent();
            TbBackupPath.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PZManager", "Backups");
            Loaded += (_, _) => Refresh();
        }

        // ── public API for AutoRestartService ─────────────────────────────────────

        public static bool AutoBackupEnabled => _auto_enabled;

        public static void TakeAutoBackup(AppSettings settings)
        {
            if (!_auto_enabled) return;
            BackupService.CreateBackup(settings, "auto-restart");
            BackupService.Rotate(_keep_count);
        }

        // ── backup now ────────────────────────────────────────────────────────────

        private void BackupNow_Click(object sender, RoutedEventArgs e)
        {
            var result = BackupService.CreateBackup(MainWindow.AppSettings);
            if (result != null)
            {
                ReadSettings();
                BackupService.Rotate(_keep_count);
                Refresh();
                TbStatus.Text = "backup created ✓";
            }
            else
            {
                TbStatus.Text = "nothing to back up — check config directory in Settings";
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PZManager", "Backups");
            Directory.CreateDirectory(path);
            Process.Start("explorer.exe", path);
        }

        // ── list builder ──────────────────────────────────────────────────────────

        private void Refresh()
        {
            ReadSettings();
            var backups = BackupService.ListBackups();

            BackupList.Children.Clear();
            PanelEmpty.Visibility  = backups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            BackupList.Visibility  = backups.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            TbCount.Text           = $"{backups.Count} backup{(backups.Count == 1 ? "" : "s")}";

            foreach (var b in backups)
                BackupList.Children.Add(BuildCard(b));
        }

        private void ReadSettings()
        {
            _auto_enabled = CbAutoBackup.IsChecked == true;
            if (int.TryParse(TbKeep.Text, out var k) && k > 0) _keep_count = k;
        }

        private UIElement BuildCard(BackupEntry backup)
        {
            var is_auto = backup.Folder.Contains("auto-restart");

            var card = new Border
            {
                Style  = (Style)FindResource("SectionCard"),
                Margin = new Thickness(0, 0, 0, 8),
            };

            var outer = new Grid();
            outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            card.Child = outer;

            // left: timestamp + tags + size
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(left, 0);

            // top row: timestamp + auto badge
            var top_row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            top_row.Children.Add(new TextBlock
            {
                Text       = backup.Timestamp == DateTime.MinValue
                             ? Path.GetFileName(backup.Folder)
                             : backup.Timestamp.ToString("ddd dd MMM yyyy  —  HH:mm:ss"),
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                FontSize   = 13,
                VerticalAlignment = VerticalAlignment.Center,
            });
            if (is_auto)
                top_row.Children.Add(new Border
                {
                    Background    = (Brush)FindResource("BgActiveBrush"),
                    BorderBrush   = (Brush)FindResource("BorderAccentBrush"),
                    BorderThickness = new Thickness(0.5),
                    CornerRadius  = new CornerRadius(3),
                    Padding       = new Thickness(5, 1, 5, 1),
                    Margin        = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text       = "auto",
                        FontSize   = 9,
                        Foreground = (Brush)FindResource("TextMutedBrush"),
                    }
                });
            left.Children.Add(top_row);

            // bottom row: content tags + size
            var tag_row = new StackPanel { Orientation = Orientation.Horizontal };
            if (backup.HasIni)      tag_row.Children.Add(MakeTag("config"));
            if (backup.HasSandbox)  tag_row.Children.Add(MakeTag("sandbox"));
            if (backup.HasModList)  tag_row.Children.Add(MakeTag("mods"));
            tag_row.Children.Add(new TextBlock
            {
                Text       = "  " + BackupService.FormatSize(backup.Folder),
                Foreground = (Brush)FindResource("TextDimBrush"),
                FontSize   = 11,
                VerticalAlignment = VerticalAlignment.Center,
            });
            left.Children.Add(tag_row);

            outer.Children.Add(left);

            // right: action buttons
            var right = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(12, 0, 0, 0),
            };
            Grid.SetColumn(right, 1);

            var restore_btn = MakeButton("Restore…", "DarkButton");
            restore_btn.Click += (_, _) => RestoreDialog(backup);
            right.Children.Add(restore_btn);

            var delete_btn = MakeButton("✕", "DangerButton", width: 32);
            delete_btn.Margin  = new Thickness(6, 0, 0, 0);
            delete_btn.ToolTip = "Delete this backup";
            delete_btn.Click  += (_, _) => DeleteBackup(backup);
            right.Children.Add(delete_btn);

            outer.Children.Add(right);
            return card;
        }

        // ── restore dialog ────────────────────────────────────────────────────────

        private void RestoreDialog(BackupEntry backup)
        {
            var dlg = new Window
            {
                Title           = "Restore Backup",
                Width           = 420,
                Height          = 280,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background      = (Brush)FindResource("BgPrimaryBrush"),
                WindowStyle     = WindowStyle.ToolWindow,
                ResizeMode      = ResizeMode.NoResize,
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text         = $"Restore from {backup.Timestamp:ddd dd MMM  HH:mm:ss}",
                FontSize     = 14,
                Foreground   = (Brush)FindResource("TextPrimaryBrush"),
                Margin       = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(new TextBlock
            {
                Text         = "Choose what to restore. This overwrites your current files.",
                FontSize     = 12,
                Foreground   = (Brush)FindResource("TextMutedBrush"),
                Margin       = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap,
            });

            var cb_ini     = MakeCheckbox("Server config (.ini)",        backup.HasIni);
            var cb_sandbox = MakeCheckbox("Sandbox vars (.lua)",          backup.HasSandbox);
            var cb_mods    = MakeCheckbox("Mod list (Mods= / WorkshopItems=)", backup.HasModList);
            panel.Children.Add(cb_ini);
            panel.Children.Add(cb_sandbox);
            panel.Children.Add(cb_mods);

            var btn_row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0),
            };
            var btn_ok     = new Button { Content = "Restore", Style = (Style)FindResource("GreenButton"), Width = 90, Margin = new Thickness(0, 0, 6, 0) };
            var btn_cancel = new Button { Content = "Cancel",  Style = (Style)FindResource("DarkButton"),  Width = 80 };
            btn_row.Children.Add(btn_ok);
            btn_row.Children.Add(btn_cancel);
            panel.Children.Add(btn_row);

            dlg.Content      = panel;
            btn_cancel.Click += (_, _) => dlg.Close();
            btn_ok.Click     += (_, _) =>
            {
                var s = MainWindow.AppSettings;
                var errors = new List<string>();
                try { if (cb_ini.IsChecked     == true) BackupService.RestoreIni(backup, s);     } catch (Exception ex) { errors.Add("config: " + ex.Message); }
                try { if (cb_sandbox.IsChecked == true) BackupService.RestoreSandbox(backup, s); } catch (Exception ex) { errors.Add("sandbox: " + ex.Message); }
                try { if (cb_mods.IsChecked    == true) BackupService.RestoreModList(backup, s); } catch (Exception ex) { errors.Add("mods: " + ex.Message); }
                dlg.Close();
                TbStatus.Text = errors.Count == 0 ? "restored ✓" : $"errors: {string.Join("; ", errors)}";
            };
            dlg.ShowDialog();
        }

        // ── delete ────────────────────────────────────────────────────────────────

        private void DeleteBackup(BackupEntry backup)
        {
            var ts  = backup.Timestamp == DateTime.MinValue ? Path.GetFileName(backup.Folder) : backup.Timestamp.ToString("HH:mm:ss dd MMM");
            var res = MessageBox.Show($"Delete backup from {ts}?", "Delete Backup",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;
            try { BackupService.DeleteBackup(backup); Refresh(); TbStatus.Text = "deleted."; }
            catch (Exception ex) { TbStatus.Text = $"error: {ex.Message}"; }
        }

        // ── ui helpers ────────────────────────────────────────────────────────────

        private Border MakeTag(string text) => new()
        {
            Background      = (Brush)FindResource("BgHoverBrush"),
            BorderBrush     = (Brush)FindResource("BorderSubtleBrush"),
            BorderThickness = new Thickness(0.5),
            CornerRadius    = new CornerRadius(3),
            Padding         = new Thickness(5, 1, 5, 1),
            Margin          = new Thickness(0, 0, 4, 0),
            Child           = new TextBlock
            {
                Text       = text,
                FontSize   = 10,
                Foreground = (Brush)FindResource("TextDimBrush"),
            }
        };

        private Button MakeButton(string label, string style, double width = 80) => new()
        {
            Content = label,
            Style   = (Style)FindResource(style),
            Width   = width,
            Height  = 28,
        };

        private CheckBox MakeCheckbox(string label, bool enabled) => new()
        {
            Content   = label,
            IsChecked = enabled,
            IsEnabled = enabled,
            Margin    = new Thickness(0, 0, 0, 8),
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            FontSize  = 13,
        };
    }
}
