// backup_service.cs
// creates timestamped snapshots of the three files that matter:
//   servertest.ini        — server config
//   servertest_SandboxVars.lua  — sandbox settings
//   modlist.txt           — extracted Mods= and WorkshopItems= lines
//
// world saves are NOT backed up here. they're enormous and have their own location.
// if you want world backups, point a separate tool at %userprofile%\Zomboid\Saves.
// that's not our problem today.
//
// backups rotate automatically so you don't end up with 500 folders after a week of 6-hour restarts.
// restore is selective — you can restore just the mod list without touching the sandbox config, etc.
using PZManager.Models;
using System.IO;
using System.Text.Json;

namespace PZManager.Services
{
    public record BackupEntry(
        string Folder,
        DateTime Timestamp,
        bool HasIni,
        bool HasSandbox,
        bool HasModList
    );

    public static class BackupService
    {
        private static string BackupRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PZManager", "Backups");

        // ── create ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a timestamped backup of ini, sandbox lua, and mod list.
        /// Returns the folder path on success, null on failure.
        /// </summary>
        public static string? CreateBackup(AppSettings settings, string? label = null)
        {
            var ts         = DateTime.Now;
            var folder_name = ts.ToString("yyyy-MM-dd_HH-mm-ss");
            if (!string.IsNullOrWhiteSpace(label))
                folder_name += "_" + SanitiseName(label);

            var folder = Path.Combine(BackupRoot, folder_name);
            Directory.CreateDirectory(folder);

            var config_dir  = settings.ConfigDirectory;
            var config_name = settings.ServerConfigName;
            var ini_path    = Path.Combine(config_dir, config_name + ".ini");
            var lua_path    = Path.Combine(config_dir, config_name + "_SandboxVars.lua");

            bool any = false;

            // ini
            if (File.Exists(ini_path))
            {
                File.Copy(ini_path, Path.Combine(folder, config_name + ".ini"), overwrite: true);
                any = true;

                // extract and save mod list separately for easy reading
                var (mods_raw, ws_raw, _, _) = IniService.ReadModLines(ini_path);
                var mod_lines = new[] { $"Mods={mods_raw}", $"WorkshopItems={ws_raw}" };
                File.WriteAllLines(Path.Combine(folder, "modlist.txt"), mod_lines);
            }

            // sandbox vars
            if (File.Exists(lua_path))
            {
                File.Copy(lua_path, Path.Combine(folder, config_name + "_SandboxVars.lua"), overwrite: true);
                any = true;
            }

            // metadata for display
            var meta = new { Timestamp = ts, Label = label ?? "", ConfigName = config_name };
            File.WriteAllText(Path.Combine(folder, "meta.json"),
                JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

            if (!any)
            {
                // nothing to back up — clean up empty folder
                Directory.Delete(folder, recursive: true);
                return null;
            }

            return folder;
        }

        // ── list ──────────────────────────────────────────────────────────────────

        public static List<BackupEntry> ListBackups()
        {
            if (!Directory.Exists(BackupRoot)) return new();

            return Directory.GetDirectories(BackupRoot)
                .Select(folder =>
                {
                    var name = Path.GetFileName(folder);
                    // parse timestamp from folder name (first 19 chars: yyyy-MM-dd_HH-mm-ss)
                    DateTime ts = DateTime.MinValue;
                    if (name.Length >= 19)
                        DateTime.TryParseExact(name[..19], "yyyy-MM-dd_HH-mm-ss",
                            null, System.Globalization.DateTimeStyles.None, out ts);

                    var files   = Directory.GetFiles(folder).Select(Path.GetFileName).ToHashSet();
                    return new BackupEntry(
                        Folder:      folder,
                        Timestamp:   ts,
                        HasIni:      files.Any(f => f?.EndsWith(".ini") == true),
                        HasSandbox:  files.Any(f => f?.EndsWith(".lua") == true),
                        HasModList:  files.Contains("modlist.txt")
                    );
                })
                .OrderByDescending(b => b.Timestamp)
                .ToList();
        }

        // ── rotate ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Deletes oldest backups keeping only the most recent <paramref name="keep"/> entries.
        /// </summary>
        public static void Rotate(int keep = 10)
        {
            var all = ListBackups(); // already sorted newest-first
            foreach (var old in all.Skip(keep))
            {
                try { Directory.Delete(old.Folder, recursive: true); } catch { }
            }
        }

        // ── restore ───────────────────────────────────────────────────────────────

        public static void RestoreIni(BackupEntry backup, AppSettings settings)
        {
            var src = Directory.GetFiles(backup.Folder, "*.ini").FirstOrDefault();
            if (src == null) throw new FileNotFoundException("No .ini found in backup.");
            var dst = Path.Combine(settings.ConfigDirectory, settings.ServerConfigName + ".ini");
            File.Copy(src, dst, overwrite: true);
        }

        public static void RestoreSandbox(BackupEntry backup, AppSettings settings)
        {
            var src = Directory.GetFiles(backup.Folder, "*.lua").FirstOrDefault();
            if (src == null) throw new FileNotFoundException("No .lua found in backup.");
            var dst = Path.Combine(settings.ConfigDirectory, settings.ServerConfigName + "_SandboxVars.lua");
            File.Copy(src, dst, overwrite: true);
        }

        public static void RestoreModList(BackupEntry backup, AppSettings settings)
        {
            var src = Path.Combine(backup.Folder, "modlist.txt");
            if (!File.Exists(src)) throw new FileNotFoundException("No modlist.txt found in backup.");

            var lines = File.ReadAllLines(src);
            var mods  = lines.FirstOrDefault(l => l.StartsWith("Mods="))?["Mods=".Length..] ?? "";
            var ws    = lines.FirstOrDefault(l => l.StartsWith("WorkshopItems="))?["WorkshopItems=".Length..] ?? "";
            var ini   = Path.Combine(settings.ConfigDirectory, settings.ServerConfigName + ".ini");
            IniService.SaveModLines(ini, mods, ws);
        }

        public static void DeleteBackup(BackupEntry backup)
            => Directory.Delete(backup.Folder, recursive: true);

        // ── helpers ───────────────────────────────────────────────────────────────

        private static string SanitiseName(string s)
            => new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        public static string FormatSize(string folder)
        {
            try
            {
                var bytes = Directory.GetFiles(folder).Sum(f => new FileInfo(f).Length);
                return bytes < 1024 ? $"{bytes} B"
                     : bytes < 1024 * 1024 ? $"{bytes / 1024.0:F1} KB"
                     : $"{bytes / 1024.0 / 1024.0:F1} MB";
            }
            catch { return ""; }
        }
    }
}
