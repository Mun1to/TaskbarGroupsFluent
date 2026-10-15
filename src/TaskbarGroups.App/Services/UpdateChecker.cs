using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace TaskbarGroups.App.Services;

/// <summary>
/// Checks GitHub Releases for a newer version and downloads its installer, so the
/// app can update itself. Everything is best-effort and never throws — a failed
/// check (offline, rate-limited…) just means "no update".
/// </summary>
public static class UpdateChecker
{
    private const string LatestApi =
        "https://api.github.com/repos/Mun1to/TaskbarGroupsFluent/releases/latest";
    private const string UserAgent = "TaskbarGroupsFluent-Updater";

    public record UpdateInfo(Version Version, string Tag, string DownloadUrl);

    /// <summary>Returns the newer release if one exists, otherwise null.</summary>
    public static async Task<UpdateInfo?> CheckAsync() => (await TryCheckAsync()).Update;

    /// <summary>
    /// The same check, telling "you're up to date" apart from "couldn't ask":
    /// Ok is false when GitHub could not be reached or answered something
    /// unreadable, so the quick bar never shows a tick it doesn't know is true.
    /// </summary>
    public static async Task<(bool Ok, UpdateInfo? Update)> TryCheckAsync()
    {
        try
        {
            using var http = NewClient();
            using var resp = await http.GetAsync(LatestApi);
            if (!resp.IsSuccessStatusCode) return (false, null);

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Version? latest = ParseVersion(root.GetProperty("tag_name").GetString());
            if (latest is null) return (false, null);
            if (Normalize(latest) <= Normalize(CurrentVersion)) return (true, null);

            // A release whose installer isn't uploaded yet: nothing to offer for now.
            string? url = FindInstallerAsset(root);
            if (url is null) return (true, null);

            return (true, new UpdateInfo(latest, "v" + latest, url));
        }
        catch { return (false, null); }
    }

    /// <summary>The running version as "v1.2.3".</summary>
    public static string CurrentTag
    {
        get
        {
            var v = Normalize(CurrentVersion);
            return $"v{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>Downloads the installer to a temp file; returns its path or null.</summary>
    public static async Task<string?> DownloadInstallerAsync(string url)
    {
        try
        {
            using var http = NewClient();
            byte[] bytes = await http.GetByteArrayAsync(url);
            string path = Path.Combine(Path.GetTempPath(), "TaskbarGroupsFluent-Setup.exe");
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch { return null; }
    }

    public static Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    private static HttpClient NewClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return http;
    }

    private static string? FindInstallerAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets)) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString() ?? "";
            if (name.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase))
                return asset.GetProperty("browser_download_url").GetString();
        }
        return null;
    }

    private static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        return Version.TryParse(tag.TrimStart('v', 'V').Trim(), out var v) ? v : null;
    }

    // Compare on major.minor.build only; assembly versions carry a 4th field that
    // release tags (v1.4.0) don't, which would otherwise skew the comparison.
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
}
