using System.IO;
using System.Text.Json;
using LRXCore.Models;

namespace LRXCore.Services;

public sealed class AppCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<AppCatalogItem> GetDefaultCatalog()
    {
        return new List<AppCatalogItem>
        {
            // Tarayıcılar
            new() { Id = "Google.Chrome", Name = "Google Chrome", Category = "Tarayıcılar", Description = "Hızlı ve yaygın Google web tarayıcısı", IsRecommended = true, IsSelected = true, IconGlyph = "\uE774" },
            new() { Id = "Brave.Brave", Name = "Brave Browser", Category = "Tarayıcılar", Description = "Dahili reklam engelleyicili gizlilik odaklı tarayıcı", IconGlyph = "\uE774" },
            new() { Id = "Mozilla.Firefox", Name = "Mozilla Firefox", Category = "Tarayıcılar", Description = "Açık kaynaklı bağımsız web tarayıcısı", IconGlyph = "\uE774" },

            // İletişim
            new() { Id = "Discord.Discord", Name = "Discord", Category = "İletişim", Description = "Sesli, görüntülü ve metin tabanlı topluluk platformu", IsRecommended = true, IsSelected = true, IconGlyph = "\uE8BD" },
            new() { Id = "9NKSQGP7F2NH", Name = "WhatsApp", Category = "İletişim", Description = "Microsoft Store resmi WhatsApp masaüstü uygulaması", Source = "msstore", IsRecommended = true, IsSelected = true, IconGlyph = "\uE8BD" },
            new() { Id = "Telegram.TelegramDesktop", Name = "Telegram Desktop", Category = "İletişim", Description = "Hızlı ve bulut tabanlı mesajlaşma istemcisi", IconGlyph = "\uE8BD" },

            // Yapay Zeka
            new() { Id = "Anthropic.Claude", Name = "Claude", Category = "Yapay Zeka", Description = "Anthropic Claude resmi Windows masaüstü uygulaması", IsRecommended = true, IsSelected = true, IconGlyph = "\uE99A" },
            new() { Id = "OpenAI.ChatGPT", Name = "ChatGPT", Category = "Yapay Zeka", Description = "OpenAI ChatGPT resmi Windows masaüstü uygulaması", IsRecommended = true, IsSelected = true, IconGlyph = "\uE99A" },
            new() { Id = "Anysphere.Cursor", Name = "Cursor", Category = "Yapay Zeka", Description = "Yapay zeka destekli modern kod editörü", IconGlyph = "\uE943" },
            new() { Id = "Ollama.Ollama", Name = "Ollama", Category = "Yapay Zeka", Description = "Yerel LLM modellerini çalıştırma motoru", IconGlyph = "\uE99A" },

            // Medya
            new() { Id = "Spotify.Spotify", Name = "Spotify", Category = "Medya", Description = "Müzik ve podcast dinleme platformu", IsRecommended = true, IsSelected = true, IconGlyph = "\uE8D6" },
            new() { Id = "VideoLAN.VLC", Name = "VLC Media Player", Category = "Medya", Description = "Tüm video ve ses formatlarını destekleyen oynatıcı", IconGlyph = "\uE714" },
            new() { Id = "OBSProject.OBSStudio", Name = "OBS Studio", Category = "Medya", Description = "Ekran kaydı ve canlı yayın stüdyosu", IconGlyph = "\uE714" },

            // Oyun
            new() { Id = "Valve.Steam", Name = "Steam", Category = "Oyun", Description = "Valve dijital oyun mağazası ve kütüphanesi", IsRecommended = true, IsSelected = true, IconGlyph = "\uE7FC" },
            new() { Id = "EpicGames.EpicGamesLauncher", Name = "Epic Games Launcher", Category = "Oyun", Description = "Epic Games Store ve Unreal Engine başlatıcısı", IsRecommended = true, IsSelected = true, IconGlyph = "\uE7FC" },
            new() { Id = "ElectronicArts.EADesktop", Name = "EA App", Category = "Oyun", Description = "Electronic Arts PC oyun istemcisi", IconGlyph = "\uE7FC" },
            new() { Id = "Ubisoft.Connect", Name = "Ubisoft Connect", Category = "Oyun", Description = "Ubisoft oyun platformu", IconGlyph = "\uE7FC" },
            new() { Id = "Blizzard.BattleNet", Name = "Battle.net", Category = "Oyun", Description = "Blizzard Entertainment başlatıcısı", IconGlyph = "\uE7FC" },

            // Araçlar & Geliştirici
            new() { Id = "7zip.7zip", Name = "7-Zip", Category = "Araçlar & Geliştirici", Description = "Yüksek sıkıştırma oranlı hafif arşiv yöneticisi", IsRecommended = true, IconGlyph = "\uE8B7" },
            new() { Id = "RARLab.WinRAR", Name = "WinRAR", Category = "Araçlar & Geliştirici", Description = "Klasik RAR/ZIP arşiv yöneticisi", IconGlyph = "\uE8B7" },
            new() { Id = "Notepad++.Notepad++", Name = "Notepad++", Category = "Araçlar & Geliştirici", Description = "Hızlı metin ve kaynak kod düzenleyici", IconGlyph = "\uE70F" },
            new() { Id = "Microsoft.VisualStudioCode", Name = "Visual Studio Code", Category = "Araçlar & Geliştirici", Description = "Microsoft hafif kod geliştirme ortamı", IconGlyph = "\uE943" },
            new() { Id = "Git.Git", Name = "Git", Category = "Araçlar & Geliştirici", Description = "Dağıtık sürüm kontrol sistemi", IconGlyph = "\uE943" },
            new() { Id = "Microsoft.PowerToys", Name = "Microsoft PowerToys", Category = "Araçlar & Geliştirici", Description = "Windows üretkenlik araçları paketi", IconGlyph = "\uE90F" },
            new() { Id = "voidtools.Everything", Name = "Everything", Category = "Araçlar & Geliştirici", Description = "Anlık dosya adı arama motoru", IconGlyph = "\uE721" },
            new() { Id = "ShareX.ShareX", Name = "ShareX", Category = "Araçlar & Geliştirici", Description = "Gelişmiş ekran görüntüsü ve kayıt aracı", IconGlyph = "\uE722" },
            new() { Id = "REALiX.HWiNFO", Name = "HWiNFO64", Category = "Araçlar & Geliştirici", Description = "Detaylı donanım ve sensör izleme aracı", IconGlyph = "\uE950" },
            new() { Id = "Guru3D.Afterburner", Name = "MSI Afterburner", Category = "Araçlar & Geliştirici", Description = "GPU hız aşırtma ve FPS/sıcaklık OSD aracı", IconGlyph = "\uE9D9" },
            new() { Id = "qBittorrent.qBittorrent", Name = "qBittorrent", Category = "Araçlar & Geliştirici", Description = "Reklamsız açık kaynak torrent istemcisi", IconGlyph = "\uE896" },

            // Sürücü & Altyapı
            new() { Id = "abbodi1406.vcredist", Name = "Visual C++ Redistributable Runtimes (Hepsi Bir Arada / AIO)", Category = "Sürücü & Altyapı", Description = "2005-2022 arası tüm x86/x64 C++ paketlerini tek seferde sessizce kurar (abbodi1406 AIO)", IsRecommended = true, IsSelected = true, IconGlyph = "\uE943" },
            new() { Id = "XP8CLZL93F5Z4P", Name = "NVIDIA App", Category = "Sürücü & Altyapı", Description = "NVIDIA GPU sürücü ve oyun optimizasyon uygulaması", Source = "msstore", IconGlyph = "\uE7F4" },
            new() { Id = "Microsoft.VCRedist.2015+.x64", Name = "Visual C++ Redistributable (Sadece 2015-2022 x64)", Category = "Sürücü & Altyapı", Description = "Sadece güncel 64-bit C++ çalışma zamanı paketi", IconGlyph = "\uE943" },
            new() { Id = "Microsoft.VCRedist.2015+.x86", Name = "Visual C++ Redistributable (Sadece 2015-2022 x86)", Category = "Sürücü & Altyapı", Description = "Sadece güncel 32-bit C++ çalışma zamanı paketi", IconGlyph = "\uE943" },
            new() { Id = "Microsoft.DotNet.DesktopRuntime.8", Name = ".NET 8 Desktop Runtime", Category = "Sürücü & Altyapı", Description = ".NET tabanlı uygulamalar için masaüstü çalışma zamanı", IconGlyph = "\uE943" },
            new() { Id = "Microsoft.DirectX", Name = "DirectX End-User Runtime", Category = "Sürücü & Altyapı", Description = "Eski ve yeni nesil oyunlar için DirectX kütüphaneleri", IconGlyph = "\uE7FC" }
        };
    }

    public void SaveProfile(string filePath, IEnumerable<AppCatalogItem> selectedItems)
    {
        var ids = selectedItems.Where(x => x.IsSelected).Select(x => x.Id).ToList();
        var json = JsonSerializer.Serialize(ids, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public List<string> LoadProfileIds(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
    }
}
