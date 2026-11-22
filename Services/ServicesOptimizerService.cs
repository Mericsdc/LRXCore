using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.Json;
using LRXCore.Models;
using Microsoft.Win32;

namespace LRXCore.Services;

public sealed class ServicesOptimizerService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _backupsDir;

    /// <summary>
    /// Critical Windows, Network, Security, Audio, Display, and Anti-Cheat services that must NEVER be touched.
    /// </summary>
    public static readonly HashSet<string> ProtectedCoreServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs", "RpcEptMapper", "DcomLaunch", "LSM", "Power", "PlugPlay", "BrokerInfrastructure",
        "CoreMessagingRegistrar", "StateRepository", "EventLog", "EventSystem", "SENS", "ProfSvc",
        "UserManager", "Schedule", "Winmgmt", "Dhcp", "Dnscache", "NlaSvc", "netprofm", "nsi",
        "Wcmsvc", "BFE", "mpssvc", "WinDefend", "WdNisSvc", "SecurityHealthService", "Sense",
        "wscsvc", "Audiosrv", "AudioEndpointBuilder", "MMCSS", "CryptSvc", "TrustedInstaller",
        "wuauserv", "UsoSvc", "BITS", "InstallService", "Appinfo", "gpsvc", "LanmanWorkstation",
        "LanmanServer", "SamSs", "VaultSvc", "KeyIso", "BthServ", "BTAGService", "hidserv",
        "TabletInputService", "FontCache", "Themes", "ShellHWDetection", "StorSvc", "stisvc",
        "vds", "VSS", "swprv", "WlanSvc", "iphlpsvc", "netman", "nsi", "CryptSvc", "AppXSvc",
        "ClipSVC", "LicenseManager", "TokenBroker", "UserDataSvc", "OneSyncSvc", "CDPUserSvc",
        "NVDisplay.ContainerLocalSystem", "NvTelemetryContainer", "AMDRSServ", "igfxCUIService2.0.0.0",
        "RtkAudioUniversalService", "vgk", "vgc", "EasyAntiCheat", "EasyAntiCheat_EOS", "BEService"
    };

    private sealed record CuratedServiceDef(
        string Name,
        ServiceSafetyTier Tier,
        ServiceActionType Action,
        string Explanation,
        string ImpactNote);

    private static readonly List<CuratedServiceDef> CuratedCatalog = new()
    {
        // Güvenli (Önerilen) — Saf telemetri ve gereksiz servisler
        new("DiagTrack", ServiceSafetyTier.Guvenli, ServiceActionType.Disable,
            "Bağlı Kullanıcı Deneyimleri ve Telemetri servisi; Microsoft'a kullanım ve tanılama verisi gönderir.",
            "Kapatılması Windows kararlılığını etkilemez."),
        new("dmwappushservice", ServiceSafetyTier.Guvenli, ServiceActionType.Disable,
            "WAP Push yönlendirme telemetri mesajlaşma servisi.",
            "Kapatılması güvenlidir."),
        new("diagnosticshub.standardcollector.service", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Microsoft Diagnostics Hub standart toplayıcı servisi.",
            "Sadece geliştirici profillemesinde gerekir; uyutulabilir."),
        new("diagsvc", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Tanılama Yürütme Hizmeti (Diagnostic Execution Service).",
            "Gerekmedikçe arka planda çalışmasına ihtiyaç yoktur."),
        new("WerSvc", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Windows Hata Bildirimi Hizmeti; çöken uygulama dökümlerini Microsoft'a raporlar.",
            "Uyutulması sorun oluşturmaz, ihtiyaç halinde manuel çağrılır."),
        new("MapsBroker", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "İndirilen Haritalar Yöneticisi; Windows Haritalar uygulaması için arka plan güncellemesi yapar.",
            "Masaüstü/oyun sistemlerinde gereksizdir."),
        new("Fax", ServiceSafetyTier.Guvenli, ServiceActionType.Disable,
            "Windows Faks gönderme ve alma servisi.",
            "Faks cihazı kullanmıyorsanız tamamen kapatılabilir."),
        new("RetailDemo", ServiceSafetyTier.Guvenli, ServiceActionType.Disable,
            "Mağaza teşhir (Retail Demo) modu servisi.",
            "Kişisel bilgisayarlarda hiçbir işlevi yoktur."),
        new("RemoteRegistry", ServiceSafetyTier.Guvenli, ServiceActionType.Disable,
            "Uzak kullanıcıların kayıt defterinizi ağ üzerinden değiştirmesine izin veren servis.",
            "Güvenlik açısından kapalı tutulması önerilir."),
        new("wisvc", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Windows Insider Programı servisi.",
            "Insider derlemeleri kullanmıyorsanız uyutulabilir."),
        new("TrkWks", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Dağıtılmış Bağlantı İzleme İstemcisi; NTFS kısayol bağlantılarını ağda takip eder.",
            "Ev bilgisayarlarında uyutulması güvenlidir."),
        new("lfsvc", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Coğrafi Konum (Geolocation) servisi.",
            "Hava durumu/harita konum takibi istemiyorsanız uyutabilirsiniz."),
        new("PhoneSvc", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Telefon Bağlantısı / Telephony durum yönetimi.",
            "PC üzerinden telefon araması yapmıyorsanız uyutulabilir."),
        new("WalletService", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Eski Microsoft Cüzdan hizmeti.",
            "Kullanılmıyorsa uyutulabilir."),
        new("WMPNetworkSvc", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Windows Media Player ağ paylaşım hizmeti.",
            "DLNA medya yayını yapmıyorsanız uyutulabilir."),
        new("icssvc", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Windows Mobil Etkin Nokta (Hotspot) paylaşım hizmeti.",
            "PC'den Wi-Fi hotspot paylaşmıyorsanız uyutulabilir."),
        new("SEMgrSvc", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Ödemeler ve NFC/SE Yöneticisi.",
            "NFC ödeme donanımı olmayan PC'lerde uyutulabilir."),
        new("SmsRouter", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Microsoft Windows SMS Yönlendirici Hizmeti.",
            "SIM kart/LTE modemi olmayan sistemlerde uyutulabilir."),
        new("SCardSvr", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Akıllı Kart (Smart Card) okuyucu servisi.",
            "Akıllı kart donanımı kullanmıyorsanız uyutulabilir."),
        new("SensrSvc", ServiceSafetyTier.Guvenli, ServiceActionType.SleepManual,
            "Sensör İzleme Hizmeti (otomatik ekran parlaklığı/döndürme sensörleri).",
            "Masaüstü veya sensörsüz sistemlerde uyutulabilir."),

        // İsteğe Bağlı (Kısmen Önerilen) — Kullanıma bağlı servisler
        new("Spooler", ServiceSafetyTier.IstegeBagli, ServiceActionType.SleepManual,
            "Yazdırma Biriktiricisi (Print Spooler); yazıcı ve PDF yazıcı kuyruklarını yönetir.",
            "Yazıcı veya 'Microsoft Print to PDF' kullanıyorsanız AÇIK bırakın."),
        new("SysMain", ServiceSafetyTier.IstegeBagli, ServiceActionType.SleepManual,
            "Superfetch; sık açılan uygulamaları önceden RAM'e yükler.",
            "Hızlı NVMe SSD'lerde RAM/disk yükünü azaltmak için uyutulabilir."),
        new("WSearch", ServiceSafetyTier.IstegeBagli, ServiceActionType.SleepManual,
            "Windows Arama indeksleme hizmeti.",
            "Dosya aramasında 'Everything' kullanıyorsanız uyutabilirsiniz; Başlat araması yavaşlayabilir."),
        new("XblAuthManager", ServiceSafetyTier.IstegeBagli, ServiceActionType.SleepManual,
            "Xbox Live Kimlik Doğrulama Yöneticisi.",
            "Xbox Game Pass / Microsoft Store oyunları oynamıyorsanız uyutulabilir."),
        new("XblGameSave", ServiceSafetyTier.IstegeBagli, ServiceActionType.SleepManual,
            "Xbox Live Oyun Kaydı eşitleme servisi.",
            "Xbox bulut kaydı kullanmıyorsanız uyutulabilir."),
        new("XboxNetApiSvc", ServiceSafetyTier.IstegeBagli, ServiceActionType.SleepManual,
            "Xbox Live Ağ Hizmeti.",
            "Xbox App / Game Bar çok oyunculu oyunları kullanmıyorsanız uyutulabilir."),
        new("XboxGipSvc", ServiceSafetyTier.IstegeBagli, ServiceActionType.SleepManual,
            "Xbox Aksesuar Yönetim Hizmeti.",
            "Xbox kontrolcüsü (gamepad) kullanıyorsanız AÇIK bırakın."),
        new("PcaSvc", ServiceSafetyTier.IstegeBagli, ServiceActionType.SleepManual,
            "Program Uyumluluk Yardımcısı (Program Compatibility Assistant).",
            "Eski program uyumluluk uyarılarına ihtiyacınız yoksa uyutulabilir.")
    };

    public ServicesOptimizerService(string backupsDir)
    {
        _backupsDir = backupsDir;
        Directory.CreateDirectory(_backupsDir);
    }

    public Task<List<ServiceOptimizationItem>> ScanServicesAsync() => Task.Run(ScanServicesInternal);

    private static List<ServiceOptimizationItem> ScanServicesInternal()
    {
        var result = new List<ServiceOptimizationItem>();
        var catalogMap = CuratedCatalog.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, State, StartMode, PathName FROM Win32_Service");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(name)) continue;

                // Hard safety boundary: never expose protected core services
                if (ProtectedCoreServices.Contains(name))
                    continue;

                var displayName = obj["DisplayName"]?.ToString()?.Trim() ?? name;
                var state = obj["State"]?.ToString()?.Trim() ?? "Stopped";
                var startMode = obj["StartMode"]?.ToString()?.Trim() ?? "Manual";
                var pathName = obj["PathName"]?.ToString()?.Trim() ?? "";

                if (catalogMap.TryGetValue(name, out var def))
                {
                    bool optimized = def.Action == ServiceActionType.Disable
                        ? startMode.Equals("Disabled", StringComparison.OrdinalIgnoreCase)
                        : startMode.Equals("Manual", StringComparison.OrdinalIgnoreCase) ||
                          startMode.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

                    // Select safe items that are still set to Auto or actively Running
                    bool preSelect = def.Tier == ServiceSafetyTier.Guvenli &&
                                     (!optimized || state.Equals("Running", StringComparison.OrdinalIgnoreCase));

                    result.Add(new ServiceOptimizationItem
                    {
                        ServiceName = name,
                        DisplayName = displayName,
                        PathName = pathName,
                        Tier = def.Tier,
                        RecommendedAction = def.Action,
                        Explanation = def.Explanation,
                        ImpactNote = def.ImpactNote,
                        CurrentState = state,
                        CurrentStartMode = startMode,
                        IsAlreadyOptimized = optimized && !state.Equals("Running", StringComparison.OrdinalIgnoreCase),
                        IsSelected = preSelect
                    });
                }
                else if (IsCandidateUnusedThirdPartyService(name, displayName, state, startMode, pathName))
                {
                    result.Add(new ServiceOptimizationItem
                    {
                        ServiceName = name,
                        DisplayName = displayName,
                        PathName = pathName,
                        Tier = ServiceSafetyTier.DurmusUcuncuParti,
                        RecommendedAction = ServiceActionType.SleepManual,
                        Explanation = "Şu anda çalışmayan 3. parti arka plan hizmeti (Otomatik başlangıçta bekliyor).",
                        ImpactNote = "İhtiyaç duymadığınız bir programa aitse 'Elle (Uyut)' yapmanız açılışı hızlandırır; Windows'u bozmaz.",
                        CurrentState = state,
                        CurrentStartMode = startMode,
                        IsAlreadyOptimized = false,
                        IsSelected = false
                    });
                }
            }
        }
        catch { }

        return result
            .OrderBy(s => s.Tier)
            .ThenBy(s => s.IsAlreadyOptimized)
            .ThenBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool IsCandidateUnusedThirdPartyService(
        string name,
        string displayName,
        string state,
        string startMode,
        string pathName)
    {
        // Only surface stopped third-party services currently set to Auto-start
        if (!state.Equals("Stopped", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!startMode.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return false;

        // Skip Windows core svchost / System32 binaries
        if (string.IsNullOrWhiteSpace(pathName) ||
            pathName.Contains(@"\Windows\System32\", StringComparison.OrdinalIgnoreCase) ||
            pathName.Contains(@"\Windows\SysWOW64\", StringComparison.OrdinalIgnoreCase) ||
            pathName.Contains(@"\Windows\servicing\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Skip hardware drivers and common game anti-cheats
        string combined = $"{name} {displayName} {pathName}";
        string[] protectedVendors =
        {
            "NVIDIA", "AMD", "Radeon", "Intel", "Realtek", "Dolby", "ASUS", "ROG", "Armoury",
            "Microsoft", "Defender", "Steam", "Riot", "Vanguard", "EasyAntiCheat", "BattlEye"
        };
        foreach (var v in protectedVendors)
        {
            if (combined.Contains(v, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public async Task<(int changed, int failed, string snapshotFile)> ApplyOptimizationAsync(
        IEnumerable<ServiceOptimizationItem> selectedItems,
        bool sleepOnly)
    {
        var items = selectedItems.Where(s => s.IsSelected && !ProtectedCoreServices.Contains(s.ServiceName)).ToList();
        if (items.Count == 0)
            return (0, 0, string.Empty);

        // Save snapshot before changing any service
        var snapshot = items.Select(i => new ServiceSnapshotEntry
        {
            ServiceName = i.ServiceName,
            OriginalStartMode = i.CurrentStartMode,
            WasDelayedAuto = IsDelayedAutoStart(i.ServiceName)
        }).ToList();

        string snapshotPath = Path.Combine(_backupsDir, $"ServicesSnapshot_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        await File.WriteAllTextAsync(snapshotPath, JsonSerializer.Serialize(snapshot, JsonOptions));

        int changed = 0;
        int failed = 0;

        foreach (var item in items)
        {
            var action = sleepOnly ? ServiceActionType.SleepManual : item.RecommendedAction;
            string scStart = action == ServiceActionType.Disable ? "disabled" : "demand";

            if (item.CurrentState.Equals("Running", StringComparison.OrdinalIgnoreCase))
                await RunScAsync($"stop \"{item.ServiceName}\"");

            int code = await RunScAsync($"config \"{item.ServiceName}\" start= {scStart}");
            if (code == 0)
            {
                changed++;
                item.CurrentStartMode = action == ServiceActionType.Disable ? "Disabled" : "Manual";
                item.CurrentState = "Stopped";
                item.IsAlreadyOptimized = true;
                item.IsSelected = false;
            }
            else
            {
                failed++;
            }
        }

        return (changed, failed, snapshotPath);
    }

    public async Task<(bool found, int restored, string message)> RestoreLatestSnapshotAsync()
    {
        if (!Directory.Exists(_backupsDir))
            return (false, 0, "Geri yüklenecek hizmet yedeği bulunamadı.");

        var latest = Directory.EnumerateFiles(_backupsDir, "ServicesSnapshot_*.json")
            .OrderByDescending(File.GetCreationTime)
            .FirstOrDefault();

        if (latest is null)
            return (false, 0, "Henüz oluşturulmuş bir hizmet yedeği yok.");

        List<ServiceSnapshotEntry>? entries;
        try
        {
            var json = await File.ReadAllTextAsync(latest);
            entries = JsonSerializer.Deserialize<List<ServiceSnapshotEntry>>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            return (false, 0, $"Yedek dosyası okunamadı: {ex.Message}");
        }

        if (entries is null || entries.Count == 0)
            return (false, 0, "Yedek dosyası boş.");

        int restored = 0;
        foreach (var entry in entries)
        {
            if (ProtectedCoreServices.Contains(entry.ServiceName)) continue;

            string mode = entry.OriginalStartMode switch
            {
                "Auto" => entry.WasDelayedAuto ? "delayed-auto" : "auto",
                "Disabled" => "disabled",
                _ => "demand"
            };

            if (await RunScAsync($"config \"{entry.ServiceName}\" start= {mode}") == 0)
            {
                if (entry.OriginalStartMode == "Auto")
                    await RunScAsync($"start \"{entry.ServiceName}\"");
                restored++;
            }
        }

        return (true, restored, $"{Path.GetFileName(latest)} yedeğinden {restored} hizmet eski durumuna döndürüldü.");
    }

    public bool HasAnySnapshot() =>
        Directory.Exists(_backupsDir) && Directory.EnumerateFiles(_backupsDir, "ServicesSnapshot_*.json").Any();

    private static bool IsDelayedAutoStart(string serviceName)
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            return k?.GetValue("DelayedAutostart") is int d && d == 1;
        }
        catch { return false; }
    }

    private static async Task<int> RunScAsync(string args)
    {
        var psi = new ProcessStartInfo("sc.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(psi);
        if (proc is null) return -1;
        await proc.WaitForExitAsync();
        return proc.ExitCode;
    }
}
