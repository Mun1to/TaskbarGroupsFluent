using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Windows.Services.Store;

namespace TaskbarGroups.App.Services;

/// <summary>
/// The Store package's version of <see cref="UpdateChecker"/>. The Store updates
/// apps on its own sooner or later, but this gives the same "new version" notice
/// the installer build shows, and hands the download and install to the Store,
/// since a package's folder is read-only and can't be updated from inside.
///
/// Best-effort like the other one: anything that fails means "no update". A copy
/// that did not come from the Store (registered by hand for testing) has nothing
/// to ask about and simply gets no notice.
/// </summary>
public static class StoreUpdateChecker
{
    /// <summary>The pending updates, or null if there are none.</summary>
    public static async Task<IReadOnlyList<StorePackageUpdate>?> CheckAsync(Window owner)
    {
        try
        {
            var updates = await ContextFor(owner).GetAppAndOptionalStorePackageUpdatesAsync();
            return updates.Count > 0 ? updates : null;
        }
        catch { return null; }
    }

    /// <summary>The version the first pending update brings, as "v1.2.3".</summary>
    public static string Tag(IReadOnlyList<StorePackageUpdate> updates)
    {
        var v = updates[0].Package.Id.Version;
        return $"v{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>
    /// Asks the Store to download and install the updates, with its own progress
    /// window. Windows closes the app when its package is replaced, so a true here
    /// is rarely seen; false means it failed or the user cancelled.
    /// </summary>
    public static async Task<bool> InstallAsync(Window owner, IReadOnlyList<StorePackageUpdate> updates)
    {
        try
        {
            var result = await ContextFor(owner).RequestDownloadAndInstallStorePackageUpdatesAsync(updates);
            return result.OverallState == StorePackageUpdateState.Completed;
        }
        catch { return false; }
    }

    // A desktop app has to tell the Store which window its dialogs belong to;
    // without it the install request fails instead of showing them.
    private static StoreContext ContextFor(Window owner)
    {
        var context = StoreContext.GetDefault();
        WinRT.Interop.InitializeWithWindow.Initialize(context, new WindowInteropHelper(owner).Handle);
        return context;
    }
}
