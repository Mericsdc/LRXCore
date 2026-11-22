using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using LRXCore.Helpers;
using LRXCore.Models;
using Microsoft.Win32;

namespace LRXCore.Services;

public sealed class UninstallerService
{
    private static readonly HashSet<string> ExcludedGenericTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows", "microsoft", "software", "program", "programs", "common", "update", "updates",
        "installer", "runtime", "redistributable", "package", "client", "desktop", "launcher",
        "application", "app", "apps", "system", "driver", "drivers", "tools", "tool", "utility",
        "inc", "llc", "corp", "ltd", "gmbh", "setup", "x64", "x86", "win64", "win32", "bit", "edition"
    };

    private readonly string _backupsDir;

    public UninstallerService(string backupsDir)
    {
        _backupsDir = backupsDir;
        Directory.CreateDirectory(_backupsDir);
    }

    public async Task<List<InstalledAppInfo>> GetInstalledAppsAsync(bool includeSystemComponents = false)
    {
        var win32Task = Task.Run(() => EnumerateWin32Apps(includeSystemComponents));
        var storeTask = EnumerateStoreAppsAsync();

        await Task.WhenAll(win32Task, storeTask);

        var combined = new List<InstalledAppInfo>();
        combined.AddRange(win32Task.Result);
        combined.AddRange(storeTask.Result);

        return combined
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static List<InstalledAppInfo> EnumerateWin32Apps(bool includeSystemComponents)
    {
        var list = new List<InstalledAppInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void ReadRoot(RegistryHive hive, RegistryView view, string subPath, string hivePrefix)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstallKey = baseKey.OpenSubKey(subPath);
                if (uninstallKey is null) return;

                foreach (var name in uninstallKey.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = uninstallKey.OpenSubKey(name);
                        if (sub is null) continue;

                        var displayName = sub.GetValue("DisplayName")?.ToString()?.Trim();
                        if (string.IsNullOrEmpty(displayName)) continue;

                        var sysComp = sub.GetValue("SystemComponent") is int sc && sc == 1;
                        if (sysComp && !includeSystemComponents) continue;

                        if (sub.GetValue("ParentKeyName") is not null || sub.GetValue("ParentDisplayName") is not null)
                            continue; // Skip hotfixes/sub-patches

                        var uninstallStr = sub.GetValue("UninstallString")?.ToString()?.Trim() ?? "";
                        var quietUninstallStr = sub.GetValue("QuietUninstallString")?.ToString()?.Trim() ?? "";
                        if (string.IsNullOrEmpty(uninstallStr) && string.IsNullOrEmpty(quietUninstallStr))
                            continue;

                        var dedupeKey = $"{displayName}|{sub.GetValue("DisplayVersion")}";
                        if (!seen.Add(dedupeKey)) continue;

                        long sizeKb = 0;
                        if (sub.GetValue("EstimatedSize") is int est)
                            sizeKb = est;

                        list.Add(new InstalledAppInfo
                        {
                            Name = displayName,
                            Publisher = sub.GetValue("Publisher")?.ToString()?.Trim() ?? "—",
                            Version = sub.GetValue("DisplayVersion")?.ToString()?.Trim() ?? "—",
                            InstallLocation = sub.GetValue("InstallLocation")?.ToString()?.Trim() ?? "",
                            UninstallString = uninstallStr,
                            QuietUninstallString = quietUninstallStr,
                            RegistryKeyPath = $@"{hivePrefix}\{subPath}\{name}",
                            InstallDate = sub.GetValue("InstallDate")?.ToString()?.Trim() ?? "",
                            EstimatedSizeKb = sizeKb,
                            AppType = InstalledAppType.Win32,
                            IsSystemComponent = sysComp
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        const string uninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        ReadRoot(RegistryHive.LocalMachine, RegistryView.Registry64, uninstallPath, "HKLM64");
        ReadRoot(RegistryHive.LocalMachine, RegistryView.Registry32, uninstallPath, "HKLM32");
        ReadRoot(RegistryHive.CurrentUser, RegistryView.Default, uninstallPath, "HKCU");

        return list;
    }

    private static async Task<List<InstalledAppInfo>> EnumerateStoreAppsAsync()
    {
        var results = new List<InstalledAppInfo>();
        const string script =
            "$ProgressPreference = 'SilentlyContinue'; " +
            "Get-AppxPackage | Where-Object { -not $_.IsFramework -and $_.SignatureKind -ne 'System' -and -not $_.NonRemovable } | " +
            "Select-Object Name, PackageFullName, Publisher, Version, InstallLocation | ConvertTo-Json -Compress";

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return results;
            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();
            await Task.WhenAll(outTask, errTask, p.WaitForExitAsync());
            var json = outTask.Result;

            if (string.IsNullOrWhiteSpace(json)) return results;

            using var doc = JsonDocument.Parse(json);
            var elements = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray()
                : new[] { doc.RootElement }.AsEnumerable();

            foreach (var el in elements)
            {
                var name = el.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                var full = el.TryGetProperty("PackageFullName", out var f) ? f.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(full)) continue;
                if (name.Equals("LRXCore", StringComparison.OrdinalIgnoreCase)) continue;

                results.Add(new InstalledAppInfo
                {
                    Name = BeautifyStoreName(name),
                    Publisher = CleanStorePublisher(el.TryGetProperty("Publisher", out var pub) ? pub.GetString() ?? "" : ""),
                    Version = el.TryGetProperty("Version", out var v) ? v.GetString() ?? "—" : "—",
                    InstallLocation = el.TryGetProperty("InstallLocation", out var loc) ? loc.GetString() ?? "" : "",
                    PackageFullName = full,
                    AppType = InstalledAppType.StoreApp
                });
            }
        }
        catch { }

        return results;
    }

    private static string BeautifyStoreName(string raw)
    {
        var dotIdx = raw.IndexOf('.');
        if (dotIdx > 0 && dotIdx < raw.Length - 1)
        {
            var suffix = raw[(dotIdx + 1)..];
            return suffix.Length >= 3 ? suffix : raw;
        }
        return raw;
    }

    private static string CleanStorePublisher(string pub)
    {
        if (string.IsNullOrWhiteSpace(pub)) return "Microsoft Store";
        var cn = pub.Split(',').FirstOrDefault(s => s.Trim().StartsWith("CN=", StringComparison.OrdinalIgnoreCase));
        return cn is not null ? cn.Trim()[3..] : pub;
    }

    public async Task<(bool ran, string message)> RunNativeUninstallerAsync(InstalledAppInfo app)
    {
        if (app.AppType == InstalledAppType.StoreApp)
        {
            var escaped = app.PackageFullName.Replace("'", "''");
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes($"$ProgressPreference = 'SilentlyContinue'; Remove-AppxPackage -Package '{escaped}' -ErrorAction Stop"));
            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p is null) return (false, "PowerShell başlatılamadı.");
            var err = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            return p.ExitCode == 0
                ? (true, "Mağaza uygulaması kaldırıldı.")
                : (false, $"Kaldırma hatası: {err.Trim()}");
        }

        var cmdStr = !string.IsNullOrWhiteSpace(app.UninstallString) ? app.UninstallString : app.QuietUninstallString;
        if (string.IsNullOrWhiteSpace(cmdStr))
            return (false, "Uygulamaya ait kaldırıcı komutu bulunamadı.");

        // Convert MsiExec /I{GUID} into /X{GUID} for uninstall
        if (cmdStr.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
        {
            var msiArgs = cmdStr.Replace("/I", "/X", StringComparison.OrdinalIgnoreCase);
            var idx = msiArgs.IndexOf("msiexec.exe", StringComparison.OrdinalIgnoreCase);
            var argsPart = idx >= 0
                ? msiArgs[(idx + "msiexec.exe".Length)..].Trim()
                : msiArgs.Replace("msiexec", "", StringComparison.OrdinalIgnoreCase).Trim();

            var psi = new ProcessStartInfo("msiexec.exe", argsPart) { UseShellExecute = true };
            using var proc = Process.Start(psi);
            if (proc is not null)
                await proc.WaitForExitAsync();
            return (true, "MSI kaldırıcı tamamlandı.");
        }

        var (exePath, exeArgs) = SplitCommandLine(cmdStr);
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return (false, $"Kaldırıcı dosyası bulunamadı: {exePath}. Doğrudan artık taraması ile temizleyebilirsiniz.");
        }

        try
        {
            var psi = new ProcessStartInfo(exePath, exeArgs) { UseShellExecute = true };
            using var proc = Process.Start(psi);
            if (proc is not null)
                await proc.WaitForExitAsync();
            return (true, "Resmi kaldırıcı tamamlandı. Artıklar taranıyor...");
        }
        catch (Exception ex)
        {
            return (false, $"Kaldırıcı başlatılamadı: {ex.Message}");
        }
    }

    public Task<List<LeftoverItem>> ScanLeftoversAsync(InstalledAppInfo app) => Task.Run(() => ScanLeftoversInternal(app));

    private static List<LeftoverItem> ScanLeftoversInternal(InstalledAppInfo app)
    {
        var leftovers = new List<LeftoverItem>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var (exactTerms, broadTerms) = BuildSearchTerms(app);

        void AddFsItem(string path, string category, LeftoverType type, LeftoverRisk risk)
        {
            if (FormatHelper.IsProtectedPath(path)) return;
            var norm = FormatHelper.NormalizePath(path);
            if (!seenPaths.Add(norm)) return;

            long size = type == LeftoverType.Folder
                ? FormatHelper.GetDirectorySizeSafe(norm, maxDepth: 4)
                : (File.Exists(norm) ? new FileInfo(norm).Length : 0);

            leftovers.Add(new LeftoverItem
            {
                Path = norm,
                LocationCategory = category,
                Type = type,
                Risk = risk,
                SizeBytes = size,
                IsSelected = risk == LeftoverRisk.Guvenli
            });
        }

        // 1. Remaining InstallLocation
        if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
        {
            AddFsItem(app.InstallLocation, "Kurulum Dizini", LeftoverType.Folder, LeftoverRisk.Guvenli);
        }

        // 2. Scan known app directories (one or two levels deep for Publisher/App)
        var searchRoots = new (string path, string label)[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Program Files"),
            (Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Program Files (x86)"),
            (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppData\\Roaming"),
            (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppData\\Local"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow"), "AppData\\LocalLow"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ProgramData"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"), "Local\\Programs")
        };

        foreach (var (root, label) in searchRoots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    var dirName = Path.GetFileName(dir);
                    if (MatchesAny(dirName, exactTerms))
                    {
                        AddFsItem(dir, label, LeftoverType.Folder, LeftoverRisk.Guvenli);
                    }
                    else if (MatchesAny(dirName, broadTerms))
                    {
                        // Check inside publisher directory for exact app folder first
                        bool foundSpecificChild = false;
                        try
                        {
                            foreach (var child in Directory.EnumerateDirectories(dir))
                            {
                                if (MatchesAny(Path.GetFileName(child), exactTerms))
                                {
                                    AddFsItem(child, label, LeftoverType.Folder, LeftoverRisk.Guvenli);
                                    foundSpecificChild = true;
                                }
                            }
                        }
                        catch { }

                        if (!foundSpecificChild)
                            AddFsItem(dir, label, LeftoverType.Folder, LeftoverRisk.Orta);
                    }
                }
            }
            catch { }
        }

        // 3. Shortcuts in Start Menu & Desktop
        var shortcutRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
        };

        foreach (var root in shortcutRoots.Where(Directory.Exists))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                {
                    var fname = Path.GetFileNameWithoutExtension(file);
                    if (MatchesAny(fname, exactTerms))
                        AddFsItem(file, "Başlat / Masaüstü Kısayolları", LeftoverType.Shortcut, LeftoverRisk.Guvenli);
                }
            }
            catch { }
        }

        // 4. Registry keys
        ScanRegistryScope(RegistryHive.CurrentUser, RegistryView.Default, "HKCU", @"Software", exactTerms, broadTerms, leftovers, seenPaths);
        ScanRegistryScope(RegistryHive.LocalMachine, RegistryView.Registry64, "HKLM64", @"SOFTWARE", exactTerms, broadTerms, leftovers, seenPaths);
        ScanRegistryScope(RegistryHive.LocalMachine, RegistryView.Registry32, "HKLM32", @"SOFTWARE", exactTerms, broadTerms, leftovers, seenPaths);

        // 5. Orphaned Uninstall entry itself
        if (!string.IsNullOrWhiteSpace(app.RegistryKeyPath) && DoesRegistryPathExist(app.RegistryKeyPath))
        {
            if (seenPaths.Add(app.RegistryKeyPath))
            {
                leftovers.Add(new LeftoverItem
                {
                    Path = app.RegistryKeyPath,
                    LocationCategory = "Kaldırma (Uninstall) Kaydı",
                    Type = LeftoverType.RegistryKey,
                    Risk = LeftoverRisk.Guvenli,
                    IsSelected = true
                });
            }
        }

        return leftovers;
    }

    private static void ScanRegistryScope(
        RegistryHive hive,
        RegistryView view,
        string prefix,
        string subPath,
        List<string> exactTerms,
        List<string> broadTerms,
        List<LeftoverItem> leftovers,
        HashSet<string> seen)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var sw = baseKey.OpenSubKey(subPath);
            if (sw is null) return;

            foreach (var name in sw.GetSubKeyNames())
            {
                // Never touch Microsoft / Windows / Classes / Policies core keys
                if (name.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Classes", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Policies", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("WOW6432Node", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fullDisplay = $@"{prefix}\{subPath}\{name}";
                if (MatchesAny(name, exactTerms))
                {
                    if (seen.Add(fullDisplay))
                    {
                        leftovers.Add(new LeftoverItem
                        {
                            Path = fullDisplay,
                            LocationCategory = "Kayıt Defteri (Software)",
                            Type = LeftoverType.RegistryKey,
                            Risk = LeftoverRisk.Guvenli,
                            IsSelected = true
                        });
                    }
                }
                else if (MatchesAny(name, broadTerms))
                {
                    try
                    {
                        using var pubKey = sw.OpenSubKey(name);
                        bool foundChild = false;
                        if (pubKey is not null)
                        {
                            foreach (var child in pubKey.GetSubKeyNames())
                            {
                                if (MatchesAny(child, exactTerms))
                                {
                                    var childFull = $@"{fullDisplay}\{child}";
                                    if (seen.Add(childFull))
                                    {
                                        leftovers.Add(new LeftoverItem
                                        {
                                            Path = childFull,
                                            LocationCategory = "Kayıt Defteri (Software)",
                                            Type = LeftoverType.RegistryKey,
                                            Risk = LeftoverRisk.Guvenli,
                                            IsSelected = true
                                        });
                                    }
                                    foundChild = true;
                                }
                            }
                        }

                        if (!foundChild && seen.Add(fullDisplay))
                        {
                            leftovers.Add(new LeftoverItem
                            {
                                Path = fullDisplay,
                                LocationCategory = "Kayıt Defteri (Yayıncı)",
                                Type = LeftoverType.RegistryKey,
                                Risk = LeftoverRisk.Orta,
                                IsSelected = false
                            });
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    public Task<(int cleanedCount, int failedCount, long freedBytes, string? backupFile)> CleanLeftoversAsync(
        IEnumerable<LeftoverItem> selectedItems)
    {
        return Task.Run(() =>
        {
            var items = selectedItems.Where(i => i.IsSelected).ToList();
            int cleaned = 0;
            int failed = 0;
            long freed = 0;
            string? backupPath = null;

            // Backup registry keys before deletion
            var regItems = items.Where(i => i.Type == LeftoverType.RegistryKey).ToList();
            if (regItems.Count > 0)
            {
                backupPath = Path.Combine(_backupsDir, $"UninstallReg_{DateTime.Now:yyyyMMdd_HHmmss}.reg");
                BackupRegistryKeys(regItems.Select(r => r.Path), backupPath);
            }

            foreach (var item in items)
            {
                try
                {
                    if (item.Type == LeftoverType.Folder)
                    {
                        if (FormatHelper.IsProtectedPath(item.Path)) { failed++; continue; }
                        if (Directory.Exists(item.Path))
                        {
                            Directory.Delete(item.Path, true);
                            freed += item.SizeBytes;
                        }
                        cleaned++;
                    }
                    else if (item.Type is LeftoverType.File or LeftoverType.Shortcut)
                    {
                        if (File.Exists(item.Path))
                        {
                            File.Delete(item.Path);
                            freed += item.SizeBytes;
                        }
                        cleaned++;
                    }
                    else if (item.Type == LeftoverType.RegistryKey)
                    {
                        if (DeleteRegistryPath(item.Path))
                            cleaned++;
                        else
                            failed++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            return (cleaned, failed, freed, backupPath);
        });
    }

    private static (List<string> exactTerms, List<string> broadTerms) BuildSearchTerms(InstalledAppInfo app)
    {
        var exact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var broad = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Clean app name (strip version numbers or parenthesized architecture tags)
        var cleanName = app.Name.Split('(')[0].Trim();
        // Remove trailing version tokens like "1.2.3"
        var tokens = cleanName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .TakeWhile(t => !char.IsDigit(t[0]) || t.Length < 2)
            .ToArray();
        var baseName = tokens.Length > 0 ? string.Join(" ", tokens) : cleanName;

        if (baseName.Length >= 4 && !ExcludedGenericTerms.Contains(baseName))
            exact.Add(baseName);

        if (!string.IsNullOrWhiteSpace(app.InstallLocation))
        {
            var folderName = Path.GetFileName(FormatHelper.NormalizePath(app.InstallLocation));
            if (folderName.Length >= 4 && !ExcludedGenericTerms.Contains(folderName))
                exact.Add(folderName);
        }

        // Single distinctive words from app name (>= 5 chars)
        foreach (var t in tokens)
        {
            var cleanT = t.Trim('.', ',', '-', '_');
            if (cleanT.Length >= 5 && !ExcludedGenericTerms.Contains(cleanT))
                exact.Add(cleanT);
        }

        // Publisher name (kept in broad/medium risk)
        if (!string.IsNullOrWhiteSpace(app.Publisher) && app.Publisher != "—")
        {
            var pubClean = app.Publisher.Split(',')[0].Trim();
            if (pubClean.Length >= 4 && !ExcludedGenericTerms.Contains(pubClean))
                broad.Add(pubClean);
        }

        return (exact.ToList(), broad.ToList());
    }

    private static bool MatchesAny(string candidate, List<string> terms)
    {
        if (string.IsNullOrWhiteSpace(candidate) || terms.Count == 0)
            return false;

        foreach (var term in terms)
        {
            if (candidate.Equals(term, StringComparison.OrdinalIgnoreCase))
                return true;
            if (term.Length >= 6 && candidate.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static (string exe, string args) SplitCommandLine(string cmd)
    {
        cmd = cmd.Trim();
        if (cmd.StartsWith('"'))
        {
            var endQuote = cmd.IndexOf('"', 1);
            if (endQuote > 1)
                return (cmd[1..endQuote], cmd[(endQuote + 1)..].Trim());
        }

        var exeIdx = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIdx > 0)
        {
            var splitAt = exeIdx + 4;
            return (cmd[..splitAt].Trim(), cmd[splitAt..].Trim());
        }

        var spaceIdx = cmd.IndexOf(' ');
        return spaceIdx > 0 ? (cmd[..spaceIdx], cmd[(spaceIdx + 1)..].Trim()) : (cmd, "");
    }

    private static bool DoesRegistryPathExist(string prefixedPath)
    {
        if (!ParsePrefixedRegistryPath(prefixedPath, out var hive, out var view, out var subPath))
            return false;
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var sub = baseKey.OpenSubKey(subPath);
            return sub is not null;
        }
        catch { return false; }
    }

    private static bool DeleteRegistryPath(string prefixedPath)
    {
        if (!ParsePrefixedRegistryPath(prefixedPath, out var hive, out var view, out var subPath))
            return false;
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            baseKey.DeleteSubKeyTree(subPath, throwOnMissingSubKey: false);
            return true;
        }
        catch { return false; }
    }

    private static bool ParsePrefixedRegistryPath(string path, out RegistryHive hive, out RegistryView view, out string subPath)
    {
        hive = RegistryHive.CurrentUser;
        view = RegistryView.Default;
        subPath = string.Empty;

        var idx = path.IndexOf('\\');
        if (idx <= 0) return false;
        var prefix = path[..idx];
        subPath = path[(idx + 1)..];

        switch (prefix)
        {
            case "HKCU":
                hive = RegistryHive.CurrentUser;
                view = RegistryView.Default;
                return true;
            case "HKLM64":
                hive = RegistryHive.LocalMachine;
                view = RegistryView.Registry64;
                return true;
            case "HKLM32":
                hive = RegistryHive.LocalMachine;
                view = RegistryView.Registry32;
                return true;
            default:
                return false;
        }
    }

    private static void BackupRegistryKeys(IEnumerable<string> prefixedPaths, string outputFile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();

        foreach (var p in prefixedPaths)
        {
            var regExePath = p.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase)
                ? @"HKEY_CURRENT_USER\" + p[5..]
                : p.StartsWith("HKLM64\\", StringComparison.OrdinalIgnoreCase)
                    ? @"HKEY_LOCAL_MACHINE\" + p[7..]
                    : p.StartsWith("HKLM32\\", StringComparison.OrdinalIgnoreCase)
                        ? @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\" + p[7..].Replace(@"SOFTWARE\", "", StringComparison.OrdinalIgnoreCase)
                        : null;

            if (regExePath is null) continue;

            try
            {
                var tempFile = Path.GetTempFileName();
                var viewFlag = p.StartsWith("HKLM32\\", StringComparison.OrdinalIgnoreCase) ? "/reg:32" : "/reg:64";
                var psi = new ProcessStartInfo("reg.exe", $"export \"{regExePath}\" \"{tempFile}\" /y {viewFlag}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(3000);
                if (File.Exists(tempFile))
                {
                    var text = File.ReadAllText(tempFile);
                    text = text.Replace("Windows Registry Editor Version 5.00", "").Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                        sb.AppendLine();
                    }
                    File.Delete(tempFile);
                }
            }
            catch { }
        }

        File.WriteAllText(outputFile, sb.ToString(), Encoding.Unicode);
    }
}
