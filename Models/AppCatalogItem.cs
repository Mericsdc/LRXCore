using LRXCore.Helpers;

namespace LRXCore.Models;

public enum AppInstallStatus
{
    Hazir,
    Bekliyor,
    Kuruluyor,
    Tamamlandi,
    ZatenKurulu,
    Hata
}

public sealed class AppCatalogItem : ObservableObject
{
    private bool _isSelected;
    private AppInstallStatus _status = AppInstallStatus.Hazir;
    private string _statusMessage = string.Empty;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Source { get; set; } = "winget";
    public bool IsRecommended { get; set; }
    public string IconGlyph { get; set; } = "\uE756";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public AppInstallStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
                OnPropertyChanged(nameof(StatusText));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string StatusText => Status switch
    {
        AppInstallStatus.Hazir => "Hazır",
        AppInstallStatus.Bekliyor => "Kuyrukta",
        AppInstallStatus.Kuruluyor => "Kuruluyor...",
        AppInstallStatus.Tamamlandi => "Kuruldu",
        AppInstallStatus.ZatenKurulu => "Zaten Kurulu",
        AppInstallStatus.Hata => "Hata",
        _ => ""
    };
}
