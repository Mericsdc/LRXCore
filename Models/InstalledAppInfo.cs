using LRXCore.Helpers;

namespace LRXCore.Models;

public enum InstalledAppType
{
    Win32,
    StoreApp
}

public sealed class InstalledAppInfo
{
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string UninstallString { get; set; } = string.Empty;
    public string QuietUninstallString { get; set; } = string.Empty;
    public string RegistryKeyPath { get; set; } = string.Empty;
    public string PackageFullName { get; set; } = string.Empty;
    public string InstallDate { get; set; } = string.Empty;
    public long EstimatedSizeKb { get; set; }
    public InstalledAppType AppType { get; set; } = InstalledAppType.Win32;
    public bool IsSystemComponent { get; set; }

    public string TypeLabel => AppType == InstalledAppType.StoreApp ? "Mağaza (UWP)" : "Masaüstü";
    public string SizeDisplay => EstimatedSizeKb > 0 ? FormatHelper.FormatBytes(EstimatedSizeKb * 1024L) : "—";
}
