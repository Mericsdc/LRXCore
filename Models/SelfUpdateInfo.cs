namespace LRXCore.Models;

public sealed class SelfUpdateInfo
{
    public bool IsUpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = "1.0.0";
    public string LatestVersion { get; set; } = "1.0.0";
    public string ReleaseTitle { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string ReleasePageUrl { get; set; } = "https://github.com/Mericsdc/LRXCore/releases";
    public string? InstallerDownloadUrl { get; set; }
    public string? InstallerFileName { get; set; }
    public long InstallerSizeBytes { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}
