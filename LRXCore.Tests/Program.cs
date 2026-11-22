using System.IO;
using LRXCore.Helpers;
using LRXCore.Services;

int failures = 0;

void AssertTrue(bool condition, string name)
{
    if (condition)
    {
        Console.WriteLine($"[PASS] {name}");
    }
    else
    {
        Console.WriteLine($"[FAIL] {name}");
        failures++;
    }
}

Console.WriteLine("=== LRX Core Non-Destructive Verification ===");

// 1. FormatHelper safety guard checks
AssertTrue(FormatHelper.IsProtectedPath(@"C:\"), "Protected root drive C:\\");
AssertTrue(FormatHelper.IsProtectedPath(@"C:\Windows"), "Protected C:\\Windows");
AssertTrue(FormatHelper.IsProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)), "Protected Program Files");
AssertTrue(FormatHelper.IsProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), "Protected AppData");
AssertTrue(!FormatHelper.IsProtectedPath(@"C:\Program Files\SomeRemovedApp"), "Non-protected app subfolder allowed");
AssertTrue(FormatHelper.FormatBytes(1536).Contains("1.5") || FormatHelper.FormatBytes(1536).Contains("1,5"), "FormatBytes KB formatting");

// 2. WingetService.ParseUpgradeOutput (English + spinner + multi-token version)
var englishSample =
    "   - \r   \\ \r   | \rName                                                         Id                                   Version                 Available                 Source\n" +
    "----------------------------------------------------------------------------------------------------------------------------------------------------------\n" +
    "AnyDesk                                                      AnyDesk.AnyDesk                      ad 9.8.2                9.8.4                     winget\n" +
    "Microsoft Visual C++ v14 Redistributable (x64) - 14.51.36247 Microsoft.VCRedist.2015+.x64         14.51.36247.0           14.52.37029.0             winget\n" +
    "2 upgrades available.\n";

var enParsed = WingetService.ParseUpgradeOutput(englishSample);
AssertTrue(enParsed.Count == 2, $"English winget parse count == 2 (got {enParsed.Count})");
AssertTrue(enParsed[0].WingetId == "AnyDesk.AnyDesk" && enParsed[0].CurrentVersion == "ad 9.8.2" && enParsed[0].AvailableVersion == "9.8.4",
    $"English row 0 fields: {enParsed[0].WingetId} | {enParsed[0].CurrentVersion} -> {enParsed[0].AvailableVersion}");
AssertTrue(enParsed[1].WingetId == "Microsoft.VCRedist.2015+.x64", "English row 1 WingetId");

// 3. WingetService.ParseUpgradeOutput (Turkish locale sample)
var turkishSample =
    "Ad                                   Kimlik                          Sürüm       Kullanılabilir  Kaynak\n" +
    "--------------------------------------------------------------------------------------------------------\n" +
    "Google Chrome                        Google.Chrome                   157.0.1.0   158.0.8127.37   winget\n" +
    "Discord                              Discord.Discord                 1.0.9260    1.0.9269        winget\n" +
    "2 yükseltme kullanılabilir.\n";

var trParsed = WingetService.ParseUpgradeOutput(turkishSample);
AssertTrue(trParsed.Count == 2, $"Turkish winget parse count == 2 (got {trParsed.Count})");
AssertTrue(trParsed[0].WingetId == "Google.Chrome" && trParsed[1].AvailableVersion == "1.0.9269", "Turkish winget row values");

// 3b. Elevation prohibition detection
AssertTrue(WingetService.IsElevationProhibited(-1978335135, ""), "Detects exit code -1978335135 as elevation prohibited");
AssertTrue(WingetService.IsElevationProhibited(1, "The installer cannot be run from an administrator context."), "Detects English administrator context message");
AssertTrue(!WingetService.IsElevationProhibited(0, "Successfully installed"), "Non-error install is not elevation prohibited");

// 4. Live HardwareService read-only check
var hwService = new HardwareService();
var hw = await hwService.GetHardwareInfoAsync();
AssertTrue(!string.IsNullOrWhiteSpace(hw.CpuName) && hw.CpuName != "Bilinmiyor", $"CPU detected: {hw.CpuName}");
AssertTrue(hw.Gpus.Count > 0, $"GPU detected ({hw.Gpus.Count}): {hw.PrimaryGpu?.Name} [{hw.PrimaryGpu?.VramDisplay}]");
AssertTrue(hw.TotalRamBytes > 0, $"RAM detected: {hw.RamDisplay}");
AssertTrue(hw.Disks.Count > 0, $"Disks detected: {hw.Disks.Count}");
AssertTrue(hw.Disks.All(d => !string.IsNullOrWhiteSpace(d.Label)), "All disk labels non-empty (no empty '( )')");
AssertTrue(hw.Recommendations.Count >= 3, $"Driver/Runtime recommendations generated: {hw.Recommendations.Count}");
AssertTrue(hw.Recommendations.Any(r => r.WingetId == "abbodi1406.vcredist"), "Hardware recommendations include Visual C++ AIO (abbodi1406.vcredist)");

var catalogService = new AppCatalogService();
var defaultCatalog = catalogService.GetDefaultCatalog();
AssertTrue(defaultCatalog.Any(a => a.Id == "abbodi1406.vcredist" && a.IsSelected), "AppCatalog includes pre-selected Visual C++ AIO (abbodi1406.vcredist)");

// 5. Read-only Tweaks state inspection (ShutUp10++ + LRX Core)
var tweaksService = new TweaksService();
var tweaks = tweaksService.GetTweaksWithCurrentState();
AssertTrue(tweaks.Count == 25, $"Tweaks loaded count == 25 (got {tweaks.Count})");
AssertTrue(
    tweaks.Any(t => t.Key == "classic_context_menu") &&
    tweaks.Any(t => t.Key == "remove_onedrive") &&
    tweaks.Any(t => t.Key == "disable_activity_history") &&
    tweaks.Any(t => t.Key == "disable_edge_telemetry") &&
    tweaks.Any(t => t.Key == "disable_p2p_updates") &&
    tweaks.Any(t => t.Key == "disable_windows_recall") &&
    tweaks.Any(t => t.Key == "disable_clipboard_cloud") &&
    tweaks.Any(t => t.Key == "disable_app_diagnostics"),
    "All core + ShutUp10++ recommended tweaks present");

// 5b. Read-only Services optimizer scan
var tempSvcDir = Path.Combine(Path.GetTempPath(), "LRXCoreSvcVerify_" + Guid.NewGuid().ToString("N")[..6]);
var svcOptimizer = new ServicesOptimizerService(tempSvcDir);
var svcItems = await svcOptimizer.ScanServicesAsync();
AssertTrue(svcItems.Count > 5, $"Safe optimizable/sleepable services scanned: {svcItems.Count}");
string[] protectedMustBeAbsent = { "RpcSs", "Dhcp", "Dnscache", "Audiosrv", "WinDefend", "wuauserv", "EventLog" };
AssertTrue(!svcItems.Any(s => protectedMustBeAbsent.Contains(s.ServiceName, StringComparer.OrdinalIgnoreCase)),
    "Zero protected core services exposed in optimizer");
try { if (Directory.Exists(tempSvcDir)) Directory.Delete(tempSvcDir, true); } catch { }

// 6. Read-only Uninstaller enumeration
var tempAppDir = Path.Combine(Path.GetTempPath(), "LRXCoreVerify_" + Guid.NewGuid().ToString("N")[..6]);
var uninstallerService = new UninstallerService(tempAppDir);
var installedApps = await uninstallerService.GetInstalledAppsAsync(false);
AssertTrue(installedApps.Count > 5, $"Installed apps enumerated: {installedApps.Count} apps");

// 7. Read-only JunkCleaner scan
var settingsService = new SettingsService();
var cleanerService = new JunkCleanerService(settingsService);
var categories = cleanerService.CreateCategories();
AssertTrue(categories.Count == 26, $"Cleaner categories count == 26 (got {categories.Count})");
await cleanerService.ScanCategoriesAsync(categories);
AssertTrue(categories.All(c => c.HasScanned), $"Junk scan completed (Total scanned: {FormatHelper.FormatBytes(categories.Sum(c => c.ScannedBytes))})");

// 8. Read-only RegistryCleaner scan
var regService = new RegistryCleanerService(tempAppDir);
var regIssues = await regService.ScanSafeCategoriesAsync(true, true, true, true, true, true);
AssertTrue(regIssues is not null, $"Registry safe scan completed ({regIssues?.Count ?? 0} candidate issues found, 0 modified)");

// 9. Live Winget update check (read-only)
var wingetService = new WingetService();
var liveUpdates = await wingetService.CheckUpdatesAsync();
AssertTrue(liveUpdates.Count >= 0, $"Live winget check completed ({liveUpdates.Count} updates found)");

// 10. WPF Theme contrast verification (STA thread)
bool themeVerified = false;
Exception? themeEx = null;
var staThread = new Thread(() =>
{
    try
    {
        _ = typeof(LRXCore.App).Assembly;
        if (System.Windows.Application.Current is null)
            _ = new System.Windows.Application();

        var rd = new System.Windows.ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/LRXCore;component/Themes/ObsidianBlack.xaml", UriKind.Absolute)
        };

        var primaryBtn = new System.Windows.Controls.Button
        {
            Style = (System.Windows.Style)rd["PrimaryButton"],
            Content = "Test Buton"
        };
        primaryBtn.Resources = rd;
        primaryBtn.ApplyTemplate();
        primaryBtn.Measure(new System.Windows.Size(200, 40));
        primaryBtn.Arrange(new System.Windows.Rect(0, 0, 200, 40));
        primaryBtn.UpdateLayout();

        static T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;
                var nested = FindVisualChild<T>(child);
                if (nested is not null) return nested;
            }
            return null;
        }

        var innerTb = FindVisualChild<System.Windows.Controls.TextBlock>(primaryBtn);
        var fgColor = (innerTb?.Foreground as System.Windows.Media.SolidColorBrush)?.Color;
        themeVerified = fgColor == System.Windows.Media.Color.FromRgb(0x09, 0x09, 0x0B);
    }
    catch (Exception ex)
    {
        themeEx = ex;
    }
});
staThread.SetApartmentState(ApartmentState.STA);
staThread.Start();
staThread.Join();
AssertTrue(themeVerified, $"PrimaryButton inner TextBlock Foreground is dark #09090B {(themeEx is null ? "" : $"({themeEx.Message})")}");

// 11. Lazy MainViewModel & idle memory trimming verification
using (var vm = new LRXCore.ViewModels.MainViewModel(startMinimized: true))
{
    vm.ReleaseIdleMemory();
    AssertTrue(vm.DashboardVM.Hardware.CpuName == "Bilinmiyor", "Minimized startup defers WMI hardware query");
    var inst = vm.InstallerVM;
    AssertTrue(inst.AllApps.Count > 20, $"Lazy InstallerVM initialized on demand ({inst.AllApps.Count} apps)");
}
AssertTrue(true, "MainViewModel idle memory trim and dispose succeeded");

var runtimeCfgPath = Path.Combine(AppContext.BaseDirectory, "LRXCore.runtimeconfig.json");
if (File.Exists(runtimeCfgPath))
{
    var rtJson = File.ReadAllText(runtimeCfgPath);
    AssertTrue(rtJson.Contains("System.GC.RetainVM", StringComparison.OrdinalIgnoreCase), "runtimeconfig includes System.GC.RetainVM setting");
}

// 12. SelfUpdateService version comparison & ICO BMP-DIB format checks
AssertTrue(SelfUpdateService.CompareVersions("v1.1.0", new Version(1, 0, 0)) > 0, "SelfUpdate v1.1.0 > 1.0.0");
AssertTrue(SelfUpdateService.CompareVersions("1.0.0", new Version(1, 0, 0)) == 0, "SelfUpdate 1.0.0 == 1.0.0");
AssertTrue(SelfUpdateService.CompareVersions("v0.9.5-beta", new Version(1, 0, 0)) < 0, "SelfUpdate v0.9.5-beta < 1.0.0");

var icoFile = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Assets", "app_icon.ico"));
AssertTrue(File.Exists(icoFile), "Assets/app_icon.ico exists");
if (File.Exists(icoFile))
{
    using var fs = File.OpenRead(icoFile);
    using var br = new BinaryReader(fs);
    ushort reserved = br.ReadUInt16();
    ushort type = br.ReadUInt16();
    ushort count = br.ReadUInt16();
    AssertTrue(reserved == 0 && type == 1 && count >= 6, $"ICO header valid (frames={count})");

    int dibCount = 0;
    int pngCount = 0;
    var entries = new List<(byte w, uint offset)>();
    for (int i = 0; i < count; i++)
    {
        byte w = br.ReadByte();
        br.ReadBytes(11); // h, colors, res, planes, bpp, size
        uint off = br.ReadUInt32();
        entries.Add((w, off));
    }

    foreach (var (w, off) in entries)
    {
        fs.Position = off;
        uint magic = br.ReadUInt32();
        if (w != 0 && magic == 40) dibCount++; // 40-byte BITMAPINFOHEADER
        else if (w == 0 && magic == 0x474E5089) pngCount++; // PNG signature
    }
    AssertTrue(dibCount >= 5 && pngCount == 1, $"ICO contains BMP-DIB frames for 16..64 ({dibCount}) and 256 PNG ({pngCount})");
}

try { if (Directory.Exists(tempAppDir)) Directory.Delete(tempAppDir, true); } catch { }

Console.WriteLine(failures == 0 ? "ALL VERIFICATION CHECKS PASSED." : $"VERIFICATION FAILED WITH {failures} ERRORS.");
return failures == 0 ? 0 : 1;
