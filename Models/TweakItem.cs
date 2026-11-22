using LRXCore.Helpers;

namespace LRXCore.Models;

public enum RecommendationTier
{
    Onerilen,
    KismenOnerilen,
    Opsiyonel
}

public sealed class TweakItem : ObservableObject
{
    private bool _isEnabled;
    private bool _isBusy;

    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SourceTag { get; set; } = "LRX Core";
    public RecommendationTier Tier { get; set; } = RecommendationTier.Onerilen;
    public bool RequiresExplorerRestart { get; set; }
    public bool IsOneShotAction { get; set; }
    public string ActionButtonText { get; set; } = "Uygula";

    public bool IsRecommended => Tier == RecommendationTier.Onerilen;
    public bool IsPartiallyRecommended => Tier == RecommendationTier.KismenOnerilen;

    public string RecommendationLabel => Tier switch
    {
        RecommendationTier.Onerilen => "Önerilen",
        RecommendationTier.KismenOnerilen => "Kısmen Önerilen",
        _ => "Opsiyonel"
    };

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
                OnPropertyChanged(nameof(StateLabel));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StateLabel => IsEnabled ? "Aktif" : "Varsayılan";
}

public sealed class BloatwareAppItem : ObservableObject
{
    private bool _isSelected;
    private bool _isInstalled = true;

    public string PackageName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set => SetProperty(ref _isInstalled, value);
    }
}
