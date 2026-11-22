namespace LRXCore.Models;

public sealed class GpuInfo
{
    public string Name { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty; // NVIDIA, AMD, Intel, Other
    public long VramBytes { get; set; }
    public string VramDisplay => VramBytes > 0 ? Helpers.FormatHelper.FormatBytes(VramBytes) : "Paylaşımlı / Bilinmiyor";
}

public sealed class DiskInfoItem
{
    public string DriveLetter { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
    public double UsedPercent => TotalBytes > 0 ? Math.Round(UsedBytes * 100.0 / TotalBytes, 1) : 0;
    public string SummaryDisplay =>
        $"{Helpers.FormatHelper.FormatBytes(FreeBytes)} boş / {Helpers.FormatHelper.FormatBytes(TotalBytes)} ({UsedPercent:F0}% dolu)";
}

public sealed class DriverRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? WingetId { get; set; }
    public string Source { get; set; } = "winget";
    public string? DownloadUrl { get; set; }
    public string ActionLabel => !string.IsNullOrEmpty(WingetId) ? "Kur (Winget)" : "Resmi Sayfayı Aç";
    public bool IsWingetInstall => !string.IsNullOrEmpty(WingetId);
}

public sealed class HardwareInfo
{
    public string CpuName { get; set; } = "Bilinmiyor";
    public string CpuVendor { get; set; } = "Other";
    public int CpuCores { get; set; }
    public int CpuThreads { get; set; }
    public int CpuMaxClockMhz { get; set; }

    public List<GpuInfo> Gpus { get; set; } = new();
    public GpuInfo? PrimaryGpu => Gpus.FirstOrDefault();

    public string MotherboardName { get; set; } = "Bilinmiyor";
    public long TotalRamBytes { get; set; }
    public int RamSpeedMhz { get; set; }
    public int RamModuleCount { get; set; }

    public string OsCaption { get; set; } = "Windows";
    public string OsBuild { get; set; } = string.Empty;
    public string ComputerName { get; set; } = Environment.MachineName;

    public List<DiskInfoItem> Disks { get; set; } = new();
    public List<DriverRecommendation> Recommendations { get; set; } = new();

    public string RamDisplay =>
        RamSpeedMhz > 0
            ? $"{Helpers.FormatHelper.FormatBytes(TotalRamBytes)} ({RamSpeedMhz} MHz, {RamModuleCount} modül)"
            : Helpers.FormatHelper.FormatBytes(TotalRamBytes);
}
