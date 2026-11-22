using System.Management;
using LRXCore.Models;
using Microsoft.Win32;

namespace LRXCore.Services;

public sealed class HardwareService
{
    public Task<HardwareInfo> GetHardwareInfoAsync() => Task.Run(CollectHardwareInfo);

    private static HardwareInfo CollectHardwareInfo()
    {
        var info = new HardwareInfo();

        // CPU
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                info.CpuName = (obj["Name"]?.ToString() ?? "Bilinmiyor").Trim();
                var mfg = obj["Manufacturer"]?.ToString() ?? string.Empty;
                info.CpuVendor = DetectCpuVendor(info.CpuName, mfg);
                info.CpuCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                info.CpuThreads = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                info.CpuMaxClockMhz = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
                break;
            }
        }
        catch { }

        // GPU + Registry VRAM lookup (Win32_VideoController.AdapterRAM overflows at 4GB)
        var registryVramMap = ReadRegistryVramMap();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion, AdapterCompatibility, AdapterRAM FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var name = (obj["Name"]?.ToString() ?? "").Trim();
                if (string.IsNullOrEmpty(name)) continue;

                // Skip virtual/basic display adapters when real GPUs exist unless it's the only one
                var compat = (obj["AdapterCompatibility"]?.ToString() ?? "").Trim();
                var driverVer = (obj["DriverVersion"]?.ToString() ?? "").Trim();
                long vram = 0;

                if (registryVramMap.TryGetValue(name, out var regVram) && regVram > 0)
                    vram = regVram;
                else if (obj["AdapterRAM"] is not null && long.TryParse(obj["AdapterRAM"].ToString(), out var wmiRam))
                    vram = wmiRam;

                info.Gpus.Add(new GpuInfo
                {
                    Name = name,
                    DriverVersion = driverVer,
                    Vendor = DetectGpuVendor(name, compat),
                    VramBytes = vram
                });
            }
        }
        catch { }

        // Sort discrete GPUs before integrated / virtual display adapters
        info.Gpus = info.Gpus
            .OrderByDescending(g => g.Vendor is "NVIDIA" or "AMD")
            .ThenByDescending(g => g.VramBytes)
            .ToList();

        // Motherboard
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get())
            {
                var mfg = (obj["Manufacturer"]?.ToString() ?? "").Trim();
                var prod = (obj["Product"]?.ToString() ?? "").Trim();
                info.MotherboardName = $"{mfg} {prod}".Trim();
                break;
            }
        }
        catch { }

        // RAM
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity, ConfiguredClockSpeed, Speed FROM Win32_PhysicalMemory");
            long totalRam = 0;
            int count = 0;
            int speed = 0;
            foreach (var obj in searcher.Get())
            {
                if (long.TryParse(obj["Capacity"]?.ToString(), out var cap))
                    totalRam += cap;
                if (speed == 0)
                {
                    if (int.TryParse(obj["ConfiguredClockSpeed"]?.ToString(), out var cSpeed) && cSpeed > 0)
                        speed = cSpeed;
                    else if (int.TryParse(obj["Speed"]?.ToString(), out var s) && s > 0)
                        speed = s;
                }
                count++;
            }
            info.TotalRamBytes = totalRam;
            info.RamModuleCount = count;
            info.RamSpeedMhz = speed;
        }
        catch { }

        // OS
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption, BuildNumber, Version FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                info.OsCaption = (obj["Caption"]?.ToString() ?? "Windows").Replace("Microsoft ", "").Trim();
                info.OsBuild = (obj["BuildNumber"]?.ToString() ?? "").Trim();
                break;
            }
        }
        catch { }

        // Disks
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT DeviceID, VolumeName, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3");
            foreach (var obj in searcher.Get())
            {
                var letter = obj["DeviceID"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(letter)) continue;
                long.TryParse(obj["Size"]?.ToString(), out var size);
                long.TryParse(obj["FreeSpace"]?.ToString(), out var free);
                var volName = (obj["VolumeName"]?.ToString() ?? string.Empty).Trim();
                info.Disks.Add(new DiskInfoItem
                {
                    DriveLetter = letter,
                    Label = string.IsNullOrWhiteSpace(volName) ? "Yerel Disk" : volName,
                    TotalBytes = size,
                    FreeBytes = free
                });
            }
        }
        catch { }

        info.Recommendations = BuildRecommendations(info);
        return info;
    }

    private static Dictionary<string, long> ReadRegistryVramMap()
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        const string classPath = @"SYSTEM\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(classPath);
            if (baseKey is null) return map;

            foreach (var subName in baseKey.GetSubKeyNames())
            {
                if (!subName.All(char.IsDigit)) continue;
                try
                {
                    using var sub = baseKey.OpenSubKey(subName);
                    var desc = sub?.GetValue("DriverDesc")?.ToString()?.Trim();
                    var rawSize = sub?.GetValue("HardwareInformation.qwMemorySize");
                    if (string.IsNullOrEmpty(desc) || rawSize is null) continue;

                    long bytes = rawSize switch
                    {
                        long l => l,
                        int i => i,
                        byte[] b when b.Length >= 8 => BitConverter.ToInt64(b, 0),
                        byte[] b when b.Length == 4 => BitConverter.ToUInt32(b, 0),
                        _ => 0
                    };
                    if (bytes > 0)
                        map[desc] = bytes;
                }
                catch { }
            }
        }
        catch { }

        return map;
    }

    private static string DetectCpuVendor(string name, string mfg)
    {
        var combined = $"{name} {mfg}";
        if (combined.Contains("AMD", StringComparison.OrdinalIgnoreCase) || combined.Contains("Ryzen", StringComparison.OrdinalIgnoreCase))
            return "AMD";
        if (combined.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            return "Intel";
        return "Other";
    }

    private static string DetectGpuVendor(string name, string compat)
    {
        var combined = $"{name} {compat}";
        if (combined.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || combined.Contains("GeForce", StringComparison.OrdinalIgnoreCase) || combined.Contains("RTX", StringComparison.OrdinalIgnoreCase))
            return "NVIDIA";
        if (combined.Contains("AMD", StringComparison.OrdinalIgnoreCase) || combined.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
            return "AMD";
        if (combined.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            return "Intel";
        return "Other";
    }

    private static List<DriverRecommendation> BuildRecommendations(HardwareInfo info)
    {
        var list = new List<DriverRecommendation>();

        if (info.Gpus.Any(g => g.Vendor == "NVIDIA"))
        {
            list.Add(new DriverRecommendation
            {
                Title = "NVIDIA App",
                Reason = "Sisteminizde NVIDIA ekran kartı tespit edildi. Sürücü güncellemeleri ve GPU ayarları için önerilir.",
                WingetId = "XP8CLZL93F5Z4P",
                Source = "msstore"
            });
            list.Add(new DriverRecommendation
            {
                Title = "NVIDIA Game Ready Sürücü Sayfası",
                Reason = "En güncel WHQL Game Ready ekran kartı sürücüsünü doğrudan indirin.",
                DownloadUrl = "https://www.nvidia.com/en-us/geforce/drivers/"
            });
        }

        if (info.Gpus.Any(g => g.Vendor == "AMD"))
        {
            list.Add(new DriverRecommendation
            {
                Title = "AMD Software: Adrenalin Edition",
                Reason = "Sisteminizde AMD Radeon grafik birimi tespit edildi. Resmi sürücü indirme sayfası.",
                DownloadUrl = "https://www.amd.com/en/support/download/drivers.html"
            });
        }

        if (info.Gpus.Any(g => g.Vendor == "Intel") || info.CpuVendor == "Intel")
        {
            list.Add(new DriverRecommendation
            {
                Title = "Intel Driver & Support Assistant",
                Reason = "Intel işlemci / dahili grafik ve Wi-Fi/Chipset sürücülerini otomatik tespit eder.",
                WingetId = "Intel.IntelDriverAndSupportAssistant",
                Source = "winget"
            });
        }

        if (info.CpuVendor == "AMD")
        {
            list.Add(new DriverRecommendation
            {
                Title = "AMD Chipset Sürücüleri",
                Reason = "Ryzen işlemci güç yönetimi ve PCIe performansı için güncel chipset sürücüsü önerilir.",
                DownloadUrl = "https://www.amd.com/en/support/download/drivers.html"
            });
        }

        list.Add(new DriverRecommendation
        {
            Title = "Visual C++ Redistributable Runtimes (Hepsi Bir Arada / AIO)",
            Reason = "2005–2022 arası tüm x86 ve x64 C++ kütüphanelerini tek pakette kurar; oyun ve uygulama DLL hatalarını önler.",
            WingetId = "abbodi1406.vcredist",
            Source = "winget"
        });

        list.Add(new DriverRecommendation
        {
            Title = "Visual C++ AIO — Technopat Arşiv Sayfası",
            Reason = "Winget yerine ZIP/EXE arşivini manuel olarak indirmek isterseniz Technopat resmi indirme sayfası.",
            DownloadUrl = "https://www.technopat.net/sosyal/indir/visual-c-redistributable-runtimes-hepsi-bir-arada.486/"
        });

        list.Add(new DriverRecommendation
        {
            Title = ".NET 8 Desktop Runtime (x64)",
            Reason = "Modern Windows masaüstü uygulamaları için temel çalışma zamanı paketi.",
            WingetId = "Microsoft.DotNet.DesktopRuntime.8",
            Source = "winget"
        });

        return list;
    }
}
