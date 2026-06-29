using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace TaskbarGroups.App.Helpers;

/// <summary>
/// Opens File Explorer with a file selected. Uses the official
/// SHOpenFolderAndSelectItems API, which handles paths with spaces reliably —
/// unlike "explorer.exe /select,..." which mangles quoted paths.
/// </summary>
public static class ShellHelper
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ILCreateFromPath(string pszPath);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    [DllImport("shell32.dll")]
    private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, IntPtr[]? apidl, uint dwFlags);

    public static void SelectInExplorer(string filePath)
    {
        IntPtr pidl = ILCreateFromPath(filePath);
        if (pidl != IntPtr.Zero)
        {
            try
            {
                SHOpenFolderAndSelectItems(pidl, 0, null, 0);
                return;
            }
            catch
            {
                // Fall through to opening the containing folder.
            }
            finally
            {
                ILFree(pidl);
            }
        }

        // Fallback: just open the containing folder.
        string? folder = Path.GetDirectoryName(filePath);
        if (folder != null && Directory.Exists(folder))
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }
}
