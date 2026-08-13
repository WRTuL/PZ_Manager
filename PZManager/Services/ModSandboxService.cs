// mod_sandbox_service.cs
// parses SandboxVars.lua as nested lua tables to find mod settings.
//
// B42 stores mod sandbox options like this:
//   SandboxVars.ModName = { OptionName = value, OtherOption = value, }
//
// vanilla options are a flat table at the top level.
// mod options are nested tables with the mod name as the key.
// this is actually a reasonable design. we don't have to be happy about everything.
//
// the vanilla key list is hardcoded. yes that means it needs updating when TiS adds new fields.
// yes that's annoying. yes there's probably a better way. no i'm not doing it right now. again, cry about it.
using PZManager.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace PZManager.Services
{
    public static class ModSandboxService
    {
        // vanilla top-level table keys — ignore these, the Sandbox page handles them
        private static readonly HashSet<string> _vanilla_tables = new(StringComparer.OrdinalIgnoreCase)
        {
            "ZombieLore", "ZombieConfig", "MultiplierConfig", "Map",
            "Basement", "ProximityInventory", "SandboxOptions",
        };

        // regex to extract a named nested table:   TableName = {\n...\n},
        private static readonly Regex _table_regex = new(
            @"^\s{4}(\w+)\s*=\s*\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}",
            RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);

        // regex to extract one field line inside a table (with optional preceding comment)
        // matches:   FieldName = value,      (value can be number, bool, string)
        private static readonly Regex _field_regex = new(
            @"^\s+(\w+)\s*=\s*(true|false|""[^""]*""|[-\d\.]+),?\s*$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // regex to pull Min/Max/Default from the preceding comment line
        // e.g.   -- Min: 0 Max: 100 Default: 25
        private static readonly Regex _range_regex = new(
            @"Min:\s*([-\d\.]+).*?Max:\s*([-\d\.]+)(?:.*?Default:\s*([-\d\.]+))?",
            RegexOptions.Compiled);

        // ── public entry points ───────────────────────────────────────────────────

        /// <summary>
        /// Parses the live SandboxVars.lua and returns one ModSandboxSection
        /// per top-level table that is NOT in the vanilla set.
        /// Field types, ranges, and defaults are inferred from the lua values
        /// and inline comments PZ generates automatically.
        /// </summary>
        public static List<ModSandboxSection> ScanModTables(string sandbox_lua_path)
        {
            if (!File.Exists(sandbox_lua_path)) return new();

            var content = File.ReadAllText(sandbox_lua_path);
            var sections = new List<ModSandboxSection>();

            foreach (Match tm in _table_regex.Matches(content))
            {
                var table_name = tm.Groups[1].Value;
                if (_vanilla_tables.Contains(table_name)) continue;

                var body    = tm.Groups[2].Value;
                var options = ParseTableBody(table_name, body);
                if (options.Count == 0) continue;

                sections.Add(new ModSandboxSection
                {
                    ModId      = table_name,
                    ModName    = table_name,
                    WorkshopId = "",
                    Options    = options,
                });
            }

            return sections;
        }

        /// <summary>
        /// Writes modified mod table values back into SandboxVars.lua in-place.
        /// Only updates keys that already exist — never injects new ones.
        /// </summary>
        public static void SaveModTables(string sandbox_lua_path, IEnumerable<ModSandboxSection> sections)
        {
            if (!File.Exists(sandbox_lua_path)) return;
            var content = File.ReadAllText(sandbox_lua_path);

            foreach (var section in sections)
            foreach (var opt in section.Options)
            {
                var lua_val = FormatValue(opt);
                // match "    FieldName = <anything>," inside the file
                var pat = new Regex(
                    $@"(\b{Regex.Escape(opt.OptionName)}\s*=\s*)([^,\r\n]+)(,?)",
                    RegexOptions.Compiled);
                content = pat.Replace(content, m => m.Groups[1].Value + lua_val + m.Groups[3].Value, 1);
            }

            File.WriteAllText(sandbox_lua_path, content);
        }

        // ── parsing ───────────────────────────────────────────────────────────────

        private static List<ModSandboxOption> ParseTableBody(string table_name, string body)
        {
            var options = new List<ModSandboxOption>();

            // split into lines so we can look at the comment above each field
            var lines = body.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var fm   = _field_regex.Match(line);
                if (!fm.Success) continue;

                var key   = fm.Groups[1].Value;
                var raw   = fm.Groups[2].Value.Trim().TrimEnd(',');

                // look back up to 5 lines for a -- comment containing Min:/Max:/Default:
                double? min = null, max = null;
                string? def_str = null;

                for (int j = i - 1; j >= Math.Max(0, i - 5); j--)
                {
                    var comment = lines[j].Trim();
                    if (!comment.StartsWith("--")) continue;
                    var rm = _range_regex.Match(comment);
                    if (rm.Success)
                    {
                        if (double.TryParse(rm.Groups[1].Value, out var mn)) min = mn;
                        if (double.TryParse(rm.Groups[2].Value, out var mx)) max = mx;
                        if (rm.Groups[3].Success && double.TryParse(rm.Groups[3].Value, out var df)) def_str = rm.Groups[3].Value;
                        break;
                    }
                }

                var type = InferType(raw);

                var opt = new ModSandboxOption
                {
                    FullKey      = $"{table_name}.{key}",
                    ModName      = table_name,
                    OptionName   = key,
                    Label        = SplitCamelCase(key),
                    Type         = type,
                    Min          = min ?? DefaultMin(type, raw),
                    Max          = max ?? DefaultMax(type, raw),
                    DefaultValue = def_str ?? raw,
                    CurrentValue = raw.Trim('"'),
                    EnumValues   = new(),
                };

                options.Add(opt);
            }

            return options;
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        private static ModOptionType InferType(string raw)
        {
            if (raw == "true" || raw == "false") return ModOptionType.Boolean;
            if (raw.StartsWith('"'))             return ModOptionType.String;
            if (raw.Contains('.'))               return ModOptionType.Double;
            return ModOptionType.Integer;
        }

        private static double DefaultMin(ModOptionType t, string raw) => 0;

        private static double DefaultMax(ModOptionType t, string raw)
        {
            if (t == ModOptionType.Boolean) return 1;
            if (double.TryParse(raw, out var d))
            {
                if (d <= 0)   return 10;
                if (d <= 1)   return 10;
                if (d <= 10)  return 100;
                if (d <= 100) return 500;
                return Math.Ceiling(d * 5);
            }
            return 100;
        }

        private static string FormatValue(ModSandboxOption opt)
        {
            return opt.Type switch
            {
                ModOptionType.Boolean => opt.CurrentValue == "true" ? "true" : "false",
                ModOptionType.Double  => double.TryParse(opt.CurrentValue, out var d)
                                        ? d.ToString("F2") : opt.DefaultValue,
                ModOptionType.Integer => int.TryParse(opt.CurrentValue, out var i)
                                        ? i.ToString() : opt.DefaultValue,
                _                     => $"\"{opt.CurrentValue}\"",
            };
        }

        private static string SplitCamelCase(string s)
        {
            // "EnableCataclysmHorde" -> "Enable Cataclysm Horde"
            // "General_KillCounter" -> "Kill Counter"  (strip prefix)
            var us = s.IndexOf('_');
            if (us > 0) s = s[(us + 1)..];
            return Regex.Replace(s, @"(?<=[a-z])(?=[A-Z])", " ");
        }
    }
}
