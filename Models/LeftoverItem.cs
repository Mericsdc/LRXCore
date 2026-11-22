using LRXCore.Helpers;

namespace LRXCore.Models;

public enum LeftoverType
{
    Folder,
    File,
    Shortcut,
    RegistryKey
}

public enum LeftoverRisk
{
    Guvenli,
    Orta
}

public sealed class LeftoverItem : ObservableObject
{
    private bool _isSelected = true;

    public string Path { get; set; } = string.Empty;
    public string LocationCategory { get; set; } = string.Empty;
    public LeftoverType Type { get; set; }
    public LeftoverRisk Risk { get; set; } = LeftoverRisk.Guvenli;
    public long SizeBytes { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string TypeLabel => Type switch
    {
        LeftoverType.Folder => "Klasör",
        LeftoverType.File => "Dosya",
        LeftoverType.Shortcut => "Kısayol",
        LeftoverType.RegistryKey => "Kayıt Defteri",
        _ => "Diğer"
    };

    public string RiskLabel => Risk == LeftoverRisk.Guvenli ? "Güvenli" : "Orta (İncele)";
    public string SizeDisplay => Type == LeftoverType.RegistryKey ? "Anahtar" : FormatHelper.FormatBytes(SizeBytes);
}
