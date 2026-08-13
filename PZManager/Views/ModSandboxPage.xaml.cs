// mod_sandbox_page.xaml.cs
// auto-scans SandboxVars.lua on page load.
// cross-references the mod list from the ini — mods with tables get full tabs,
// mods without yet (not run once) get a dimmed placeholder.
// workflow: add mods → start server → stop server → open this page → done.
using PZManager.Models;
using PZManager.Services;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PZManager.Views
{
    public partial class ModSandboxPage : Page
    {
        private List<ModSandboxSection> _sections  = new();
        private bool _has_scanned = false;

        public ModSandboxPage()
        {
            InitializeComponent();
            // scan when the page first becomes visible.
            // IsVisibleChanged fires on every navigation, but _has_scanned gates it to once.
            // clicking Refresh resets _has_scanned so the next visibility change re-scans.
            IsVisibleChanged += (_, e) =>
            {
                if ((bool)e.NewValue && !_has_scanned)
                    _ = ScanAsync();
            };
        }

        // ── scan ─────────────────────────────────────────────────────────────────

        private void Scan_Click(object sender, RoutedEventArgs e)
        {
            _has_scanned = false; // allow re-scan even if we already did one
            _ = ScanAsync();
        }

        private async Task ScanAsync()
        {
            var s           = MainWindow.AppSettings;
            var sandbox_lua = Path.Combine(s.ConfigDirectory, s.ServerConfigName + "_SandboxVars.lua");
            var ini_path    = Path.Combine(s.ConfigDirectory, s.ServerConfigName + ".ini");

            ShowPanel("scanning");
            TbScanStatus.Text = "Reading SandboxVars.lua…";
            BtnScan.IsEnabled = false;

            if (!File.Exists(sandbox_lua))
            {
                TbScanStatus.Text =
                    $"SandboxVars.lua not found at:\n{sandbox_lua}\n\n" +
                    "Start the server once to generate it, then come back here.";
                BtnScan.IsEnabled = true;
                _has_scanned = true;
                return;
            }

            // run the scan off the UI thread — file could be large
            _sections = await Task.Run(() => ModSandboxService.ScanModTables(sandbox_lua));

            // pull the mod folder IDs from the ini so we can show placeholders for
            // mods that haven't written their lua tables yet
            var ini_mod_ids = GetIniModIds(ini_path);

            BtnScan.IsEnabled = true;
            _has_scanned      = true;

            // even if no mod tables found, still show the tab view with placeholders
            BuildTabs(ini_mod_ids);

            var active  = _sections.Count;
            var pending = ini_mod_ids.Count(id => !_sections.Any(sec =>
                sec.ModId.Equals(id, StringComparison.OrdinalIgnoreCase)));

            var badge = active > 0
                ? $"{active} mod{(active == 1 ? "" : "s")} ready"
                  + (pending > 0 ? $"  ·  {pending} pending" : "")
                : pending > 0
                    ? $"{pending} mod{(pending == 1 ? "" : "s")} pending — run server first"
                    : "no mod sandbox options found";

            TbModCount.Text   = badge;
            BtnSave.IsEnabled = active > 0;

            if (active > 0 || pending > 0)
                ShowPanel("results");
            else
            {
                TbScanStatus.Text =
                    "No mod sandbox tables found and no mods in the ini.\n" +
                    "Add mods via Mod Manager, start the server, then come back.";
            }
        }

        // ── mod list cross-reference ──────────────────────────────────────────────

        /// Returns the list of mod folder IDs from the server ini (the Mods= line).
        private static List<string> GetIniModIds(string ini_path)
        {
            if (!File.Exists(ini_path)) return new();
            var (_, _, mod_ids, _) = IniService.ReadModLines(ini_path);
            return mod_ids
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
        }

        // ── tab builder ───────────────────────────────────────────────────────────

        private void BuildTabs(List<string> ini_mod_ids)
        {
            TabControl.Items.Clear();

            // tabs for mods that have lua tables — full fields
            foreach (var section in _sections)
                TabControl.Items.Add(BuildActiveTab(section));

            // placeholder tabs for mods in the ini that haven't written lua tables yet
            // i.e. not yet run with the server
            var active_ids = _sections.Select(s => s.ModId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var mod_id in ini_mod_ids.Where(id => !active_ids.Contains(id)))
                TabControl.Items.Add(BuildPendingTab(mod_id));
        }

        private TabItem BuildActiveTab(ModSandboxSection section)
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(16, 12, 16, 12),
            };
            var panel = new StackPanel();
            scroll.Content = panel;

            foreach (var opt in section.Options)
                panel.Children.Add(BuildOptionRow(opt));

            return new TabItem { Header = section.ModId, Content = scroll };
        }

        private TabItem BuildPendingTab(string mod_id)
        {
            // dimmed header to signal "waiting"
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock
            {
                Text       = mod_id,
                Foreground = (Brush)FindResource("TextDimBrush"),
                FontStyle  = FontStyles.Italic,
            });
            header.Children.Add(new TextBlock
            {
                Text       = " ···",
                Foreground = (Brush)FindResource("TextDimBrush"),
                FontSize   = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin     = new Thickness(4, 0, 0, 0),
            });

            var content = new StackPanel
            {
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 40, 0, 0),
            };
            content.Children.Add(new TextBlock
            {
                Text              = $"Waiting for {mod_id}",
                FontSize          = 15,
                Foreground        = (Brush)FindResource("TextSecondaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin            = new Thickness(0, 0, 0, 10),
            });
            content.Children.Add(new TextBlock
            {
                Text              = "This mod is in your mod list but hasn't written its\nsandbox defaults yet.\n\nStart the server once with this mod loaded,\nthen click Refresh.",
                FontSize          = 13,
                Foreground        = (Brush)FindResource("TextDimBrush"),
                TextAlignment     = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                LineHeight        = 22,
            });

            return new TabItem
            {
                Header  = header,
                Content = content,
                IsEnabled = true, // still clickable so they can read the message
            };
        }

        // ── option row builders ───────────────────────────────────────────────────

        private UIElement BuildOptionRow(ModSandboxOption opt)
        {
            return opt.Type switch
            {
                ModOptionType.Boolean => BuildBoolRow(opt),
                ModOptionType.String  => BuildTextRow(opt),
                _                     => BuildSliderRow(opt),
            };
        }

        private UIElement BuildSliderRow(ModSandboxOption opt)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            var label = new TextBlock
            {
                Text              = opt.Label,
                Foreground        = (Brush)FindResource("TextMutedBrush"),
                FontSize          = 12,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip           = opt.FullKey,
                TextWrapping      = TextWrapping.Wrap,
            };

            bool is_double = opt.Type == ModOptionType.Double;
            var slider = new Slider
            {
                Minimum             = opt.Min,
                Maximum             = opt.Max,
                TickFrequency       = is_double ? (opt.Max - opt.Min) / 20.0 : 1.0,
                IsSnapToTickEnabled = !is_double,
                VerticalAlignment   = VerticalAlignment.Center,
            };

            var val_block = new TextBlock
            {
                FontFamily          = new FontFamily("Consolas, Courier New"),
                FontSize            = 13,
                FontWeight          = FontWeights.Medium,
                Foreground          = (Brush)FindResource("AccentGreenBrush"),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin              = new Thickness(10, 0, 0, 0),
                VerticalAlignment   = VerticalAlignment.Center,
            };

            if (double.TryParse(opt.CurrentValue,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var cv))
                slider.Value = Math.Clamp(cv, opt.Min, opt.Max);

            slider.ValueChanged += (_, args) =>
            {
                val_block.Text   = is_double ? args.NewValue.ToString("F2") : ((int)args.NewValue).ToString();
                opt.CurrentValue = val_block.Text;
            };
            val_block.Text   = is_double ? slider.Value.ToString("F2") : ((int)slider.Value).ToString();
            opt.CurrentValue = val_block.Text;

            Grid.SetColumn(label,     0);
            Grid.SetColumn(slider,    1);
            Grid.SetColumn(val_block, 2);
            grid.Children.Add(label);
            grid.Children.Add(slider);
            grid.Children.Add(val_block);
            return grid;
        }

        private UIElement BuildBoolRow(ModSandboxOption opt)
        {
            var cb = new CheckBox
            {
                Content   = opt.Label,
                IsChecked = opt.CurrentValue == "true",
                Margin    = new Thickness(0, 0, 0, 10),
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                FontSize  = 13,
                ToolTip   = opt.FullKey,
            };
            cb.Checked   += (_, _) => opt.CurrentValue = "true";
            cb.Unchecked += (_, _) => opt.CurrentValue = "false";
            return cb;
        }

        private UIElement BuildTextRow(ModSandboxOption opt)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text              = opt.Label,
                Foreground        = (Brush)FindResource("TextMutedBrush"),
                FontSize          = 12,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip           = opt.FullKey,
            };
            var tb = new TextBox { Text = opt.CurrentValue, VerticalAlignment = VerticalAlignment.Center };
            tb.TextChanged += (_, _) => opt.CurrentValue = tb.Text;

            Grid.SetColumn(label, 0);
            Grid.SetColumn(tb,    1);
            grid.Children.Add(label);
            grid.Children.Add(tb);
            return grid;
        }

        // ── save ─────────────────────────────────────────────────────────────────

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var sandbox_lua = Path.Combine(
                MainWindow.AppSettings.ConfigDirectory,
                MainWindow.AppSettings.ServerConfigName + "_SandboxVars.lua");

            if (!File.Exists(sandbox_lua))
            {
                MessageBox.Show($"Can't find:\n{sandbox_lua}", "File not found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                ModSandboxService.SaveModTables(sandbox_lua, _sections);
                TbModCount.Text = $"{_sections.Count} section(s) saved ✓";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── panel switcher ────────────────────────────────────────────────────────

        private void ShowPanel(string which)
        {
            PanelEmpty.Visibility    = which == "empty"    ? Visibility.Visible : Visibility.Collapsed;
            PanelScanning.Visibility = which == "scanning" ? Visibility.Visible : Visibility.Collapsed;
            TabControl.Visibility    = which == "results"  ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
