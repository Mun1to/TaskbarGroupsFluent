using System.Diagnostics;

namespace TaskbarGroups.App.Helpers;

/// <summary>
/// The coffee page, the only way to support the app: there is no paid version.
/// The README, the site and GitHub's Sponsor button point at the same page.
/// </summary>
public static class SupportLink
{
    public const string Url = "https://buymeacoffee.com/munito";

    public static void Open()
    {
        try { Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true }); }
        catch { /* no default browser: nothing to open, and nothing else breaks */ }
    }
}
