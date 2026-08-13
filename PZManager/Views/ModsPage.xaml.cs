using Microsoft.Win32;
using PZManager.Models;
using PZManager.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace PZManager.Views
{
    public partial class ModsPage : Page
    {
        private readonly ObservableCollection<ModEntry> _mods = new();

        public ModsPage()
        {
            InitializeComponent();
            ModGrid.ItemsSource = _mods;
            _mods.CollectionChanged += (_, _) => RefreshOutput();
        }

        private void AddIds_Click(object sender, RoutedEventArgs e)
        {
            var raw = TbModInput.Text;
            if (string.IsNullOrWhiteSpace(raw)) return;
            var ids = raw.Split(new[] { ';', '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s) && s.All(char.IsDigit)).Distinct();
            bool added = false;
            foreach (var id in ids)
            {
                if (_mods.Any(m => m.WorkshopId == id)) continue;
                _mods.Add(new ModEntry { WorkshopId = id, Status = FetchStatus.Idle });
                added = true;
            }
            if (added) { TbModInput.Clear(); RefreshOutput(); UpdateCount(); if (MainWindow.AppSettings.AutoFetchModNames) _ = FetchNewModsAsync(); }
        }

        private async void LookupAll_Click(object sender, RoutedEventArgs e)
        { BtnLookup.IsEnabled = false; await FetchAllModsAsync(); BtnLookup.IsEnabled = true; }

        private async Task FetchAllModsAsync() => await FetchModListAsync(_mods.Where(m => m.Status != FetchStatus.Ok).ToList());
        private async Task FetchNewModsAsync()  => await FetchModListAsync(_mods.Where(m => m.Status == FetchStatus.Idle).ToList());

        private async Task FetchModListAsync(List<ModEntry> entries)
        {
            if (entries.Count == 0) return;
            foreach (var m in entries) m.Status = FetchStatus.Fetching;
            SetStatus($"fetching {entries.Count} mod(s)…");

            var progress = new Progress<(string id, ModScrapeResult result)>(r =>
            {
                var entry = _mods.FirstOrDefault(m => m.WorkshopId == r.id);
                if (entry == null) return;

                if (r.result.DisplayName != null)
                    entry.ScrapedName = r.result.DisplayName;

                // auto-fill Mod ID from page — only if user hasn't already set one
                if (r.result.ModIds.Count > 0 && string.IsNullOrWhiteSpace(entry.ModFolderId))
                    entry.ModFolderId = string.Join(";", r.result.ModIds);

                // merge Steam dependencies with any mod.info require= deps already loaded
                var all_deps = new List<string>(entry.Dependencies);
                foreach (var dep in r.result.SteamDependencies)
                    if (!all_deps.Contains(dep)) all_deps.Add(dep);
                entry.Dependencies = all_deps;

                entry.Status = r.result.DisplayName != null || r.result.ModIds.Count > 0
                    ? FetchStatus.Ok : FetchStatus.Failed;

                // also try to load version + mod-level deps from disk (if server has run)
                ReadModInfoFromDisk(entry);

                RefreshOutput();
            });

            await ModScraperService.FetchManyAsync(entries.Select(m => m.WorkshopId), progress);
            SetStatus("");
        }

        private void ReadModInfoFromDisk(ModEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.WorkshopId)) return;
            var s    = MainWindow.AppSettings;
            var info = ModInfoService.ReadModInfo(s.ServerDirectory, entry.WorkshopId, entry.ModFolderId);
            if (info == null) return;

            if (!string.IsNullOrWhiteSpace(info.Version))
                entry.Version = info.Version;

            // merge mod.info require= deps (these are mod folder IDs, not workshop IDs)
            if (info.Requires.Count > 0)
            {
                var all_deps = new List<string>(entry.Dependencies);
                foreach (var dep in info.Requires)
                    if (!all_deps.Contains(dep)) all_deps.Add(dep);
                entry.Dependencies = all_deps;
            }
        }

        private void ImportIni_Click(object sender, RoutedEventArgs e)
        {
            var initial_dir = MainWindow.AppSettings.ConfigDirectory;
            var dlg = new OpenFileDialog { Filter = "INI files|*.ini|All files|*.*", Title = "Select server .ini file" };
            if (Directory.Exists(initial_dir)) dlg.InitialDirectory = initial_dir;
            if (dlg.ShowDialog() != true) return;
            var (_, _, folder_ids, ws_ids) = IniService.ReadModLines(dlg.FileName);
            _mods.Clear();
            for (int i = 0; i < ws_ids.Count; i++)
                _mods.Add(new ModEntry { WorkshopId = ws_ids[i], Status = FetchStatus.Idle, ModFolderId = i < folder_ids.Count ? folder_ids[i] : "" });
            if (ws_ids.Count == 0 && folder_ids.Count > 0)
                foreach (var fid in folder_ids) _mods.Add(new ModEntry { ModFolderId = fid, Status = FetchStatus.Idle });
            RefreshOutput(); UpdateCount(); SetSaveStatus("");
            // read version + deps from disk for mods already downloaded
            foreach (var mod in _mods) ReadModInfoFromDisk(mod);
            if (MainWindow.AppSettings.AutoFetchModNames && ws_ids.Count > 0) _ = FetchAllModsAsync();
        }

        private void SaveIni_Click(object sender, RoutedEventArgs e)
        {
            var ini_path = Path.Combine(MainWindow.AppSettings.ConfigDirectory, MainWindow.AppSettings.ServerConfigName + ".ini");
            if (!File.Exists(ini_path))
            {
                MessageBox.Show($"can't find:\n{ini_path}\n\ncheck Settings.", "file not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var missing = _mods.Where(m => string.IsNullOrWhiteSpace(m.ModFolderId)).ToList();
            if (missing.Count > 0)
            {
                var names = string.Join("\n  • ", missing.Select(m => m.ScrapedName.Length > 0 ? m.ScrapedName : m.WorkshopId));
                if (MessageBox.Show($"{missing.Count} mod(s) missing Mod ID:\n\n  • {names}\n\nsave anyway?",
                    "missing mod ids", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            }
            IniService.SaveModLines(ini_path, BuildModsLine(), BuildWorkshopLine());
            SetSaveStatus($"✓ saved {_mods.Count} mod(s) to {Path.GetFileName(ini_path)}");
            SetStatus("");
        }

        private void CopyOutput_Click(object sender, RoutedEventArgs e) { Clipboard.SetText($"{TbModsLine.Text}\n{TbWorkshopLine.Text}"); SetStatus("copied ✓"); }
        private void DeleteMod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            { var entry = _mods.FirstOrDefault(m => m.WorkshopId == id); if (entry != null) { _mods.Remove(entry); RefreshOutput(); UpdateCount(); } }
        }
        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("clear all mods?", "confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            { _mods.Clear(); RefreshOutput(); UpdateCount(); SetSaveStatus(""); }
        }

        private string BuildModsLine()    => string.Join(";", _mods.Select(m => m.ModFolderId.Trim()));
        private string BuildWorkshopLine() => string.Join(";", _mods.Select(m => m.WorkshopId));

        private void RefreshOutput()
        {
            TbModsLine.Text     = $"Mods={BuildModsLine()}";
            TbWorkshopLine.Text = $"WorkshopItems={BuildWorkshopLine()}";
            var blank = _mods.Count(m => string.IsNullOrWhiteSpace(m.ModFolderId));
            TbWarning.Text       = blank > 0 && _mods.Count > 0 ? $"⚠  {blank} mod(s) missing Mod ID — fill the green column before saving." : "";
            TbWarning.Visibility = blank > 0 && _mods.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateCount()          => TbModCount.Text  = $"{_mods.Count} mod{(_mods.Count == 1 ? "" : "s")}";
        private void SetStatus(string msg)  => TbStatus.Text     = msg;
        private void SetSaveStatus(string msg) => TbSaveStatus.Text = msg;
    }
}
