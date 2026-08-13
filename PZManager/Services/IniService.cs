// ini_service.cs
// reads and writes .ini files.
// you'd think this would be simple. you would be wrong.
// pz uses a completely flat key=value format with no sections, which would be fine,
// except the Mods= and WorkshopItems= lines are semicolon-delimited parallel arrays
// that have to stay in sync or the server just silently loads nothing.
// great design. thanks TiS.
using PZManager.Models;
using System.IO;
using System.Text;

namespace PZManager.Services
{
    public static class IniService
    {
        public static Dictionary<string, string> ReadIni(string path)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return dict;
            foreach (var line in File.ReadAllLines(path))
            {
                var t = line.Trim();
                if (string.IsNullOrEmpty(t) || t.StartsWith('#') || t.StartsWith(';')) continue;
                var idx = t.IndexOf('=');
                if (idx < 1) continue;
                dict[t[..idx].Trim()] = t[(idx + 1)..].Trim();
            }
            return dict;
        }

        public static void WriteIni(string path, Dictionary<string, string> values)
        {
            var lines   = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i].Trim();
                if (string.IsNullOrEmpty(t) || t.StartsWith('#') || t.StartsWith(';')) continue;
                var idx = t.IndexOf('=');
                if (idx < 1) continue;
                var key = t[..idx].Trim();
                if (values.TryGetValue(key, out var nv)) { lines[i] = $"{key}={nv}"; written.Add(key); }
            }
            foreach (var kv in values)
                if (!written.Contains(kv.Key)) lines.Add($"{kv.Key}={kv.Value}");
            File.WriteAllLines(path, lines, Encoding.UTF8);
        }

        public static ServerConfig LoadServerConfig(string ini_path)
        {
            var d = ReadIni(ini_path);
            var cfg = new ServerConfig();
            if (d.TryGetValue("Public",            out var pub)) cfg.PublicServer  = pub.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (d.TryGetValue("PauseEmpty",        out var pe))  cfg.PauseEmpty    = pe.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (d.TryGetValue("PublicName",        out var pn))  cfg.PublicName           = pn;
            if (d.TryGetValue("PublicDescription", out var pd))  cfg.PublicDescription    = pd;
            if (d.TryGetValue("Password",          out var pw))  cfg.Password             = pw;
            if (d.TryGetValue("AdminPassword",     out var apw)) cfg.AdminPassword        = apw;
            if (d.TryGetValue("ServerWelcomeMessage", out var wm)) cfg.ServerWelcomeMessage = wm;
            if (d.TryGetValue("RCONPassword",      out var rpw)) cfg.RconPassword         = rpw;
            if (d.TryGetValue("JVMArgs",           out var jvm)) cfg.JvmArgs              = jvm;
            if (d.TryGetValue("MaxPlayers",        out var mp)  && int.TryParse(mp,  out var mpv))  cfg.MaxPlayers        = mpv;
            if (d.TryGetValue("DefaultPort",       out var dp)  && int.TryParse(dp,  out var dpv))  cfg.Port              = dpv;
            if (d.TryGetValue("UDPPort",           out var udp) && int.TryParse(udp, out var udpv)) cfg.UdpPort           = udpv;
            if (d.TryGetValue("RCONPort",          out var rp)  && int.TryParse(rp,  out var rpv))  cfg.RconPort          = rpv;
            if (d.TryGetValue("SaveWorldEveryMinutes", out var swe) && int.TryParse(swe, out var swv)) cfg.SaveWorldInterval = swv;
            return cfg;
        }

        public static void SaveServerConfig(string ini_path, ServerConfig cfg)
        {
            WriteIni(ini_path, new Dictionary<string, string>
            {
                ["Public"]               = cfg.PublicServer ? "true" : "false",
                ["PublicName"]           = cfg.PublicName,
                ["PublicDescription"]    = cfg.PublicDescription,
                ["MaxPlayers"]           = cfg.MaxPlayers.ToString(),
                ["DefaultPort"]          = cfg.Port.ToString(),
                ["UDPPort"]              = cfg.UdpPort.ToString(),
                ["Password"]             = cfg.Password,
                ["AdminPassword"]        = cfg.AdminPassword,
                ["ServerWelcomeMessage"] = cfg.ServerWelcomeMessage,
                ["PauseEmpty"]           = cfg.PauseEmpty ? "true" : "false",
                ["SaveWorldEveryMinutes"] = cfg.SaveWorldInterval.ToString(),
                ["RCONPort"]             = cfg.RconPort.ToString(),
                ["RCONPassword"]         = cfg.RconPassword,
                ["JVMArgs"]              = cfg.JvmArgs,
            });
        }

        // Mods= and WorkshopItems= are parallel arrays. pz law. don't break the order.
        public static (string mods_raw, string workshop_raw, List<string> mod_folder_ids, List<string> workshop_ids)
            ReadModLines(string ini_path)
        {
            var d = ReadIni(ini_path);
            d.TryGetValue("Mods", out var mr); d.TryGetValue("WorkshopItems", out var wr);
            mr ??= ""; wr ??= "";
            var split = (string s) => s.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();
            return (mr, wr, split(mr), split(wr));
        }

        public static void SaveModLines(string ini_path, string mods, string workshop_items)
            => WriteIni(ini_path, new Dictionary<string, string> { ["Mods"] = mods, ["WorkshopItems"] = workshop_items });
    }
}
