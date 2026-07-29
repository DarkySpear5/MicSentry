using System.Net.Http;
using System.Text.Json;

namespace MicSentry;

// Mirrors linux/update_checker.py: a single check at launch, only if the
// user opted in via Settings, never auto-downloads or auto-installs
// anything, and stays completely silent on failure or "already current" —
// this must never nag. The one deliberate exception to "zero network
// calls," which is exactly why it defaults to off in AppSettings.
internal static class UpdateChecker
{
    public const string CurrentVersion = "1.1.0"; // bump alongside the repo's release tags

    private const string ReleasesApiUrl = "https://api.github.com/repos/DarkySpear5/MicSentry/releases/latest";
    public const string ReleasesPageUrl = "https://github.com/DarkySpear5/MicSentry/releases/latest";

    public static async Task<(string Version, string Url)?> CheckAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MicSentry-UpdateChecker");

            var json = await client.GetStringAsync(ReleasesApiUrl);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("tag_name", out var tagProp))
                return null;

            var latestVersion = tagProp.GetString()?.TrimStart('v', 'V');
            if (string.IsNullOrEmpty(latestVersion))
                return null;

            return IsNewer(latestVersion, CurrentVersion) ? (latestVersion, ReleasesPageUrl) : null;
        }
        catch
        {
            // no internet, rate-limited, API shape changed, whatever happened —
            // this must never surface as an error, only ever a positive
            // "here's a new version" when there genuinely is one.
            return null;
        }
    }

    internal static bool IsNewer(string latest, string current)
    {
        return Version.TryParse(NormalizeForVersion(latest), out var latestVer)
            && Version.TryParse(NormalizeForVersion(current), out var currentVer)
            && latestVer > currentVer;
    }

    private static string NormalizeForVersion(string v)
    {
        int dashIdx = v.IndexOf('-');
        if (dashIdx >= 0) v = v[..dashIdx];

        // System.Version needs at least major.minor
        return v.Contains('.') ? v : v + ".0";
    }
}
