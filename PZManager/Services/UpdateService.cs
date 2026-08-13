// update_service.cs
// hits the github releases API and compares tag_name against the current version.
// no auth, no api key, completely free for public repos.
// fires once on startup, doesn't nag you every 30 seconds like some kind of adobe product.
// high chance this doesn't actually work. i simply haven't texted it yet.
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PZManager.Services
{
    public record UpdateInfo(string LatestTag, string CurrentVersion, bool UpdateAvailable, string ReleaseUrl);

    public static class UpdateService
    {
        // if you fork this and forget to update the url you'll be checking someone else's releases.
        // that would be embarrassing. update the url.
        private const string RELEASES_URL =
            "https://api.github.com/repos/WRTuL/PZ_Manager/releases/latest";

        public const string ReleasesPage =
            "https://github.com/WRTuL/PZ_Manager/releases/latest";

        // github requires a user-agent or it 403s you. classic.
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestHeaders = { { "User-Agent", "PZManager-UpdateChecker" } }
        };

        /// <summary>
        /// Checks GitHub for a newer release. Returns null if the check fails for any reason —
        /// no internet, rate limited, repo doesn't exist yet, whatever. caller should just ignore null.
        /// </summary>
        public static async Task<UpdateInfo?> CheckAsync()
        {
            try
            {
                var json     = await _http.GetStringAsync(RELEASES_URL);
                var doc      = JsonDocument.Parse(json);
                var tag_name = doc.RootElement.GetProperty("tag_name").GetString() ?? "";

                // strip leading 'v' from tag — "v1.2.3" -> "1.2.3"
                var latest  = tag_name.TrimStart('v').Trim();
                var current = GitInfo.Version;

                var update_available = IsNewer(latest, current);

                return new UpdateInfo(
                    LatestTag:       tag_name,
                    CurrentVersion:  current,
                    UpdateAvailable: update_available,
                    ReleaseUrl:      ReleasesPage
                );
            }
            catch
            {
                // network down, repo not public yet, rate limit hit, 404 because no releases exist —
                // all of these are "just don't show the banner" situations. not an error worth logging.
                return null;
            }
        }

        // compares two version strings like "1.2.3" > "1.0.0".
        // using semver proper comparison rather than string comparison because "1.10.0" > "1.9.0"
        // and string comparison gets that wrong. yes this matters. yes someone would have filed a bug.
        private static bool IsNewer(string latest, string current)
        {
            if (string.IsNullOrWhiteSpace(latest) || string.IsNullOrWhiteSpace(current)) return false;
            try
            {
                var l = ParseVersion(latest);
                var c = ParseVersion(current);
                if (l.major != c.major) return l.major > c.major;
                if (l.minor != c.minor) return l.minor > c.minor;
                return l.patch > c.patch;
            }
            catch { return false; }
        }

        private static (int major, int minor, int patch) ParseVersion(string v)
        {
            // strip build metadata suffix — "1.0.0+a3f9c2d" -> "1.0.0"
            var clean = v.Split('+')[0].Trim();
            var parts = clean.Split('.');
            return (
                parts.Length > 0 && int.TryParse(parts[0], out var maj) ? maj : 0,
                parts.Length > 1 && int.TryParse(parts[1], out var min) ? min : 0,
                parts.Length > 2 && int.TryParse(parts[2], out var pat) ? pat : 0
            );
        }
    }
}
