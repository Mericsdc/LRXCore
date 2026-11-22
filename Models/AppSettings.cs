namespace LRXCore.Models;

public sealed class AppSettings
{
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool AutoCheckUpdates { get; set; } = true;
    public int UpdateCheckIntervalHours { get; set; } = 6;
    public bool StartWithWindows { get; set; } = false;
    public List<string> CustomCleanerFolders { get; set; } = new();
}
