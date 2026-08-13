// mod_scraper_service.cs
// fetches mod info from the steam web api.
// we used to scrape the html directly but steam's bot detection got aggressive
// and started returning 403s for anything that looked remotely programmatic.
// the api is cleaner anyway — structured json instead of hoping a div class name doesn't change.
// no api key required for the basic endpoint, but dependency resolution needs a key
// because valve decided that feature belongs to the authenticated api. of course it does.
// mod ID is still pulled from description text because steam doesn't have a dedicated field for it.
// that's fine. everything is fine. we just gonna roll with it.
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PZManager.Services
{
    public record ModScrapeResult(string? DisplayName, List<string> ModIds, List<string> SteamDependencies);

    public class ModScraperService
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

        // keyless endpoint — works anonymously, does NOT reliably return children even with includechildren=1
        private const string KEYLESS_URL =
            "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";

        // key endpoint — requires a Steam Web API key but properly returns children
        private const string KEY_URL =
            "https://api.steampowered.com/IPublishedFileService/GetDetails/v1/";

        private static readonly Regex _mod_id_re = new(
            @"Mod\s+ID\s*[:\-]\s*([A-Za-z0-9_\-\.][A-Za-z0-9_\-\. ,;]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _ws_id_re = new(
            @"Workshop\s+ID\s*[:\-]\s*\d+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ── public API ────────────────────────────────────────────────────────────

        public static async Task<ModScrapeResult> ScrapeAsync(string workshop_id)
        {
            var batch = await FetchApiAsync(new[] { workshop_id.Trim() });
            return batch.TryGetValue(workshop_id.Trim(), out var r) ? r
                : new ModScrapeResult(null, new(), new());
        }

        public static async Task<Dictionary<string, ModScrapeResult>> FetchManyAsync(
            IEnumerable<string> workshop_ids,
            IProgress<(string id, ModScrapeResult result)>? progress = null,
            int concurrency = 4)
        {
            var id_list = workshop_ids.Select(id => id.Trim()).Where(id => !string.IsNullOrEmpty(id)).ToList();
            var results = new Dictionary<string, ModScrapeResult>();
            var batches  = id_list.Chunk(100).ToList();
            var sem      = new SemaphoreSlim(Math.Max(1, concurrency / 4));

            var tasks = batches.Select(async batch =>
            {
                await sem.WaitAsync();
                try
                {
                    var br = await FetchApiAsync(batch);
                    lock (results) foreach (var kv in br) results[kv.Key] = kv.Value;
                    if (progress != null) foreach (var kv in br) progress.Report((kv.Key, kv.Value));
                }
                finally { sem.Release(); }
            });

            await Task.WhenAll(tasks);

            foreach (var id in id_list.Where(id => !results.ContainsKey(id)))
            {
                results[id] = new ModScrapeResult(null, new(), new());
                progress?.Report((id, results[id]));
            }
            return results;
        }

        // ── Steam API ─────────────────────────────────────────────────────────────

        private static async Task<Dictionary<string, ModScrapeResult>> FetchApiAsync(IEnumerable<string> ids)
        {
            var api_key  = PZManager.MainWindow.AppSettings.SteamApiKey?.Trim() ?? "";
            var id_array = ids.ToArray();

            return string.IsNullOrEmpty(api_key)
                ? await FetchKeyless(id_array)
                : await FetchWithKey(id_array, api_key);
        }

        /// Keyless POST to ISteamRemoteStorage — works anonymously.
        /// Note: children may not be returned even with includechildren=1 (Steam limitation on this endpoint).
        private static async Task<Dictionary<string, ModScrapeResult>> FetchKeyless(string[] id_array)
        {
            var results = new Dictionary<string, ModScrapeResult>();
            try
            {
                var sb = new StringBuilder();
                sb.Append($"itemcount={id_array.Length}");
                for (int i = 0; i < id_array.Length; i++)
                    sb.Append($"&publishedfileids%5B{i}%5D={id_array[i]}");

                var content  = new StringContent(sb.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded");
                var response = await _http.PostAsync(KEYLESS_URL, content);
                response.EnsureSuccessStatusCode();

                var doc     = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var details = doc.RootElement.GetProperty("response").GetProperty("publishedfiledetails");

                foreach (var item in details.EnumerateArray())
                    ParseItem(item, results);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModScraper keyless] {ex.Message}");
                foreach (var id in id_array)
                    if (!results.ContainsKey(id)) results[id] = new(null, new(), new());
            }
            return results;
        }

        /// GET to IPublishedFileService with API key — returns children properly.
        private static async Task<Dictionary<string, ModScrapeResult>> FetchWithKey(string[] id_array, string api_key)
        {
            var results = new Dictionary<string, ModScrapeResult>();
            try
            {
                var qs = new StringBuilder($"?key={Uri.EscapeDataString(api_key)}&includechildren=true&includetags=false&includekvtags=false&includevotes=false");
                for (int i = 0; i < id_array.Length; i++)
                    qs.Append($"&publishedfileids%5B{i}%5D={id_array[i]}");

                var response = await _http.GetAsync(KEY_URL + qs);
                response.EnsureSuccessStatusCode();

                var doc  = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (!doc.RootElement.TryGetProperty("response", out var resp)) return results;
                if (!resp.TryGetProperty("publishedfiledetails", out var details)) return results;

                foreach (var item in details.EnumerateArray())
                    ParseItem(item, results);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModScraper keyed] {ex.Message}");
                foreach (var id in id_array)
                    if (!results.ContainsKey(id)) results[id] = new(null, new(), new());
            }
            return results;
        }

        private static void ParseItem(JsonElement item, Dictionary<string, ModScrapeResult> results)
        {
            if (!item.TryGetProperty("publishedfileid", out var id_el)) return;
            var ws_id = id_el.GetString() ?? "";

            var result_code = item.TryGetProperty("result", out var rc) ? rc.GetInt32() : 0;
            if (result_code != 1) { results[ws_id] = new(null, new(), new()); return; }

            var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            var desc  = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            var mod_ids = ExtractModIds(desc);

            // formal Steam Required Items — only present when API key is used with includechildren=true
            var steam_deps = new List<string>();
            if (item.TryGetProperty("children", out var children))
                foreach (var child in children.EnumerateArray())
                    if (child.TryGetProperty("publishedfileid", out var cid))
                    {
                        var dep = cid.GetString();
                        if (!string.IsNullOrEmpty(dep) && dep != ws_id) steam_deps.Add(dep);
                    }

            // text fallback — catches "Requires X" written in description without Steam's formal system
            foreach (var dep in ExtractTextDependencies(desc, title ?? ""))
                if (!steam_deps.Contains(dep, StringComparer.OrdinalIgnoreCase))
                    steam_deps.Add(dep);

            results[ws_id] = new(title, mod_ids, steam_deps);
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        private static List<string> ExtractModIds(string text)
        {
            var mod_ids = new List<string>();
            foreach (Match m in _mod_id_re.Matches(text))
            {
                var ls = text.LastIndexOf('\n', m.Index);
                var le = text.IndexOf('\n', m.Index);
                var line = text[(ls < 0 ? 0 : ls)..(le < 0 ? text.Length : le)];
                if (_ws_id_re.IsMatch(line)) continue;
                foreach (var raw in m.Groups[1].Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var clean = raw.Trim();
                    if (!string.IsNullOrWhiteSpace(clean) && clean.Length < 64
                        && !clean.All(char.IsDigit)
                        && !mod_ids.Contains(clean, StringComparer.OrdinalIgnoreCase))
                        mod_ids.Add(clean);
                }
                if (mod_ids.Count > 0) break;
            }
            return mod_ids;
        }

        // Claude coming in clutch and serving up these bangers of regex. thanks Claude.
        private static readonly Regex[] _dep_patterns =
        {
            new(@"[Rr]equires?\s+([A-Z][A-Za-z0-9 _\-]{2,40}?)(?:\s+(?:framework|mod|to be|is)|\.|,|$|\[|\n)", RegexOptions.Compiled),
            new(@"([A-Z][A-Za-z0-9 _\-]{2,40}?)\s+(?:framework\s+)?(?:is required|must be (?:loaded|installed|enabled))", RegexOptions.Compiled),
            new(@"[Dd]epends? on\s+([A-Z][A-Za-z0-9 _\-]{2,40}?)(?:\s|\.|,|$|\[)", RegexOptions.Compiled),
            new(@"[Nn]eed[s]?\s+([A-Z][A-Za-z0-9 _\-]{2,40}?)\s+(?:framework|mod|to be|installed)", RegexOptions.Compiled),
        };

        private static readonly HashSet<string> _dep_stopwords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a","an","the","this","that","these","those","you","your","my","our",
            "build","version","update","patch","fix","server","client","game","mod",
            "workshop","steam","framework","newest","latest","newer","older",
        };

        private static List<string> ExtractTextDependencies(string desc, string own_title)
        {
            var deps = new List<string>();
            foreach (var pat in _dep_patterns)
                foreach (Match m in pat.Matches(desc))
                {
                    var name = m.Groups[1].Value.Trim().TrimEnd('.', ',', ' ');
                    if (string.IsNullOrWhiteSpace(name) || name.Length < 3 || name.Length > 50) continue;
                    if (_dep_stopwords.Contains(name)) continue;
                    if (!string.IsNullOrEmpty(own_title) && own_title.Contains(name, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!deps.Contains(name, StringComparer.OrdinalIgnoreCase)) deps.Add(name);
                }
            return deps;
        }
    }
}
