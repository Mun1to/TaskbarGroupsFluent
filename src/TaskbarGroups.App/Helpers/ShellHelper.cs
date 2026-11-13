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

    /// <summary>
    /// Starts dragging <paramref name="filePath"/> as File Explorer would, with the
    /// shell's own data and drag picture, so that whatever File Explorer can drop
    /// it on takes it the same way (issue #18: the taskbar, which pins a shortcut
    /// dropped on it). Only as a copy or a link: the group's shortcut never leaves
    /// its folder, wherever it is dropped. Returns once the drag is over.
    /// </summary>
    public static void DragFile(IntPtr owner, string filePath)
    {
        Guid iidItem = typeof(IShellItem).GUID;
        if (SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref iidItem, out IShellItem? item) != 0 || item is null)
            return;

        IntPtr data = IntPtr.Zero;
        try
        {
            Guid bhid = BHID_DataObject, iid = IID_IDataObject;
            if (item.BindToHandler(IntPtr.Zero, ref bhid, ref iid, out data) != 0 || data == IntPtr.Zero) return;
            SHDoDragDrop(owner, data, IntPtr.Zero, DROPEFFECT_COPY | DROPEFFECT_LINK, out _);
        }
        catch { /* no drag, nothing else lost */ }
        finally
        {
            if (data != IntPtr.Zero) Marshal.Release(data);
            Marshal.ReleaseComObject(item);
        }
    }

    private const uint DROPEFFECT_COPY = 1, DROPEFFECT_LINK = 4;
    private static readonly Guid BHID_DataObject = new("B8C0BD9F-ED24-455C-83E6-D5390C4FE8C4");
    private static readonly Guid IID_IDataObject = new("0000010E-0000-0000-C000-000000000046");

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, out IShellItem? item);

    [DllImport("shell32.dll")]
    private static extern int SHDoDragDrop(IntPtr hwnd, IntPtr dataObject, IntPtr dropSource, uint okEffects, out uint effect);
}
