using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using LRXCore.Models;
using Microsoft.Win32;

namespace LRXCore.Services;

public sealed class TweaksService
{
    public List<TweakItem> GetTweaksWithCurrentState()
    {
        var list = new List<TweakItem>
        {
            // Arayüz & Debloat (LRX Core)
            new()
            {
                Key = "classic_context_menu",
                Title = "Windows 11 Klasik Sağ Tık Menüsü",
                Description = "Sağ tık menüsünü doğrudan eski tam Windows 10 görünümüne çevirir (InprocServer32 CLSID).",
                Category = "Arayüz & Gezgin",
                SourceTag = "LRX Core",
                Tier = RecommendationTier.Onerilen,
                RequiresExplorerRestart = true
            },
            new()
            {
                Key = "remove_onedrive",
                Title = "OneDrive'ı Komple Kaldır ve Engelle",
                Description = "OneDrive sürecini durdurur, resmi kaldırıcıyı çalıştırır, Gezgin kısayolunu siler ve tekrar yüklenmesini engeller.",
                Category = "Bloatware Temizliği",
                SourceTag = "LRX Core",
                Tier = RecommendationTier.Onerilen,
                RequiresExplorerRestart = true,
                IsOneShotAction = true,
                ActionButtonText = "Komple Kaldır"
            },

            // ShutUp10++ — Gizlilik (Önerilen)
            new()
            {
                Key = "disable_telemetry",
                Title = "Windows Telemetri ve Tanılama Verilerini Kapat",
                Description = "DiagTrack & dmwappushservice servislerini devre dışı bırakır ve AllowTelemetry ilkesini 0 olarak ayarlar.",
                Category = "Gizlilik",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "disable_ads",
                Title = "Reklam Kimliği, Başlat & Kilit Ekranı Önerilerini Kapat",
                Description = "Reklam Kimliği (Advertising ID), Başlat önerileri, kilit ekranı ipuçları ve sessiz uygulama yüklemelerini kapatır.",
                Category = "Gizlilik",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "disable_activity_history",
                Title = "Etkinlik Geçmişi ve Microsoft Bulut Gönderimini Kapat",
                Description = "Zaman çizelgesi etkinlik kaydı toplama ve Microsoft hesabına etkinlik yüklemeyi durdurur.",
                Category = "Gizlilik",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "disable_app_launch_tracking",
                Title = "Başlat Menüsü Uygulama Başlatma Takibini Kapat",
                Description = "Windows'un hangi uygulamaları ne sıklıkla açtığınızı Başlat önerileri için izlemesini kapatır.",
                Category = "Gizlilik",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "disable_tailored_exp",
                Title = "Tanılama Verileriyle Kişiselleştirilmiş Deneyimleri Kapat",
                Description = "Microsoft'un tanılama verilerinizi kullanarak size özel ipucu ve reklam sunmasını kapatır.",
                Category = "Gizlilik",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "disable_feedback",
                Title = "Windows Geri Bildirim İsteme Sıklığını Kapat",
                Description = "'Windows'u nasıl değerlendirirsiniz?' geri bildirim bildirimlerini tamamen kapatır.",
                Category = "Gizlilik",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "disable_typing_insights",
                Title = "Yazma, Mürekkep Kişiselleştirme ve Sözlük Veri Toplamayı Kapat",
                Description = "Klavye yazım alışkanlıklarının yerel kişiselleştirme sözlüğünde toplanıp gönderilmesini kapatır.",
                Category = "Gizlilik",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "disable_clipboard_cloud",
                Title = "Pano Geçmişinin Cihazlar Arası Bulut Eşitlemesini Kapat",
                Description = "Kopyaladığınız metinlerin Microsoft hesabıyla başka cihazlara senkronize edilmesini kapatır (Win+V yerel çalışır).",
                Category = "Gizlilik",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "disable_app_diagnostics",
                Title = "Uygulamaların Diğer Uygulama Tanılama Bilgisine Erişimini Kapat",
                Description = "Mağaza ve arka plan uygulamalarının sistemdeki diğer uygulamalar hakkında tanılama verisi okumasını engeller.",
                Category = "Uygulama Gizliliği",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "disable_wifi_sense",
                Title = "Wi-Fi Sense Otomatik Hotspot Paylaşımını Kapat",
                Description = "Önerilen açık kablosuz ağlara otomatik bağlanma ve ağ bilgisi paylaşımını kapatır.",
                Category = "Gizlilik",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "disable_wer_reporting",
                Title = "Windows Hata Bildirimi (WER) Otomatik Gönderimini Kapat",
                Description = "Program çökmelerinde Microsoft sunucularına otomatik hata raporu gönderilmesini durdurur.",
                Category = "Gizlilik",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },

            // ShutUp10++ — Edge, Arama & Yapay Zeka
            new()
            {
                Key = "disable_bing_search",
                Title = "Başlat Menüsünde Bing Web Aramasını Kapat",
                Description = "Başlat menüsünde arama yaparken internet/Bing sonuçlarının gelmesini ve sorgu gönderimini engeller.",
                Category = "Edge & AI",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen,
                RequiresExplorerRestart = true
            },
            new()
            {
                Key = "disable_copilot_widgets",
                Title = "Windows Copilot ve Görev Çubuğu Araçlarını (Widgets) Kapat",
                Description = "Görev çubuğundaki Widgets haber akışını ve Windows Copilot entegrasyonunu devre dışı bırakır.",
                Category = "Edge & AI",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen,
                RequiresExplorerRestart = true
            },
            new()
            {
                Key = "disable_windows_recall",
                Title = "Windows Recall AI Ekran Anlık Görüntü Analizini Kapat",
                Description = "Windows AI veri analizi ve otomatik ekran anlık görüntüsü alma özelliğini ilke üzerinden kapatır.",
                Category = "Edge & AI",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "disable_edge_telemetry",
                Title = "Microsoft Edge Telemetri, Startup Boost ve Arka Planı Kapat",
                Description = "Edge tanılama/kişiselleştirme gönderimini, kapalıyken arka planda çalışmasını ve Startup Boost'u kapatır.",
                Category = "Edge & AI",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },

            // ShutUp10++ — Windows Update & Sistem
            new()
            {
                Key = "disable_p2p_updates",
                Title = "Windows Update P2P (Başka PC'lere Güncelleme Paylaşımını) Kapat",
                Description = "Bilgisayarınızın bant genişliğini kullanarak internetteki diğer cihazlara güncelleme dosyası göndermesini kapatır.",
                Category = "Güncelleme & Sistem",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "disable_settings_sync",
                Title = "Windows Ayarlarının Buluta Senkronizasyonunu Kapat",
                Description = "Tema, parola ve Windows tercihlerinin Microsoft hesabıyla otomatik senkronize edilmesini kapatır.",
                Category = "Gizlilik",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.KismenOnerilen
            },
            new()
            {
                Key = "disable_location_tracking",
                Title = "Konum Servisi ve Uygulama Konum İzlemesini Kapat",
                Description = "Windows ve uygulamaların konum geçmişinizi okumasını genel olarak devre dışı bırakır.",
                Category = "Uygulama Gizliliği",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.KismenOnerilen
            },
            new()
            {
                Key = "disable_background_apps",
                Title = "UWP / Mağaza Uygulamalarının Arka Planda Çalışmasını Kapat",
                Description = "Kullanmadığınız Microsoft Store uygulamalarının arka planda RAM ve pil tüketmesini engeller.",
                Category = "Uygulama Gizliliği",
                SourceTag = "ShutUp10++",
                Tier = RecommendationTier.KismenOnerilen
            },

            // Arayüz & Performans (LRX Core)
            new()
            {
                Key = "show_file_ext",
                Title = "Bilinen Dosya Uzantılarını Göster",
                Description = "Dosya Gezgini'nde .exe, .png, .txt gibi uzantıları her zaman görünür yapar.",
                Category = "Arayüz & Gezgin",
                SourceTag = "LRX Core",
                Tier = RecommendationTier.Onerilen,
                RequiresExplorerRestart = true
            },
            new()
            {
                Key = "dark_mode",
                Title = "Sistem ve Uygulamalar İçin Karanlık Tema",
                Description = "Windows sistem arayüzü ve destekleyen tüm uygulamaları koyu moda geçirir.",
                Category = "Arayüz & Gezgin",
                SourceTag = "LRX Core",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "game_mode",
                Title = "Windows Oyun Modu (Game Mode) Aktif",
                Description = "Oyun sırasında arka plan Windows Update bildirimlerini ve kaynak tüketimini azaltır.",
                Category = "Performans",
                SourceTag = "LRX Core",
                Tier = RecommendationTier.Onerilen
            },
            new()
            {
                Key = "ultimate_power",
                Title = "Nihai Performans (Ultimate Performance) Güç Planı",
                Description = "Masaüstü ve yüksek performanslı sistemler için en düşük gecikmeli güç planını aktif eder.",
                Category = "Performans",
                SourceTag = "LRX Core",
                Tier = RecommendationTier.KismenOnerilen
            }
        };

        foreach (var item in list)
            item.IsEnabled = ReadTweakState(item.Key);

        return list;
    }

    public bool ReadTweakState(string key)
    {
        try
        {
            return key switch
            {
                "classic_context_menu" => Registry.CurrentUser.OpenSubKey(
                    @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32") is not null,

                "remove_onedrive" => ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC") == 1 &&
                    !File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\OneDrive\OneDrive.exe")),

                "disable_telemetry" => ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry") == 0,

                "disable_ads" => ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled") == 0 &&
                    ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled") == 0,

                "disable_activity_history" => ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed") == 0 &&
                    ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities") == 0,

                "disable_app_launch_tracking" => ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs") == 0,

                "disable_tailored_exp" => ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled") == 0,

                "disable_feedback" => ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod") == 0,

                "disable_typing_insights" => ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection") == 1 &&
                    ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\Input\TIPC", "Enabled") == 0,

                "disable_clipboard_cloud" => ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Windows\System", "AllowCrossDeviceClipboard") == 0,

                "disable_app_diagnostics" => string.Equals(ReadString(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics", "Value"),
                    "Deny", StringComparison.OrdinalIgnoreCase),

                "disable_wifi_sense" => ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config", "AutoConnectAllowedOEM") == 0,

                "disable_wer_reporting" => ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled") == 1,

                "disable_bing_search" => ReadDword(Registry.CurrentUser,
                    @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions") == 1,

                "disable_copilot_widgets" => ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa") == 0,

                "disable_windows_recall" => ReadDword(Registry.CurrentUser,
                    @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis") == 1 ||
                    ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis") == 1,

                "disable_edge_telemetry" => ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Edge", "PersonalizationReportingEnabled") == 0 &&
                    ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Edge", "DiagnosticData") == 0,

                "disable_p2p_updates" => ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode") == 0,

                "disable_settings_sync" => ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Windows\SettingSync", "DisableSettingSync") == 2,

                "disable_location_tracking" => ReadDword(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation") == 1,

                "disable_background_apps" => ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled") == 1,

                "show_file_ext" => ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt") == 0,

                "dark_mode" => ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme") == 0 &&
                    ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme") == 0,

                "game_mode" => ReadDword(Registry.CurrentUser,
                    @"Software\Microsoft\GameBar", "AllowAutoGameMode") == 1,

                "ultimate_power" => IsUltimatePerformanceActive(),

                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> ApplyTweakAsync(string key, bool enable)
    {
        return await Task.Run(async () =>
        {
            switch (key)
            {
                case "classic_context_menu":
                    const string clsid = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
                    if (enable)
                    {
                        using var sub = Registry.CurrentUser.CreateSubKey($@"{clsid}\InprocServer32", true);
                        sub?.SetValue("", "", RegistryValueKind.String);
                        return "Windows 11 klasik sağ tık menüsü aktif edildi. (Explorer yenilenince geçerli olur)";
                    }
                    else
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(clsid, throwOnMissingSubKey: false);
                        return "Windows 11 modern sağ tık menüsü geri yüklendi.";
                    }

                case "remove_onedrive":
                    return await RemoveOneDriveCompletelyAsync();

                case "disable_telemetry":
                    if (enable)
                    {
                        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0);
                        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1);
                        await RunCommandAsync("sc.exe", "stop DiagTrack");
                        await RunCommandAsync("sc.exe", "config DiagTrack start= disabled");
                        await RunCommandAsync("sc.exe", "stop dmwappushservice");
                        await RunCommandAsync("sc.exe", "config dmwappushservice start= disabled");
                        return "Telemetri servisleri durduruldu ve veri toplama kapatıldı.";
                    }
                    else
                    {
                        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry");
                        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications");
                        await RunCommandAsync("sc.exe", "config DiagTrack start= auto");
                        await RunCommandAsync("sc.exe", "start DiagTrack");
                        return "Telemetri ayarları Windows varsayılanına döndürüldü.";
                    }

                case "disable_ads":
                    int val = enable ? 0 : 1;
                    SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", val);
                    const string cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
                    string[] cdmKeys =
                    {
                        "SubscribedContent-338388Enabled",
                        "SubscribedContent-338389Enabled",
                        "SubscribedContent-338393Enabled",
                        "SubscribedContent-353694Enabled",
                        "SubscribedContent-353696Enabled",
                        "SubscribedContent-310093Enabled",
                        "SubscribedContent-338387Enabled",
                        "SystemPaneSuggestionsEnabled",
                        "SoftLandingEnabled",
                        "SilentInstalledAppsEnabled",
                        "RotatingLockScreenEnabled",
                        "RotatingLockScreenOverlayEnabled",
                        "FeatureManagementEnabled"
                    };
                    foreach (var k in cdmKeys)
                        SetDword(Registry.CurrentUser, cdm, k, val);

                    SetDword(Registry.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "ShowSyncProviderNotifications", val);
                    return enable ? "Sistem reklamları, öneriler ve reklam kimliği kapatıldı." : "Sistem önerileri ve reklam kimliği varsayılana döndürüldü.";

                case "disable_activity_history":
                    const string sysPol = @"SOFTWARE\Policies\Microsoft\Windows\System";
                    if (enable)
                    {
                        SetDword(Registry.LocalMachine, sysPol, "EnableActivityFeed", 0);
                        SetDword(Registry.LocalMachine, sysPol, "PublishUserActivities", 0);
                        SetDword(Registry.LocalMachine, sysPol, "UploadUserActivities", 0);
                        return "Etkinlik geçmişi ve bulut aktivite gönderimi kapatıldı.";
                    }
                    else
                    {
                        DeleteValue(Registry.LocalMachine, sysPol, "EnableActivityFeed");
                        DeleteValue(Registry.LocalMachine, sysPol, "PublishUserActivities");
                        DeleteValue(Registry.LocalMachine, sysPol, "UploadUserActivities");
                        return "Etkinlik geçmişi varsayılana döndürüldü.";
                    }

                case "disable_app_launch_tracking":
                    SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", enable ? 0 : 1);
                    return enable ? "Uygulama başlatma takibi kapatıldı." : "Uygulama başlatma takibi açıldı.";

                case "disable_tailored_exp":
                    SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", enable ? 0 : 1);
                    return enable ? "Kişiselleştirilmiş deneyimler kapatıldı." : "Kişiselleştirilmiş deneyimler açıldı.";

                case "disable_feedback":
                    const string siuf = @"Software\Microsoft\Siuf\Rules";
                    if (enable)
                    {
                        SetDword(Registry.CurrentUser, siuf, "NumberOfSIUFInPeriod", 0);
                        SetDword(Registry.CurrentUser, siuf, "PeriodInNanoSeconds", 0);
                        return "Windows geri bildirim sormayı durdurdu.";
                    }
                    else
                    {
                        DeleteValue(Registry.CurrentUser, siuf, "NumberOfSIUFInPeriod");
                        DeleteValue(Registry.CurrentUser, siuf, "PeriodInNanoSeconds");
                        return "Geri bildirim sıklığı varsayılana döndürüldü.";
                    }

                case "disable_typing_insights":
                    SetDword(Registry.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", enable ? 1 : 0);
                    SetDword(Registry.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", enable ? 1 : 0);
                    SetDword(Registry.CurrentUser, @"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", enable ? 0 : 1);
                    SetDword(Registry.CurrentUser, @"Software\Microsoft\Input\TIPC", "Enabled", enable ? 0 : 1);
                    return enable ? "Yazma ve mürekkep veri toplaması kapatıldı." : "Yazma kişiselleştirmesi varsayılana döndürüldü.";

                case "disable_clipboard_cloud":
                    if (enable)
                        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "AllowCrossDeviceClipboard", 0);
                    else
                        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "AllowCrossDeviceClipboard");
                    return enable ? "Pano bulut eşitlemesi kapatıldı." : "Pano bulut eşitlemesi varsayılana döndürüldü.";

                case "disable_app_diagnostics":
                    SetString(Registry.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics",
                        "Value",
                        enable ? "Deny" : "Allow");
                    return enable ? "Uygulamaların tanılama bilgilerine erişimi engellendi." : "Uygulamaların tanılama erişimi varsayılana döndürüldü.";

                case "disable_wifi_sense":
                    SetDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots", "value", enable ? 0 : 1);
                    SetDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config", "AutoConnectAllowedOEM", enable ? 0 : 1);
                    return enable ? "Wi-Fi Sense otomatik hotspot paylaşımı kapatıldı." : "Wi-Fi Sense varsayılana döndürüldü.";

                case "disable_wer_reporting":
                    const string wer = @"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting";
                    if (enable)
                        SetDword(Registry.LocalMachine, wer, "Disabled", 1);
                    else
                        DeleteValue(Registry.LocalMachine, wer, "Disabled");
                    return enable ? "Windows Hata Bildirimi otomatik gönderimi kapatıldı." : "Hata bildirimi varsayılana döndürüldü.";

                case "disable_bing_search":
                    if (enable)
                        SetDword(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1);
                    else
                        DeleteValue(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions");
                    return enable ? "Başlat menüsü Bing web araması kapatıldı." : "Başlat menüsü web araması açıldı.";

                case "disable_copilot_widgets":
                    if (enable)
                    {
                        SetDword(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
                        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0);
                        return "Copilot ve Görev Çubuğu Araçları (Widgets) kapatıldı.";
                    }
                    else
                    {
                        DeleteValue(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");
                        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 1);
                        return "Copilot ve Widgets varsayılana döndürüldü.";
                    }

                case "disable_windows_recall":
                    const string winAi = @"Software\Policies\Microsoft\Windows\WindowsAI";
                    const string winAiLm = @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI";
                    if (enable)
                    {
                        SetDword(Registry.CurrentUser, winAi, "DisableAIDataAnalysis", 1);
                        SetDword(Registry.LocalMachine, winAiLm, "DisableAIDataAnalysis", 1);
                        return "Windows Recall AI ekran analizi ilke üzerinden kapatıldı.";
                    }
                    else
                    {
                        DeleteValue(Registry.CurrentUser, winAi, "DisableAIDataAnalysis");
                        DeleteValue(Registry.LocalMachine, winAiLm, "DisableAIDataAnalysis");
                        return "Windows AI ilkesi varsayılana döndürüldü.";
                    }

                case "disable_edge_telemetry":
                    const string edgePol = @"SOFTWARE\Policies\Microsoft\Edge";
                    string[] edgeKeys =
                    {
                        "PersonalizationReportingEnabled",
                        "DiagnosticData",
                        "MetricsReportingEnabled",
                        "StartupBoostEnabled",
                        "BackgroundModeEnabled",
                        "UserFeedbackAllowed"
                    };
                    if (enable)
                    {
                        foreach (var ek in edgeKeys)
                            SetDword(Registry.LocalMachine, edgePol, ek, 0);
                        return "Microsoft Edge telemetri, Startup Boost ve arka plan modu kapatıldı.";
                    }
                    else
                    {
                        foreach (var ek in edgeKeys)
                            DeleteValue(Registry.LocalMachine, edgePol, ek);
                        return "Microsoft Edge ilkeleri varsayılana döndürüldü.";
                    }

                case "disable_p2p_updates":
                    const string doPol = @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
                    if (enable)
                        SetDword(Registry.LocalMachine, doPol, "DODownloadMode", 0);
                    else
                        DeleteValue(Registry.LocalMachine, doPol, "DODownloadMode");
                    return enable ? "Windows Update P2P bant genişliği paylaşımı kapatıldı." : "Delivery Optimization varsayılana döndürüldü.";

                case "disable_settings_sync":
                    const string syncPol = @"SOFTWARE\Policies\Microsoft\Windows\SettingSync";
                    if (enable)
                    {
                        SetDword(Registry.LocalMachine, syncPol, "DisableSettingSync", 2);
                        SetDword(Registry.LocalMachine, syncPol, "DisableSettingSyncUserOverride", 1);
                        return "Windows ayarları bulut eşitlemesi kapatıldı.";
                    }
                    else
                    {
                        DeleteValue(Registry.LocalMachine, syncPol, "DisableSettingSync");
                        DeleteValue(Registry.LocalMachine, syncPol, "DisableSettingSyncUserOverride");
                        return "Ayar senkronizasyonu varsayılana döndürüldü.";
                    }

                case "disable_location_tracking":
                    const string locPol = @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors";
                    if (enable)
                        SetDword(Registry.LocalMachine, locPol, "DisableLocation", 1);
                    else
                        DeleteValue(Registry.LocalMachine, locPol, "DisableLocation");
                    return enable ? "Konum izleme kapatıldı." : "Konum servisi varsayılana döndürüldü.";

                case "disable_background_apps":
                    SetDword(Registry.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                        "GlobalUserDisabled",
                        enable ? 1 : 0);
                    return enable ? "UWP arka plan uygulamaları kısıtlandı." : "Arka plan uygulamaları varsayılana döndürüldü.";

                case "show_file_ext":
                    SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", enable ? 0 : 1);
                    return enable ? "Dosya uzantıları görünür yapıldı." : "Bilinen dosya uzantıları gizlendi.";

                case "dark_mode":
                    int lightVal = enable ? 0 : 1;
                    SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", lightVal);
                    SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", lightVal);
                    return enable ? "Sistem ve uygulamalar koyu temaya geçirildi." : "Açık tema aktif edildi.";

                case "game_mode":
                    int gmVal = enable ? 1 : 0;
                    SetDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", gmVal);
                    SetDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", gmVal);
                    return enable ? "Windows Oyun Modu aktif edildi." : "Oyun Modu kapatıldı.";

                case "ultimate_power":
                    if (enable)
                        return await EnableUltimatePerformanceAsync();
                    else
                    {
                        await RunCommandAsync("powercfg.exe", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e"); // Balanced
                        return "Güç planı Dengeli (Balanced) moduna döndürüldü.";
                    }

                default:
                    return "Bilinmeyen ayar.";
            }
        });
    }

    public async Task<string> RemoveOneDriveCompletelyAsync()
    {
        var log = new StringBuilder();

        // 1. Kill running processes
        foreach (var procName in new[] { "OneDrive", "OneDriveSetup" })
        {
            foreach (var p in Process.GetProcessesByName(procName))
            {
                try { p.Kill(true); p.WaitForExit(3000); } catch { }
            }
        }

        // 2. Run official setup /uninstall
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string[] setups =
        {
            Path.Combine(winDir, "System32", "OneDriveSetup.exe"),
            Path.Combine(winDir, "SysWOW64", "OneDriveSetup.exe")
        };

        foreach (var exe in setups)
        {
            if (File.Exists(exe))
            {
                await RunCommandAsync(exe, "/uninstall");
                log.AppendLine($"Kaldırıcı çalıştırıldı: {exe}");
            }
        }

        // 3. Policy to block reinstall / sync
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", 1);

        // 4. Hide from Explorer navigation pane
        SetDword(Registry.CurrentUser, @"Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}", "System.IsPinnedToNameSpaceTree", 0);
        SetDword(Registry.CurrentUser, @"Software\Classes\WOW6432Node\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}", "System.IsPinnedToNameSpaceTree", 0);

        // 5. Remove leftover app folders (never blindly wipe user's personal OneDrive docs if non-empty)
        string[] safeJunkDirs =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "OneDrive"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft OneDrive"),
            Path.Combine(Path.GetPathRoot(winDir) ?? "C:\\", "OneDriveTemp")
        };

        foreach (var dir in safeJunkDirs)
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        string userOneDrive = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive");
        if (Directory.Exists(userOneDrive))
        {
            bool hasFiles = Directory.EnumerateFileSystemEntries(userOneDrive)
                .Any(f => !Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase));
            if (!hasFiles)
            {
                try { Directory.Delete(userOneDrive, true); } catch { }
            }
            else
            {
                log.AppendLine("Not: Kullanıcı klasöründeki OneDrive dizininde kişisel dosyalar bulunduğu için veri kaybını önlemek adına korundu.");
            }
        }

        // 6. Remove startup entry if any
        DeleteValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "OneDrive");

        log.Insert(0, "OneDrive komple kaldırıldı, Gezgin kısayolu gizlendi ve ilke ile engellendi. ");
        return log.ToString().Trim();
    }

    public async Task RestartExplorerAsync()
    {
        await Task.Run(() =>
        {
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { }
            }
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
        });
    }

    public async Task<(bool success, string message)> CreateRestorePointAsync()
    {
        var (code, output) = await RunPowerShellAsync(
            "Enable-ComputerRestore -Drive $env:SystemDrive -ErrorAction SilentlyContinue; " +
            "Checkpoint-Computer -Description 'LRX Core Ayar Oncesi Yedek' -RestorePointType 'MODIFY_SETTINGS'");

        return code == 0
            ? (true, "Sistem Geri Yükleme Noktası başarıyla oluşturuldu.")
            : (false, $"Geri yükleme noktası oluşturulamadı (Windows son 24 saatte zaten bir nokta oluşturmuş olabilir). Detay: {output}");
    }

    public async Task<List<BloatwareAppItem>> GetInstalledBloatwareAsync()
    {
        var knownBloat = new Dictionary<string, (string display, string desc)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Clipchamp.Clipchamp"] = ("Clipchamp", "Microsoft video düzenleyici"),
            ["Microsoft.BingNews"] = ("Microsoft Haberler (Bing News)", "Haber akışı uygulaması"),
            ["Microsoft.BingWeather"] = ("Hava Durumu (Bing Weather)", "Hava durumu uygulaması"),
            ["Microsoft.MicrosoftSolitaireCollection"] = ("Microsoft Solitaire Collection", "Ön yüklü kart oyunları"),
            ["Microsoft.WindowsFeedbackHub"] = ("Geri Bildirim Merkezi", "Microsoft geri bildirim aracı"),
            ["Microsoft.GetHelp"] = ("Yardım Al (Get Help)", "Microsoft destek uygulaması"),
            ["Microsoft.Getstarted"] = ("İpuçları (Tips)", "Windows başlangıç rehberi"),
            ["Microsoft.People"] = ("Kişiler (People)", "Kişiler uygulaması"),
            ["Microsoft.PowerAutomateDesktop"] = ("Power Automate Desktop", "Otomasyon aracı"),
            ["Microsoft.Todos"] = ("Microsoft To Do", "Yapılacaklar listesi"),
            ["Microsoft.ZuneVideo"] = ("Filmler ve TV", "Medya oynatıcı"),
            ["Microsoft.ZuneMusic"] = ("Medya Oynatıcı", "Müzik/Medya uygulaması"),
            ["Microsoft.549981C3F5F10"] = ("Cortana", "Eski dijital asistan"),
            ["MicrosoftCorporationII.QuickAssist"] = ("Hızlı Yardım", "Uzaktan destek aracı"),
            ["Microsoft.WindowsMaps"] = ("Haritalar", "Windows Haritalar uygulaması"),
            ["Microsoft.Copilot"] = ("Microsoft Copilot Uygulaması", "Ön yüklü Copilot paketi")
        };

        const string ps = "Get-AppxPackage | Select-Object Name, PackageFullName | ConvertTo-Json -Compress";
        var (code, json) = await RunPowerShellAsync(ps);
        var items = new List<BloatwareAppItem>();

        if (code != 0 || string.IsNullOrWhiteSpace(json))
            return items;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var elements = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray()
                : new[] { doc.RootElement }.AsEnumerable();

            foreach (var el in elements)
            {
                var name = el.GetProperty("Name").GetString() ?? "";
                var full = el.GetProperty("PackageFullName").GetString() ?? "";
                if (knownBloat.TryGetValue(name, out var info))
                {
                    items.Add(new BloatwareAppItem
                    {
                        PackageName = name,
                        FullName = full,
                        DisplayName = info.display,
                        Description = info.desc,
                        IsSelected = false
                    });
                }
            }
        }
        catch { }

        return items.OrderBy(x => x.DisplayName).ToList();
    }

    public async Task<int> RemoveBloatwareAsync(IEnumerable<BloatwareAppItem> items)
    {
        int removed = 0;
        foreach (var item in items)
        {
            var escaped = item.FullName.Replace("'", "''");
            var (code, _) = await RunPowerShellAsync($"Remove-AppxPackage -Package '{escaped}' -ErrorAction Stop");
            if (code == 0)
            {
                item.IsInstalled = false;
                item.IsSelected = false;
                removed++;
            }
        }
        return removed;
    }

    private static bool IsUltimatePerformanceActive()
    {
        try
        {
            var psi = new ProcessStartInfo("powercfg.exe", "/getactivescheme")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var p = Process.Start(psi);
            var output = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit(2000);
            return output.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase) ||
                   output.Contains("Nihai Performans", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static async Task<string> EnableUltimatePerformanceAsync()
    {
        var (_, listOut) = await RunCommandCaptureAsync("powercfg.exe", "/list");
        var guid = FindUltimateSchemeGuid(listOut);

        if (string.IsNullOrEmpty(guid))
        {
            await RunCommandAsync("powercfg.exe", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");
            (_, listOut) = await RunCommandCaptureAsync("powercfg.exe", "/list");
            guid = FindUltimateSchemeGuid(listOut);
        }

        if (!string.IsNullOrEmpty(guid))
        {
            await RunCommandAsync("powercfg.exe", $"/setactive {guid}");
            return "Nihai Performans güç planı aktif edildi.";
        }

        // Fallback to High Performance (8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c)
        await RunCommandAsync("powercfg.exe", "-duplicatescheme 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        await RunCommandAsync("powercfg.exe", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        return "Yüksek Performans güç planı aktif edildi.";
    }

    private static string? FindUltimateSchemeGuid(string powerCfgList)
    {
        foreach (var line in powerCfgList.Split('\n'))
        {
            if (line.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Nihai Performans", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (Guid.TryParse(part, out var g))
                        return g.ToString();
                }
            }
        }
        return null;
    }

    private static int? ReadDword(RegistryKey hive, string subKey, string name)
    {
        using var k = hive.OpenSubKey(subKey);
        var val = k?.GetValue(name);
        return val is int i ? i : null;
    }

    private static string? ReadString(RegistryKey hive, string subKey, string name)
    {
        using var k = hive.OpenSubKey(subKey);
        return k?.GetValue(name)?.ToString();
    }

    private static void SetDword(RegistryKey hive, string subKey, string name, int value)
    {
        using var k = hive.CreateSubKey(subKey, true);
        k?.SetValue(name, value, RegistryValueKind.DWord);
    }

    private static void SetString(RegistryKey hive, string subKey, string name, string value)
    {
        using var k = hive.CreateSubKey(subKey, true);
        k?.SetValue(name, value, RegistryValueKind.String);
    }

    private static void DeleteValue(RegistryKey hive, string subKey, string name)
    {
        using var k = hive.OpenSubKey(subKey, true);
        k?.DeleteValue(name, throwOnMissingValue: false);
    }

    private static async Task RunCommandAsync(string fileName, string args)
    {
        await RunCommandCaptureAsync(fileName, args);
    }

    private static async Task<(int exitCode, string output)> RunCommandCaptureAsync(string fileName, string args)
    {
        var psi = new ProcessStartInfo(fileName, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = Process.Start(psi);
        if (p is null) return (-1, "");
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode, $"{stdout}\n{stderr}".Trim());
    }

    private static async Task<(int exitCode, string output)> RunPowerShellAsync(string command)
    {
        var fullCmd = $"$ProgressPreference = 'SilentlyContinue'; {command}";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(fullCmd));
        return await RunCommandCaptureAsync("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}");
    }
}
