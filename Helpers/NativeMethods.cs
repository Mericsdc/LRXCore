using System.Runtime.InteropServices;

namespace LRXCore.Helpers;

/// <summary>
/// Win32 P/Invoke definitions for dark titlebar, recycle bin, and clipboard cleanup.
/// </summary>
internal static class NativeMethods
{
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [Flags]
    public enum RecycleFlags : uint
    {
        SHERB_NOCONFIRMATION = 0x00000001,
        SHERB_NOPROGRESSUI = 0x00000002,
        SHERB_NOSOUND = 0x00000004
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, RecycleFlags dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseClipboard();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

    /// <summary>
    /// Compacts managed heaps (optional) and trims OS working set pages so idle/minimized RAM stays minimal.
    /// </summary>
    public static void TrimWorkingSet(bool runGc = true)
    {
        try
        {
            if (runGc)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            }
            _ = SetProcessWorkingSetSize(GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
        }
        catch { }
    }

    public static (long sizeBytes, long itemCount) QueryRecycleBin()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        int hr = SHQueryRecycleBin(null, ref info);
        return hr == 0 ? (info.i64Size, info.i64NumItems) : (0, 0);
    }

    public static bool EmptyRecycleBinSilent()
    {
        int hr = SHEmptyRecycleBin(
            IntPtr.Zero,
            null,
            RecycleFlags.SHERB_NOCONFIRMATION | RecycleFlags.SHERB_NOPROGRESSUI | RecycleFlags.SHERB_NOSOUND);
        return hr == 0 || hr == unchecked((int)0x8000FFFF); // S_OK or already empty
    }

    public static bool ClearClipboardSafe()
    {
        try
        {
            if (!OpenClipboard(IntPtr.Zero))
                return false;
            try
            {
                return EmptyClipboard();
            }
            finally
            {
                CloseClipboard();
            }
        }
        catch
        {
            return false;
        }
    }
}
