using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// A web app installed from a Chromium browser (Chrome, Edge, Brave, Vivaldi and
    /// the like), what the browsers also call a PWA (issue #15). Windows starts one
    /// through the browser's launcher with the app's id, for instance
    /// <c>chrome_proxy.exe --profile-directory=Default --app-id=&lt;id&gt;</c>, so the
    /// launcher's own icon and name say nothing about the app. Both live in the
    /// browser's profile, in <c>Web Applications\_crx_&lt;id&gt;</c>: the icon as an
    /// .ico named after the app, and, when the app was pinned, a shortcut beside it.
    /// </summary>
    public sealed class WebApp
    {
        /// <summary>The app's id: 32 letters from a to p, the same in every profile and browser.</summary>
        public string AppId { get; set; }

        /// <summary>The profile folder it was installed in, "Default" or "Profile 1" and so on.</summary>
        public string Profile { get; set; }

        /// <summary>The browser's launcher, chrome_proxy.exe or msedge_proxy.exe.</summary>
        public string Launcher { get; set; }

        /// <summary>What the launcher is given to start the app.</summary>
        public string Arguments { get; set; }

        /// <summary>The browser's "User Data" folder, or null when the launcher's place says nothing about it.</summary>
        public string UserData { get; set; }

        /// <summary>The shortcut it was found through, if any.</summary>
        public string Shortcut { get; set; }

        /// <summary>The app's name, as its shortcut in the Start menu has it.</summary>
        public string Name { get; set; }

        /// <summary>The app's icon in the browser's profile, or null if it was not found.</summary>
        public string IconPath { get; set; }

        /// <summary>The same app of the same browser and profile has the same key.</summary>
        public string Key
            => ((UserData ?? Path.GetDirectoryName(Launcher) ?? "") + "|" + Profile + "|" + AppId).ToLowerInvariant();
    }

    /// <summary>
    /// Where web apps are looked for. <see cref="ThisPc"/> is the real thing; a test
    /// builds one around a made-up browser so it never reads the user's own.
    /// </summary>
    public sealed class WebAppPlaces
    {
        /// <summary>%LOCALAPPDATA%, where every Chromium browser keeps its "User Data".</summary>
        public string LocalAppData { get; set; }

        /// <summary>
        /// Where browsers are installed: a launcher in &lt;root&gt;\&lt;vendor&gt;\&lt;browser&gt;\Application
        /// keeps its profiles in %LOCALAPPDATA%\&lt;vendor&gt;\&lt;browser&gt;\User Data.
        /// </summary>
        public string[] ProgramRoots { get; set; } = Array.Empty<string>();

        /// <summary>Folders read with all their subfolders for shortcuts: the Start menu.</summary>
        public string[] ShortcutFolders { get; set; } = Array.Empty<string>();

        /// <summary>Folders read without their subfolders: the desktops.</summary>
        public string[] LooseShortcutFolders { get; set; } = Array.Empty<string>();

        public static WebAppPlaces ThisPc { get; } = Build();

        private static WebAppPlaces Build()
        {
            static string Folder(Environment.SpecialFolder f)
            {
                try { return Environment.GetFolderPath(f); }
                catch { return ""; }
            }
            static string[] Some(params string[] folders)
                => folders.Where(f => !string.IsNullOrWhiteSpace(f))
                          .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            return new WebAppPlaces
            {
                LocalAppData = Folder(Environment.SpecialFolder.LocalApplicationData),
                ProgramRoots = Some(
                    Folder(Environment.SpecialFolder.ProgramFiles),
                    Folder(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetEnvironmentVariable("ProgramW6432"),
                    Folder(Environment.SpecialFolder.LocalApplicationData)),
                ShortcutFolders = Some(
                    Folder(Environment.SpecialFolder.Programs),
                    Folder(Environment.SpecialFolder.CommonPrograms)),
                LooseShortcutFolders = Some(
                    Folder(Environment.SpecialFolder.DesktopDirectory),
                    Folder(Environment.SpecialFolder.CommonDesktopDirectory))
            };
        }
    }

    /// <summary>
    /// Finds the web apps behind a launcher and its arguments or behind a shortcut,
    /// with their icons and names, and lists the ones installed (issue #15).
    /// </summary>
    public static class WebApps
    {
        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static readonly Regex AppIdPattern = new(@"--app-id=""?([a-p]{32})(?![a-z0-9])", Options);
        // A switch's value comes bare (Default), quoted ("Profile 1") or inside
        // quotes around the whole switch ("--profile-directory=Profile 1").
        private static readonly Regex ProfilePattern = Switch("profile-directory");
        private static readonly Regex UserDataPattern = Switch("user-data-dir");

        private static Regex Switch(string name)
            => new(@"(?<q>"")?--" + name + @"=(?:""(?<v>[^""]*)""|(?(q)(?<v>[^""]*)|(?<v>\S+)))", Options);

        /// <summary>
        /// Whether a program and its arguments start a web app. Only looks at the
        /// text, so it is cheap enough for anything that draws a list.
        /// </summary>
        public static bool IsWebApp(string target, string arguments)
            => !string.IsNullOrEmpty(arguments)
               && arguments.IndexOf("--app-id=", StringComparison.OrdinalIgnoreCase) >= 0
               && target != null && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
               && AppIdPattern.IsMatch(arguments);

        /// <summary>
        /// The web app a program and its arguments start, or null if they start
        /// something else. Its name and icon are not looked for yet.
        /// </summary>
        public static WebApp Parse(string target, string arguments, WebAppPlaces places = null)
        {
            if (!IsWebApp(target, arguments)) return null;
            places ??= WebAppPlaces.ThisPc;

            string userData = Value(UserDataPattern, arguments);
            return new WebApp
            {
                AppId = AppIdPattern.Match(arguments).Groups[1].Value.ToLowerInvariant(),
                Profile = Value(ProfilePattern, arguments) ?? "Default",
                Launcher = target,
                Arguments = arguments.Trim(),
                UserData = userData != null
                    ? Environment.ExpandEnvironmentVariables(userData)
                    : UserDataFor(target, places)
            };
        }

        /// <summary>
        /// The web app a shortcut starts, named after the shortcut, or null if it
        /// starts something else or is not a shortcut.
        /// </summary>
        public static WebApp FromShortcut(string linkPath, WebAppPlaces places = null)
        {
            if (string.IsNullOrEmpty(linkPath)
                || !linkPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
                || !MentionsAppId(linkPath)
                || !ShellLink.TryReadTarget(linkPath, out string target, out string arguments))
                return null;

            WebApp app = Parse(target, arguments, places);
            if (app == null) return null;
            app.Shortcut = linkPath;
            app.Name = Path.GetFileNameWithoutExtension(linkPath);
            return app;
        }

        /// <summary>
        /// The icon of the web app that a program and its arguments, or a shortcut,
        /// start, at <paramref name="size"/> pixels; null when they start anything
        /// else or the icon is not there.
        /// </summary>
        public static Bitmap IconFor(string target, string arguments, int size, WebAppPlaces places = null)
        {
            try
            {
                WebApp app = Parse(target, arguments, places) ?? FromShortcut(target, places);
                if (app == null) return null;
                string icon = FindIcon(app);
                return icon == null ? null : PickedIcon.Extract(icon, 0, size);
            }
            catch { return null; }
        }

        /// <summary>
        /// The app's .ico in the browser's profile, or null. The same app installed
        /// in another profile of the same browser has the same id and icon, so
        /// those are tried when its own profile does not have it.
        /// </summary>
        public static string FindIcon(WebApp app)
        {
            if (app == null) return null;
            if (app.IconPath != null && File.Exists(app.IconPath)) return app.IconPath;

            string folder = AppFolder(app);
            if (folder == null) return null;

            List<FileInfo> icons = Files(folder, ".ico");
            if (icons.Count == 0) return null;

            // Named after the app; a folder with more than one keeps the newest name.
            FileInfo named = app.Name == null ? null
                : icons.FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f.Name), app.Name, StringComparison.OrdinalIgnoreCase));
            return (named ?? icons.OrderByDescending(f => f.LastWriteTimeUtc).First()).FullName;
        }

        /// <summary>
        /// The app's name: the shortcut kept beside its icon, then its shortcut in
        /// the Start menu or on the desktop, then the name of its icon, which the
        /// browser writes from the app's name. Null if none is found.
        /// </summary>
        public static string FindName(WebApp app, WebAppPlaces places = null)
        {
            if (app == null) return null;
            places ??= WebAppPlaces.ThisPc;

            string folder = AppFolder(app);
            if (folder != null)
            {
                FileInfo link = Files(folder, ".lnk").FirstOrDefault();
                if (link != null) return Path.GetFileNameWithoutExtension(link.Name);
            }

            string listed = Installed(places, findIcons: false)
                .FirstOrDefault(a => a.Key == app.Key)?.Name;
            if (listed != null) return listed;

            if (folder != null)
            {
                FileInfo icon = Files(folder, ".ico").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
                if (icon != null) return Path.GetFileNameWithoutExtension(icon.Name);
            }
            return null;
        }

        /// <summary>
        /// Every web app with a shortcut: in the Start menu (each browser's
        /// «… Apps» folder, or loose), on the desktop, or kept by the browser beside
        /// the app's icon, which is there even when the app has no other shortcut.
        /// One entry per app, browser and profile, the Start menu's first.
        /// </summary>
        public static List<WebApp> Installed(WebAppPlaces places = null, bool findIcons = true)
        {
            places ??= WebAppPlaces.ThisPc;
            var found = new Dictionary<string, WebApp>(StringComparer.OrdinalIgnoreCase);
            void Add(string linkPath)
            {
                WebApp app = FromShortcut(linkPath, places);
                if (app != null && !found.ContainsKey(app.Key)) found[app.Key] = app;
            }

            foreach (string folder in places.ShortcutFolders)
                foreach (string link in Links(folder, recursive: true)) Add(link);
            foreach (string folder in places.LooseShortcutFolders)
                foreach (string link in Links(folder, recursive: false)) Add(link);
            foreach (string userData in UserDataFolders(places))
                foreach (string profile in Directories(userData))
                    foreach (string appFolder in Directories(Path.Combine(profile, "Web Applications"), "_crx_*"))
                        foreach (string link in Links(appFolder, recursive: false)) Add(link);

            if (findIcons)
                foreach (WebApp app in found.Values)
                    app.IconPath = FindIcon(app);
            return found.Values.ToList();
        }

        // ---- Where things are ----

        // A launcher sits in <root>\<vendor>\<browser>\Application, and its browser
        // keeps its profiles in %LOCALAPPDATA%\<vendor>\<browser>\User Data whether
        // it was installed for everyone or just for this user. Measured with Brave
        // installed in Program Files; Chrome, Edge and Vivaldi use the same layout.
        // A copy somewhere else, such as a portable one, may keep User Data beside
        // Application.
        internal static string UserDataFor(string launcher, WebAppPlaces places)
        {
            try
            {
                string appFolder = Path.GetDirectoryName(Path.GetFullPath(launcher));
                if (appFolder == null || !string.Equals(Path.GetFileName(appFolder), "Application", StringComparison.OrdinalIgnoreCase))
                    return null;
                string browser = Path.GetDirectoryName(appFolder);
                if (browser == null) return null;

                string mapped = null;
                foreach (string root in places.ProgramRoots.OrderByDescending(r => r.Length))
                {
                    string relative = RelativeUnder(browser, root);
                    if (relative == null) continue;
                    mapped = Path.Combine(places.LocalAppData, relative, "User Data");
                    break;
                }
                string beside = Path.Combine(browser, "User Data");
                if (mapped != null && Directory.Exists(mapped)) return mapped;
                if (Directory.Exists(beside)) return beside;
                return mapped;
            }
            catch { return null; }
        }

        // The app's own folder in its profile, or in another profile of the same browser.
        private static string AppFolder(WebApp app)
        {
            if (string.IsNullOrEmpty(app.UserData)) return null;
            string name = "_crx_" + app.AppId;
            try
            {
                string own = Path.Combine(app.UserData, app.Profile ?? "Default", "Web Applications", name);
                if (Directory.Exists(own)) return own;
                foreach (string profile in Directories(app.UserData))
                {
                    string other = Path.Combine(profile, "Web Applications", name);
                    if (Directory.Exists(other)) return other;
                }
            }
            catch { }
            return null;
        }

        // Every Chromium "User Data" folder one or two levels under %LOCALAPPDATA%,
        // known by the "Local State" file every one of them has.
        private static IEnumerable<string> UserDataFolders(WebAppPlaces places)
        {
            if (string.IsNullOrEmpty(places.LocalAppData)) yield break;
            // Thousands of folders that never hold a browser.
            var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Packages", "Temp" };
            foreach (string first in Directories(places.LocalAppData))
            {
                if (skip.Contains(Path.GetFileName(first))) continue;
                string here = Path.Combine(first, "User Data");
                if (File.Exists(Path.Combine(here, "Local State"))) { yield return here; continue; }
                foreach (string second in Directories(first))
                {
                    string below = Path.Combine(second, "User Data");
                    if (File.Exists(Path.Combine(below, "Local State"))) yield return below;
                }
            }
        }

        private static string RelativeUnder(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return null;
            string full = Path.GetFullPath(path).TrimEnd('\\');
            string start = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            return full.StartsWith(start, StringComparison.OrdinalIgnoreCase) ? full.Substring(start.Length) : null;
        }

        private static string Value(Regex pattern, string arguments)
        {
            Match m = pattern.Match(arguments ?? "");
            if (!m.Success) return null;
            string value = m.Groups["v"].Value.Trim();
            return value.Length == 0 ? null : value;
        }

        // A shortcut is a couple of kilobytes, and only a few in the Start menu start
        // a web app, so the bytes are looked through for the id before Windows is
        // asked to read it. Shortcuts keep their arguments in UTF-16, and very old
        // ones in the system's code page.
        private static readonly byte[] AppIdWide = Encoding.Unicode.GetBytes("--app-id=");
        private static readonly byte[] AppIdNarrow = Encoding.ASCII.GetBytes("--app-id=");

        private static bool MentionsAppId(string linkPath)
        {
            try
            {
                var info = new FileInfo(linkPath);
                if (!info.Exists || info.Length > 256 * 1024) return false;
                ReadOnlySpan<byte> bytes = File.ReadAllBytes(linkPath);
                return bytes.IndexOf(AppIdWide) >= 0 || bytes.IndexOf(AppIdNarrow) >= 0;
            }
            catch { return false; }
        }

        private static readonly EnumerationOptions Shallow = new() { IgnoreInaccessible = true, RecurseSubdirectories = false };
        private static readonly EnumerationOptions Deep = new() { IgnoreInaccessible = true, RecurseSubdirectories = true };

        private static IEnumerable<string> Links(string folder, bool recursive)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return Array.Empty<string>();
            try
            {
                return Directory.EnumerateFiles(folder, "*.lnk", recursive ? Deep : Shallow)
                    .Where(f => f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch { return Array.Empty<string>(); }
        }

        private static IEnumerable<string> Directories(string folder, string pattern = "*")
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return Array.Empty<string>();
            try { return Directory.EnumerateDirectories(folder, pattern, Shallow).ToList(); }
            catch { return Array.Empty<string>(); }
        }

        // Files in a folder ending in an extension, checked by hand rather than by a
        // pattern: Windows matches a three-letter pattern against longer extensions
        // too, so "*.ico" would also take an ".icon" file.
        private static List<FileInfo> Files(string folder, string extension)
        {
            try
            {
                return new DirectoryInfo(folder).EnumerateFiles("*", Shallow)
                    .Where(f => f.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch { return new List<FileInfo>(); }
        }
    }
}
