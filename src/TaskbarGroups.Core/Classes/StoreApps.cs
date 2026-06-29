using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// A launchable Microsoft Store (UWP/MSIX) application, identified by its
    /// AppUserModelId so it can be launched via shell:AppsFolder.
    /// </summary>
    public class StoreAppInfo
    {
        public string DisplayName { get; set; } = "";
        public string AppUserModelId { get; set; } = "";
    }

    /// <summary>
    /// Enumerates installed Store apps for the current user using the WinRT
    /// PackageManager. This is what lets users add apps with no .exe (WhatsApp,
    /// Instagram, TikTok, …) that the classic file picker cannot reach.
    /// </summary>
    public static class StoreApps
    {
        public static List<StoreAppInfo> EnumerateInstalled()
        {
            var result = new List<StoreAppInfo>();

            IEnumerable<Package> packages;
            try
            {
                packages = new PackageManager().FindPackagesForUser("");
            }
            catch
            {
                return result;
            }

            foreach (var pkg in packages)
            {
                try
                {
                    if (pkg.IsFramework || pkg.IsResourcePackage)
                        continue;
                }
                catch { continue; }

                IReadOnlyList<AppListEntry> entries;
                try
                {
                    entries = pkg.GetAppListEntries();
                }
                catch { continue; }

                foreach (var entry in entries)
                {
                    try
                    {
                        string name = entry.DisplayInfo.DisplayName;
                        string aumid = entry.AppUserModelId;
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(aumid))
                            result.Add(new StoreAppInfo { DisplayName = name, AppUserModelId = aumid });
                    }
                    catch { }
                }
            }

            return result
                .GroupBy(a => a.AppUserModelId)
                .Select(g => g.First())
                .OrderBy(a => a.DisplayName)
                .ToList();
        }
    }
}
