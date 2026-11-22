using System.Diagnostics;
using System.IO;
using System.Text;
using LRXCore.Models;
using Microsoft.Win32;

namespace LRXCore.Services;

public sealed class RegistryCleanerService
{
    private readonly string _backupsDir;

    public RegistryCleanerService(string backupsDir)
    {
        _backupsDir = backupsDir;
        Directory.CreateDirectory(_backupsDir);
    }

    public Task<List<RegistryIssue>> ScanSafeCategoriesAsync(
        bool scanUninstall,
        bool scanStartup,
        bool scanSharedDlls,
        bool scanAppPaths,
        bool scanMuiCache,
        bool scanFirewall,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var issues = new List<RegistryIssue>();

            if (scanUninstall)
            {
                ct.ThrowIfCancellationRequested();
                ScanInvalidUninstallEntries(issues);
            }
            if (scanStartup)
            {
                ct.ThrowIfCancellationRequested();
                ScanBrokenStartupEntries(issues);
            }
            if (scanSharedDlls)
            {
                ct.ThrowIfCancellationRequested();
                ScanMissingSharedDlls(issues);
            }
            if (scanAppPaths)
            {
                ct.ThrowIfCancellationRequested();
                ScanInvalidAppPaths(issues);
            }
            if (scanMuiCache)
            {
                ct.ThrowIfCancellationRequested();
                ScanStaleMuiCache(issues);
            }
            if (scanFirewall)
            {
                ct.ThrowIfCancellationRequested();
                ScanStaleFirewallRules(issues);
            }

            return issues;
        }, ct);
    }

    private static void ScanInvalidUninstallEntries(List<RegistryIssue> issues)
    {
        void CheckHive(RegistryHive hive, RegistryView view, string rootLabel)
        {
            const string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(path);
                if (key is null) return;

                foreach (var subName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = key.OpenSubKey(subName);
                        if (sub is null) continue;

                        var displayName = sub.GetValue("DisplayName")?.ToString();
                        var installLoc = sub.GetValue("InstallLocation")?.ToString()?.Trim('"');
                        var uninstall = sub.GetValue("UninstallString")?.ToString()?.Trim();

                        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(uninstall))
                            continue;

                        // Skip MSI GUIDs (msiexec exists in System32)
                        if (uninstall.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var exe = ExtractExecutablePath(uninstall);
                        bool exeMissing = !string.IsNullOrEmpty(exe) && exe.Contains(":\\") && !File.Exists(exe);
                        bool locMissing = !string.IsNullOrWhiteSpace(installLoc) && installLoc.Contains(":\\") && !Directory.Exists(installLoc);

                        if (exeMissing && (string.IsNullOrWhiteSpace(installLoc) || locMissing))
                        {
                            issues.Add(new RegistryIssue
                            {
                                Category = "Geçersiz Kaldırma Kaydı",
                                RootKey = rootLabel,
                                SubKeyPath = $@"{path}\{subName}",
                                ValueName = displayName,
                                ValueData = exe,
                                Reason = "Kaldırıcı dosyası ve kurulum klasörü artık diskte mevcut değil.",
                                DeleteEntireKey = true
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        CheckHive(RegistryHive.LocalMachine, RegistryView.Registry64, "HKLM64");
        CheckHive(RegistryHive.LocalMachine, RegistryView.Registry32, "HKLM32");
        CheckHive(RegistryHive.CurrentUser, RegistryView.Default, "HKCU");
    }

    private static void ScanBrokenStartupEntries(List<RegistryIssue> issues)
    {
        const string runPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        void Check(RegistryHive hive, RegistryView view, string rootLabel, string subPath)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(subPath);
                if (key is null) return;

                foreach (var valName in key.GetValueNames())
                {
                    var raw = key.GetValue(valName)?.ToString();
                    if (string.IsNullOrWhiteSpace(raw)) continue;

                    var exe = ExtractExecutablePath(Environment.ExpandEnvironmentVariables(raw));
                    if (!string.IsNullOrEmpty(exe) && exe.Contains(":\\") && !File.Exists(exe))
                    {
                        issues.Add(new RegistryIssue
                        {
                            Category = "Bozuk Başlangıç (Run)",
                            RootKey = rootLabel,
                            SubKeyPath = subPath,
                            ValueName = valName,
                            ValueData = raw,
                            Reason = $"Başlangıçta çalıştırılmak istenen dosya bulunamıyor: {exe}",
                            DeleteEntireKey = false
                        });
                    }
                }
            }
            catch { }
        }

        Check(RegistryHive.CurrentUser, RegistryView.Default, "HKCU", runPath);
        Check(RegistryHive.LocalMachine, RegistryView.Registry64, "HKLM64", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
        Check(RegistryHive.LocalMachine, RegistryView.Registry32, "HKLM32", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
    }

    private static void ScanMissingSharedDlls(List<RegistryIssue> issues)
    {
        const string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs";
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(path);
            if (key is null) return;

            foreach (var dllPath in key.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(dllPath) || !dllPath.Contains(":\\"))
                    continue;
                if (dllPath.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase))
                    continue; // Leave core Windows entries untouched

                if (!File.Exists(dllPath))
                {
                    issues.Add(new RegistryIssue
                    {
                        Category = "Eksik Paylaşılan DLL",
                        RootKey = "HKLM64",
                        SubKeyPath = path,
                        ValueName = dllPath,
                        ValueData = key.GetValue(dllPath)?.ToString(),
                        Reason = "Kayıtlı paylaşılan DLL dosyası diskten silinmiş.",
                        DeleteEntireKey = false
                    });
                }
            }
        }
        catch { }
    }

    private static void ScanInvalidAppPaths(List<RegistryIssue> issues)
    {
        void Check(RegistryHive hive, RegistryView view, string rootLabel, string subPath)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(subPath);
                if (key is null) return;

                foreach (var appKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = key.OpenSubKey(appKeyName);
                        var raw = sub?.GetValue("")?.ToString();
                        if (string.IsNullOrWhiteSpace(raw)) continue;

                        var exe = ExtractExecutablePath(Environment.ExpandEnvironmentVariables(raw));
                        if (!string.IsNullOrEmpty(exe) && exe.Contains(":\\") && !File.Exists(exe))
                        {
                            issues.Add(new RegistryIssue
                            {
                                Category = "Geçersiz Uygulama Yolu",
                                RootKey = rootLabel,
                                SubKeyPath = $@"{subPath}\{appKeyName}",
                                ValueName = appKeyName,
                                ValueData = exe,
                                Reason = "App Paths kaydının işaret ettiği uygulama dosyası mevcut değil.",
                                DeleteEntireKey = true
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        Check(RegistryHive.LocalMachine, RegistryView.Registry64, "HKLM64", @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
        Check(RegistryHive.CurrentUser, RegistryView.Default, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\App Paths");
    }

    private static void ScanStaleMuiCache(List<RegistryIssue> issues)
    {
        const string muiPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(muiPath);
            if (key is null) return;

            foreach (var valName in key.GetValueNames())
            {
                var idx = valName.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (idx <= 0) continue;
                var exePath = valName[..(idx + 4)];
                if (exePath.Contains(":\\") &&
                    !exePath.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase) &&
                    !File.Exists(exePath))
                {
                    issues.Add(new RegistryIssue
                    {
                        Category = "MUI Önbellek Artığı",
                        RootKey = "HKCU",
                        SubKeyPath = muiPath,
                        ValueName = valName,
                        ValueData = key.GetValue(valName)?.ToString(),
                        Reason = "Silinmiş uygulamaya ait MUI önbellek girdisi.",
                        DeleteEntireKey = false
                    });
                }
            }
        }
        catch { }
    }

    private static void ScanStaleFirewallRules(List<RegistryIssue> issues)
    {
        const string fwPath = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules";
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(fwPath);
            if (key is null) return;

            foreach (var ruleName in key.GetValueNames())
            {
                var val = key.GetValue(ruleName)?.ToString();
                if (string.IsNullOrEmpty(val)) continue;

                // Look for App=C:\...\file.exe inside firewall rule string
                var appIdx = val.IndexOf("|App=", StringComparison.OrdinalIgnoreCase);
                if (appIdx < 0) continue;

                var start = appIdx + 5;
                var end = val.IndexOf('|', start);
                var appPath = end > start ? val[start..end] : val[start..];
                appPath = Environment.ExpandEnvironmentVariables(appPath.Trim());

                if (appPath.Contains(":\\") &&
                    !appPath.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase) &&
                    !appPath.StartsWith("%", StringComparison.Ordinal) &&
                    !File.Exists(appPath))
                {
                    issues.Add(new RegistryIssue
                    {
                        Category = "Geçersiz Güvenlik Duvarı Kuralı",
                        RootKey = "HKLM64",
                        SubKeyPath = fwPath,
                        ValueName = ruleName,
                        ValueData = appPath,
                        Reason = $"Güvenlik duvarı kuralındaki uygulama silinmiş: {appPath}",
                        DeleteEntireKey = false
                    });
                }
            }
        }
        catch { }
    }

    public Task<(int cleaned, int failed, string backupFile)> BackupAndCleanAsync(IEnumerable<RegistryIssue> selectedIssues)
    {
        return Task.Run(() =>
        {
            var toClean = selectedIssues.Where(i => i.IsSelected).ToList();
            if (toClean.Count == 0)
                return (0, 0, string.Empty);

            string backupPath = Path.Combine(_backupsDir, $"RegBackup_{DateTime.Now:yyyyMMdd_HHmmss}.reg");
            CreateRegBackup(toClean, backupPath);

            int cleaned = 0;
            int failed = 0;

            foreach (var issue in toClean)
            {
                try
                {
                    var (hive, view) = ResolveHive(issue.RootKey);
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    if (issue.DeleteEntireKey)
                    {
                        baseKey.DeleteSubKeyTree(issue.SubKeyPath, throwOnMissingSubKey: false);
                    }
                    else if (!string.IsNullOrEmpty(issue.ValueName))
                    {
                        using var sub = baseKey.OpenSubKey(issue.SubKeyPath, writable: true);
                        sub?.DeleteValue(issue.ValueName, throwOnMissingValue: false);
                    }
                    cleaned++;
                }
                catch
                {
                    failed++;
                }
            }

            return (cleaned, failed, backupPath);
        });
    }

    public List<RegistryBackupItem> GetBackups()
    {
        if (!Directory.Exists(_backupsDir))
            return new List<RegistryBackupItem>();

        return Directory.EnumerateFiles(_backupsDir, "*.reg")
            .Select(f => new FileInfo(f))
            .OrderByDescending(fi => fi.CreationTime)
            .Select(fi => new RegistryBackupItem
            {
                FileName = fi.Name,
                FullPath = fi.FullName,
                CreatedAt = fi.CreationTime,
                SizeBytes = fi.Length
            })
            .ToList();
    }

    public async Task<(bool success, string message)> RestoreBackupAsync(string regFilePath)
    {
        if (!File.Exists(regFilePath))
            return (false, "Yedek dosyası bulunamadı.");

        var psi = new ProcessStartInfo("reg.exe", $"import \"{regFilePath}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(psi);
        if (proc is null) return (false, "reg.exe başlatılamadı.");
        var err = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return proc.ExitCode == 0
            ? (true, "Kayıt defteri yedeği başarıyla geri yüklendi.")
            : (false, $"Geri yükleme hatası: {err}");
    }

    private static void CreateRegBackup(List<RegistryIssue> issues, string backupPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();

        var distinctKeys = issues
            .Select(i => (i.RootKey, i.SubKeyPath))
            .Distinct()
            .ToList();

        foreach (var (rootKey, subPath) in distinctKeys)
        {
            var regExeRoot = rootKey switch
            {
                "HKCU" => @"HKEY_CURRENT_USER\" + subPath,
                "HKLM64" => @"HKEY_LOCAL_MACHINE\" + subPath,
                "HKLM32" => @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\" +
                            (subPath.StartsWith(@"SOFTWARE\", StringComparison.OrdinalIgnoreCase) ? subPath[9..] : subPath),
                _ => null
            };
            if (regExeRoot is null) continue;

            try
            {
                var tmp = Path.GetTempFileName();
                var flag = rootKey == "HKLM32" ? "/reg:32" : "/reg:64";
                var psi = new ProcessStartInfo("reg.exe", $"export \"{regExeRoot}\" \"{tmp}\" /y {flag}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(3000);
                if (File.Exists(tmp))
                {
                    var text = File.ReadAllText(tmp).Replace("Windows Registry Editor Version 5.00", "").Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                        sb.AppendLine();
                    }
                    File.Delete(tmp);
                }
            }
            catch { }
        }

        File.WriteAllText(backupPath, sb.ToString(), Encoding.Unicode);
    }

    private static (RegistryHive hive, RegistryView view) ResolveHive(string rootKey) => rootKey switch
    {
        "HKCU" => (RegistryHive.CurrentUser, RegistryView.Default),
        "HKLM32" => (RegistryHive.LocalMachine, RegistryView.Registry32),
        _ => (RegistryHive.LocalMachine, RegistryView.Registry64)
    };

    private static string ExtractExecutablePath(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            if (end > 1) return command[1..end];
        }

        int exeIdx = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIdx > 0)
            return command[..(exeIdx + 4)].Trim();

        int dllIdx = command.IndexOf(".dll", StringComparison.OrdinalIgnoreCase);
        if (dllIdx > 0)
            return command[..(dllIdx + 4)].Trim();

        return command;
    }
}
