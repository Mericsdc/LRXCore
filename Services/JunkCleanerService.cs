using System.IO;
using LRXCore.Helpers;
using LRXCore.Models;

namespace LRXCore.Services;

public sealed class JunkCleanerService
{
    private readonly SettingsService _settingsService;

    public JunkCleanerService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public List<CleanerCategory> CreateCategories()
    {
        return new List<CleanerCategory>
        {
            // Ortak Temizleyici — Windows (matching screenshot structure)
            new() { Id = "win_temp", Name = "Geçici Dosyalar", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE74D", Description = "Kullanıcı ve sistem Temp klasörlerindeki geçici dosyalar", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_logs", Name = "Windows Günlük Dosyaları", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE7C3", Description = "Windows CBS, Panther ve sistem olay günlükleri", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_thumbs", Name = "Küçük Resim Önbelleği", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uEB9F", Description = "Explorer thumbcache_*.db önizleme dosyaları", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_prefetch", Name = "Eski Prefetch Verileri", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE81C", Description = "14 günden eski Prefetch (.pf) dosyaları", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_fontcache", Name = "Font Önbelleği", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE8D2", Description = "Windows yazı tipi önbellek dosyaları", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_syscache", Name = "Windows Sistem Önbelleği", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE770", Description = "Delivery Optimization ve geçici sistem önbellekleri", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_instcache", Name = "Windows Yükleyici Önbelleği", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE7B8", Description = "Yükleyici geçici .tmp ve .log artıkları (kurulum veritabanına dokunulmaz)", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_dumps", Name = "Bellek Dökümleri", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE950", Description = "Minidump, MEMORY.DMP ve uygulama çökme dökümleri", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_wer", Name = "Windows Hata Bildirimi", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE7BA", Description = "WER hata rapor arşiv ve kuyruk dosyaları", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_chkdsk", Name = "Chkdsk Dosya Parçacıkları", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uEDA2", Description = "Disk taramasından kalan FOUND.xxx / .chk parçacıkları", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_oldinst", Name = "Eski Windows Installer", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE8F1", Description = "$WINDOWS.~BT / $GetCurrent gibi eski kurulum artıkları", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_wu", Name = "Windows Güncelleme Dosyaları", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE895", Description = "SoftwareDistribution\\Download içindeki eski güncelleme dosyaları", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_storecache", Name = "Windows Mağazası Uygulama Önbelleği", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE719", Description = "Microsoft Store yerel uygulama önbelleği", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_recycle", Name = "Geri Dönüşüm Kutusu", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE74D", Description = "Geri dönüşüm kutusundaki tüm silinmiş dosyalar", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_recent", Name = "Son Belgeler", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE81C", Description = "Hızlı erişim son kullanılan dosya kısayolları", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_clipboard", Name = "Pano", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE77F", Description = "Bellekte tutulan mevcut pano içeriği", DefaultChecked = true, IsSelected = true },
            new() { Id = "win_custom", Name = "Özel Klasörler", Group = "Windows", Tab = CleanerTab.Common, IconGlyph = "\uE8B7", Description = "Kullanıcı tarafından eklenen özel temizlik yolları", DefaultChecked = false, IsSelected = false },

            // Ortak Temizleyici — Tarayıcı
            new() { Id = "br_ie", Name = "Microsoft IE Önbelleği", Group = "Tarayıcı", Tab = CleanerTab.Common, IconGlyph = "\uE774", Description = "INetCache geçici internet dosyaları", DefaultChecked = true, IsSelected = true },
            new() { Id = "br_edge", Name = "Microsoft Edge Önbelleği", Group = "Tarayıcı", Tab = CleanerTab.Common, IconGlyph = "\uE774", Description = "Edge Cache, Code Cache ve GPUCache", DefaultChecked = true, IsSelected = true },
            new() { Id = "br_chrome", Name = "Google Chrome Önbelleği", Group = "Tarayıcı", Tab = CleanerTab.Common, IconGlyph = "\uE774", Description = "Chrome Cache, Code Cache ve GPUCache (oturumlara dokunulmaz)", DefaultChecked = false, IsSelected = false },
            new() { Id = "br_chromium", Name = "Chromium Temelli Tarayıcı Önbelleği", Group = "Tarayıcı", Tab = CleanerTab.Common, IconGlyph = "\uE774", Description = "Brave, Opera, Opera GX ve Vivaldi önbellekleri", DefaultChecked = false, IsSelected = false },
            new() { Id = "br_firefox", Name = "Mozilla Firefox Önbelleği", Group = "Tarayıcı", Tab = CleanerTab.Common, IconGlyph = "\uE774", Description = "Firefox profil cache2 önbellek dosyaları", DefaultChecked = false, IsSelected = false },

            // Gelişmiş Temizleyici
            new() { Id = "adv_shaders", Name = "NVIDIA / AMD / DirectX Shader Önbelleği", Group = "Uygulama & Sistem", Tab = CleanerTab.Advanced, IconGlyph = "\uE7F4", Description = "Eski grafik gölgelendirici önbellek dosyaları", DefaultChecked = false, IsSelected = false },
            new() { Id = "adv_discord", Name = "Discord Önbelleği", Group = "Uygulama & Sistem", Tab = CleanerTab.Advanced, IconGlyph = "\uE8BD", Description = "Discord medya ve önbellek dosyaları", DefaultChecked = false, IsSelected = false },
            new() { Id = "adv_spotify", Name = "Spotify Önbelleği", Group = "Uygulama & Sistem", Tab = CleanerTab.Advanced, IconGlyph = "\uE8D6", Description = "Spotify yerel ses/kapak önbelleği", DefaultChecked = false, IsSelected = false },
            new() { Id = "adv_steam", Name = "Steam Tarayıcı Önbelleği", Group = "Uygulama & Sistem", Tab = CleanerTab.Advanced, IconGlyph = "\uE7FC", Description = "Steam web görünümü ve geçici http önbellek dosyaları", DefaultChecked = false, IsSelected = false }
        };
    }

    public async Task ScanCategoriesAsync(IEnumerable<CleanerCategory> categories, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            foreach (var cat in categories)
            {
                ct.ThrowIfCancellationRequested();
                var (bytes, count) = MeasureCategory(cat.Id);
                cat.ScannedBytes = bytes;
                cat.ScannedFileCount = count;
                cat.HasScanned = true;
                // Recommend items >= 10 MB (or recycle bin with any files)
                cat.IsRecommended = bytes >= 10L * 1024 * 1024 || (cat.Id == "win_recycle" && count > 0);
            }
        }, ct);
    }

    public async Task<(long freedBytes, int deletedFiles, int skippedFiles)> CleanSelectedAsync(
        IEnumerable<CleanerCategory> categories,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            long freed = 0;
            int deleted = 0;
            int skipped = 0;

            foreach (var cat in categories.Where(c => c.IsSelected))
            {
                ct.ThrowIfCancellationRequested();
                var (f, d, s) = CleanCategory(cat.Id);
                freed += f;
                deleted += d;
                skipped += s;

                // Refresh measurements after cleaning
                var (remBytes, remCount) = MeasureCategory(cat.Id);
                cat.ScannedBytes = remBytes;
                cat.ScannedFileCount = remCount;
                cat.IsRecommended = remBytes >= 10L * 1024 * 1024;
            }

            return (freed, deleted, skipped);
        }, ct);
    }

    public Task<List<LargeFileItem>> ScanLargeFilesAsync(int minSizeMb = 500, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var results = new List<LargeFileItem>();
            long threshold = minSizeMb * 1024L * 1024L;
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

            foreach (var root in roots.Where(Directory.Exists))
            {
                var stack = new Stack<string>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    var current = stack.Pop();
                    try
                    {
                        var dirInfo = new DirectoryInfo(current);
                        if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                            continue;

                        // Skip system AppData noise when looking for user large files
                        if (dirInfo.Name.Equals("AppData", StringComparison.OrdinalIgnoreCase))
                            continue;

                        foreach (var file in dirInfo.EnumerateFiles())
                        {
                            try
                            {
                                if (file.Length >= threshold)
                                {
                                    results.Add(new LargeFileItem
                                    {
                                        FilePath = file.FullName,
                                        FileName = file.Name,
                                        FolderPath = file.DirectoryName ?? "",
                                        SizeBytes = file.Length,
                                        LastModified = file.LastWriteTime
                                    });
                                }
                            }
                            catch { }
                        }

                        foreach (var sub in dirInfo.EnumerateDirectories())
                            stack.Push(sub.FullName);
                    }
                    catch { }
                }
            }

            return results.OrderByDescending(f => f.SizeBytes).Take(100).ToList();
        }, ct);
    }

    private (long bytes, int count) MeasureCategory(string id)
    {
        if (id == "win_recycle")
        {
            var (size, items) = NativeMethods.QueryRecycleBin();
            return (size, (int)Math.Min(items, int.MaxValue));
        }

        if (id == "win_clipboard")
            return (0, 0);

        long totalBytes = 0;
        int totalCount = 0;

        foreach (var target in GetTargetsForCategory(id))
        {
            var (b, c) = MeasureTarget(target);
            totalBytes += b;
            totalCount += c;
        }

        return (totalBytes, totalCount);
    }

    private (long freed, int deleted, int skipped) CleanCategory(string id)
    {
        if (id == "win_recycle")
        {
            var (size, count) = NativeMethods.QueryRecycleBin();
            if (size == 0 && count == 0) return (0, 0, 0);
            bool ok = NativeMethods.EmptyRecycleBinSilent();
            return ok ? (size, (int)Math.Min(count, int.MaxValue), 0) : (0, 0, (int)Math.Min(count, int.MaxValue));
        }

        if (id == "win_clipboard")
        {
            bool cleared = NativeMethods.ClearClipboardSafe();
            return cleared ? (0, 1, 0) : (0, 0, 1);
        }

        long freed = 0;
        int deleted = 0;
        int skipped = 0;

        foreach (var target in GetTargetsForCategory(id))
        {
            var (f, d, s) = ExecuteCleanTarget(target);
            freed += f;
            deleted += d;
            skipped += s;
        }

        return (freed, deleted, skipped);
    }

    private sealed record CleanTarget(string Path, string Pattern = "*", bool Recursive = true, int MinAgeDays = 0, bool DeleteRootDirIfMatches = false);

    private List<CleanTarget> GetTargetsForCategory(string id)
    {
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string sysDrive = Path.GetPathRoot(win) ?? "C:\\";
        string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var targets = new List<CleanTarget>();

        switch (id)
        {
            case "win_temp":
                targets.Add(new(Path.GetTempPath()));
                targets.Add(new(Path.Combine(win, "Temp")));
                break;

            case "win_logs":
                targets.Add(new(Path.Combine(win, "Logs"), "*.log"));
                targets.Add(new(Path.Combine(win, "Panther"), "*.log"));
                targets.Add(new(Path.Combine(win, "System32", "LogFiles"), "*.log"));
                targets.Add(new(Path.Combine(win, "INF"), "*.log", Recursive: false));
                break;

            case "win_thumbs":
                targets.Add(new(Path.Combine(localApp, "Microsoft", "Windows", "Explorer"), "thumbcache_*.db", Recursive: false));
                targets.Add(new(Path.Combine(localApp, "Microsoft", "Windows", "Explorer"), "iconcache_*.db", Recursive: false));
                break;

            case "win_prefetch":
                targets.Add(new(Path.Combine(win, "Prefetch"), "*.pf", Recursive: false, MinAgeDays: 14));
                break;

            case "win_fontcache":
                targets.Add(new(Path.Combine(win, "ServiceProfiles", "LocalService", "AppData", "Local", "FontCache"), "*", Recursive: false));
                break;

            case "win_syscache":
                targets.Add(new(Path.Combine(win, "SoftwareDistribution", "DeliveryOptimization", "Cache")));
                targets.Add(new(Path.Combine(localApp, "Microsoft", "Windows", "Caches")));
                break;

            case "win_instcache":
                targets.Add(new(Path.Combine(win, "Installer"), "*.tmp", Recursive: false));
                targets.Add(new(Path.Combine(win, "Installer"), "*.log", Recursive: false));
                break;

            case "win_dumps":
                targets.Add(new(Path.Combine(win, "Minidump"), "*.dmp"));
                targets.Add(new(win, "MEMORY.DMP", Recursive: false));
                targets.Add(new(Path.Combine(localApp, "CrashDumps")));
                break;

            case "win_wer":
                targets.Add(new(Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportArchive")));
                targets.Add(new(Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportQueue")));
                targets.Add(new(Path.Combine(localApp, "Microsoft", "Windows", "WER")));
                break;

            case "win_chkdsk":
                targets.Add(new(sysDrive, "*.chk", Recursive: false));
                break;

            case "win_oldinst":
                targets.Add(new(Path.Combine(sysDrive, "$WINDOWS.~BT"), DeleteRootDirIfMatches: true));
                targets.Add(new(Path.Combine(sysDrive, "$GetCurrent"), DeleteRootDirIfMatches: true));
                targets.Add(new(Path.Combine(sysDrive, "Windows.old"), DeleteRootDirIfMatches: true));
                break;

            case "win_wu":
                targets.Add(new(Path.Combine(win, "SoftwareDistribution", "Download")));
                break;

            case "win_storecache":
                targets.Add(new(Path.Combine(localApp, "Packages", "Microsoft.WindowsStore_8wekyb3d8bbwe", "LocalCache")));
                break;

            case "win_recent":
                targets.Add(new(Path.Combine(appData, "Microsoft", "Windows", "Recent"), "*.lnk", Recursive: false));
                break;

            case "win_custom":
                foreach (var folder in _settingsService.Current.CustomCleanerFolders)
                {
                    if (!FormatHelper.IsProtectedPath(folder) && Directory.Exists(folder))
                        targets.Add(new(folder));
                }
                break;

            case "br_ie":
                targets.Add(new(Path.Combine(localApp, "Microsoft", "Windows", "INetCache")));
                break;

            case "br_edge":
                AddChromiumCacheTargets(targets, Path.Combine(localApp, "Microsoft", "Edge", "User Data"));
                break;

            case "br_chrome":
                AddChromiumCacheTargets(targets, Path.Combine(localApp, "Google", "Chrome", "User Data"));
                break;

            case "br_chromium":
                AddChromiumCacheTargets(targets, Path.Combine(localApp, "BraveSoftware", "Brave-Browser", "User Data"));
                AddChromiumCacheTargets(targets, Path.Combine(localApp, "Vivaldi", "User Data"));
                AddChromiumCacheTargets(targets, Path.Combine(appData, "Opera Software", "Opera Stable"));
                AddChromiumCacheTargets(targets, Path.Combine(appData, "Opera Software", "Opera GX Stable"));
                break;

            case "br_firefox":
                var ffProfiles = Path.Combine(localApp, "Mozilla", "Firefox", "Profiles");
                if (Directory.Exists(ffProfiles))
                {
                    foreach (var prof in Directory.EnumerateDirectories(ffProfiles))
                        targets.Add(new(Path.Combine(prof, "cache2")));
                }
                break;

            case "adv_shaders":
                targets.Add(new(Path.Combine(localApp, "NVIDIA", "DXCache")));
                targets.Add(new(Path.Combine(localApp, "NVIDIA", "GLCache")));
                targets.Add(new(Path.Combine(localApp, "AMD", "DxCache")));
                targets.Add(new(Path.Combine(localApp, "D3DSCache")));
                break;

            case "adv_discord":
                targets.Add(new(Path.Combine(appData, "discord", "Cache")));
                targets.Add(new(Path.Combine(appData, "discord", "Code Cache")));
                targets.Add(new(Path.Combine(appData, "discord", "GPUCache")));
                break;

            case "adv_spotify":
                targets.Add(new(Path.Combine(localApp, "Spotify", "Storage")));
                targets.Add(new(Path.Combine(localApp, "Spotify", "Browser", "Cache")));
                break;

            case "adv_steam":
                targets.Add(new(Path.Combine(pf86, "Steam", "appcache", "httpcache")));
                targets.Add(new(Path.Combine(localApp, "Steam", "htmlcache")));
                break;
        }

        return targets;
    }

    private static void AddChromiumCacheTargets(List<CleanTarget> targets, string userDataRoot)
    {
        if (!Directory.Exists(userDataRoot)) return;

        string[] profileCandidates = { "Default", "Profile 1", "Profile 2", "Profile 3", "" };
        foreach (var prof in profileCandidates)
        {
            var baseDir = string.IsNullOrEmpty(prof) ? userDataRoot : Path.Combine(userDataRoot, prof);
            if (!Directory.Exists(baseDir)) continue;

            targets.Add(new(Path.Combine(baseDir, "Cache")));
            targets.Add(new(Path.Combine(baseDir, "Code Cache")));
            targets.Add(new(Path.Combine(baseDir, "GPUCache")));
            targets.Add(new(Path.Combine(baseDir, "DawnGraphiteCache")));
            targets.Add(new(Path.Combine(baseDir, "DawnWebGPUCache")));
        }
    }

    private static (long bytes, int count) MeasureTarget(CleanTarget target)
    {
        if (!Directory.Exists(target.Path))
            return (0, 0);

        long bytes = 0;
        int count = 0;
        DateTime? cutoff = target.MinAgeDays > 0 ? DateTime.Now.AddDays(-target.MinAgeDays) : null;

        foreach (var file in EnumerateTargetFiles(target, cutoff))
        {
            try
            {
                bytes += file.Length;
                count++;
            }
            catch { }
        }

        return (bytes, count);
    }

    private static (long freed, int deleted, int skipped) ExecuteCleanTarget(CleanTarget target)
    {
        if (!Directory.Exists(target.Path))
            return (0, 0, 0);

        long freed = 0;
        int deleted = 0;
        int skipped = 0;
        DateTime? cutoff = target.MinAgeDays > 0 ? DateTime.Now.AddDays(-target.MinAgeDays) : null;

        foreach (var file in EnumerateTargetFiles(target, cutoff))
        {
            try
            {
                long len = file.Length;
                file.Attributes = FileAttributes.Normal;
                file.Delete();
                freed += len;
                deleted++;
            }
            catch
            {
                skipped++;
            }
        }

        // Remove empty subdirectories inside the target
        if (target.Recursive && !FormatHelper.IsProtectedPath(target.Path))
        {
            try
            {
                foreach (var sub in Directory.GetDirectories(target.Path, "*", SearchOption.AllDirectories)
                             .OrderByDescending(d => d.Length))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(sub).Any())
                            Directory.Delete(sub);
                    }
                    catch { }
                }

                if (target.DeleteRootDirIfMatches && !Directory.EnumerateFileSystemEntries(target.Path).Any())
                    Directory.Delete(target.Path);
            }
            catch { }
        }

        return (freed, deleted, skipped);
    }

    private static IEnumerable<FileInfo> EnumerateTargetFiles(CleanTarget target, DateTime? cutoff)
    {
        var stack = new Stack<string>();
        stack.Push(target.Path);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            DirectoryInfo dirInfo;
            try
            {
                dirInfo = new DirectoryInfo(current);
                if (!dirInfo.Exists || (dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
            }
            catch { continue; }

            IEnumerable<FileInfo> files;
            try
            {
                files = dirInfo.EnumerateFiles(target.Pattern);
            }
            catch { continue; }

            foreach (var f in files)
            {
                if (cutoff.HasValue)
                {
                    DateTime lastWrite;
                    try { lastWrite = f.LastWriteTime; } catch { continue; }
                    if (lastWrite > cutoff.Value) continue;
                }
                yield return f;
            }

            if (target.Recursive)
            {
                try
                {
                    foreach (var sub in dirInfo.EnumerateDirectories())
                        stack.Push(sub.FullName);
                }
                catch { }
            }
        }
    }
}
