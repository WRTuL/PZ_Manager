// mod_info_service.cs
// reads mod.info files from the workshop download folder.
// this is where the authoritative version and mod-level dependencies live.
// path: {ServerDir}\steamapps\workshop\content\108600\{workshopId}\mods\{modId}\mod.info
// format: simple key=value, one per line (INI-ish but without sections)
using System.IO;

namespace PZManager.Services
{
    public record ModInfoData(string Version, List<string> Requires);

    public static class ModInfoService
    {
        private const string PZ_APP_ID = "108600";

        /// <summary>
        /// Reads version and require= from the mod.info file for a given workshop + mod ID.
        /// Returns null if the file doesn't exist (server hasn't run yet / mod not downloaded).
        /// </summary>
        public static ModInfoData? ReadModInfo(string server_directory, string workshop_id, string mod_folder_id)
        {
            if (string.IsNullOrWhiteSpace(workshop_id) || string.IsNullOrWhiteSpace(mod_folder_id))
                return null;

            var workshop_root = Path.Combine(server_directory,
                "steamapps", "workshop", "content", PZ_APP_ID, workshop_id);

            if (!Directory.Exists(workshop_root)) return null;

            // search recursively — mod authors sometimes nest mod.info differently
            var mod_info_files = Directory.GetFiles(workshop_root, "mod.info", SearchOption.AllDirectories);
            if (mod_info_files.Length == 0) return null;

            // prefer the one whose parent folder matches the mod_folder_id
            var best = mod_info_files
                .OrderBy(f => Path.GetDirectoryName(f)?.EndsWith(mod_folder_id, StringComparison.OrdinalIgnoreCase) == true ? 0 : 1)
                .First();

            return ParseModInfo(best);
        }

        /// <summary>
        /// Reads all mod.info files found under a workshop item — handles multi-mod packages.
        /// </summary>
        public static List<ModInfoData> ReadAllModInfos(string server_directory, string workshop_id)
        {
            var workshop_root = Path.Combine(server_directory,
                "steamapps", "workshop", "content", PZ_APP_ID, workshop_id);

            if (!Directory.Exists(workshop_root)) return new();

            return Directory.GetFiles(workshop_root, "mod.info", SearchOption.AllDirectories)
                .Select(ParseModInfo)
                .Where(d => d != null)
                .Cast<ModInfoData>()
                .ToList();
        }

        private static ModInfoData? ParseModInfo(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var version  = "";
                var requires = new List<string>();

                foreach (var raw_line in File.ReadAllLines(path))
                {
                    var line = raw_line.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

                    var eq = line.IndexOf('=');
                    if (eq < 1) continue;

                    var key = line[..eq].Trim().ToLowerInvariant();
                    var val = line[(eq + 1)..].Trim();

                    switch (key)
                    {
                        case "modversion":
                        case "version":
                            version = val;
                            break;

                        case "require":
                        case "requires":
                            // semicolon-separated list of mod folder IDs
                            var parts = val.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var p in parts)
                            {
                                var clean = p.Trim();
                                if (!string.IsNullOrWhiteSpace(clean) && !requires.Contains(clean))
                                    requires.Add(clean);
                            }
                            break;
                    }
                }

                return new ModInfoData(version, requires);
            }
            catch { return null; }
        }
    }
}
