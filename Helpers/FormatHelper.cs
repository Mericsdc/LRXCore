using System.IO;

namespace LRXCore.Helpers;

/// <summary>
/// Formatting and filesystem safety utilities shared across cleaners and uninstaller services.
/// </summary>
public static class FormatHelper
{
    private static readonly Lazy<HashSet<string>> ProtectedPaths = new(() =>
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            set.Add(NormalizePath(p));
        }

        Add(Path.GetPathRoot(Environment.SystemDirectory));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        Add(Environment.SystemDirectory);
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData"));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow"));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.Programs));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64"));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps"));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Common Files"));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Common Files"));
        Add("C:\\Users");

        return set;
    });

    public static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.Trim().Trim('"').TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    public static bool IsProtectedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        var normalized = NormalizePath(path);
        if (normalized.Length <= 3) // Drive root like C: or C:\
            return true;

        return ProtectedPaths.Value.Contains(normalized);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) bytes = 0;
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:F1} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:F1} MB";
        double gb = mb / 1024.0;
        return $"{gb:F2} GB";
    }

    public static long GetDirectorySizeSafe(string path, int maxDepth = 8)
    {
        if (!Directory.Exists(path)) return 0;
        long total = 0;
        var stack = new Stack<(string dir, int depth)>();
        stack.Push((path, 0));

        while (stack.Count > 0)
        {
            var (current, depth) = stack.Pop();
            try
            {
                var dirInfo = new DirectoryInfo(current);
                if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                foreach (var file in dirInfo.EnumerateFiles())
                {
                    try { total += file.Length; } catch { }
                }

                if (depth < maxDepth)
                {
                    foreach (var sub in dirInfo.EnumerateDirectories())
                    {
                        stack.Push((sub.FullName, depth + 1));
                    }
                }
            }
            catch
            {
                // Access denied or transient file lock
            }
        }
        return total;
    }
}
