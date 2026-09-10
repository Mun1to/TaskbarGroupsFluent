using System.IO;
using TaskbarGroups.Core;

namespace TaskbarGroups.Hover;

/// <summary>
/// Notices when the settings file changes while this process is running.
///
/// It exists so that changing a setting does not mean restarting the watcher.
/// That restart is worth avoiding: killing a process and starting another in the
/// same breath is a race, and losing it leaves the feature switched on with
/// nothing watching. Reading the change instead means a new delay takes effect at
/// once, and nothing has to be stopped at all.
///
/// It also gives the watcher a way out of its own accord: if the setting is
/// turned off and nothing manages to stop this process, it stops itself rather
/// than sitting there doing work nobody asked for.
///
/// The file events arrive on a pool thread, so all this does there is note the
/// time. The reading happens on the UI thread, from <see cref="TakeChange"/>,
/// which the watcher's own tick calls.
/// </summary>
internal sealed class SettingsListener : IDisposable
{
    /// <summary>
    /// How long to let the writer finish before reading. The file is rewritten
    /// whole, so reading the instant it changes can catch it empty or truncated,
    /// and one save raises several events anyway.
    /// </summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(250);

    private readonly FileSystemWatcher? _watcher;
    private readonly object _gate = new();
    private DateTime? _changedAt;

    public SettingsListener()
    {
        try
        {
            string path = Settings.settingsPath;
            string? folder = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

            _watcher = new FileSystemWatcher(folder, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };
            _watcher.Changed += (_, _) => Touch();
            _watcher.Created += (_, _) => Touch();
            _watcher.Renamed += (_, _) => Touch();
            _watcher.EnableRaisingEvents = true;
        }
        catch
        {
            // Survivable: the app still stops this process directly when the
            // setting is switched off, it just takes the slower route.
            _watcher = null;
        }
    }

    private void Touch()
    {
        lock (_gate) _changedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reloads the settings if a change has landed and gone quiet. Returns true
    /// only when something was actually re-read, so the caller can act on it.
    /// Call from the UI thread.
    /// </summary>
    public bool TakeChange()
    {
        lock (_gate)
        {
            if (_changedAt is null || DateTime.UtcNow - _changedAt < Settle) return false;
            _changedAt = null;
        }

        Settings.Reload();
        return true;
    }

    public void Dispose()
    {
        try { _watcher?.Dispose(); } catch { /* shutting down */ }
    }
}
