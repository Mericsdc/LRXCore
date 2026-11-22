using LRXCore.Helpers;

namespace LRXCore.Models;

public enum ServiceSafetyTier
{
    Guvenli,
    IstegeBagli,
    DurmusUcuncuParti
}

public enum ServiceActionType
{
    SleepManual,   // sc config start= demand (Elle / Uyut)
    Disable        // sc config start= disabled (Sadece saf telemetri / çöp servisler)
}

public sealed class ServiceOptimizationItem : ObservableObject
{
    private bool _isSelected;
    private string _currentState = string.Empty;
    private string _currentStartMode = string.Empty;
    private bool _isAlreadyOptimized;

    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PathName { get; set; } = string.Empty;
    public ServiceSafetyTier Tier { get; set; }
    public ServiceActionType RecommendedAction { get; set; } = ServiceActionType.SleepManual;
    public string Explanation { get; set; } = string.Empty;
    public string ImpactNote { get; set; } = string.Empty;

    public string CurrentState
    {
        get => _currentState;
        set
        {
            if (SetProperty(ref _currentState, value))
                OnPropertyChanged(nameof(StateDisplay));
        }
    }

    public string CurrentStartMode
    {
        get => _currentStartMode;
        set
        {
            if (SetProperty(ref _currentStartMode, value))
                OnPropertyChanged(nameof(StartModeDisplay));
        }
    }

    public bool IsAlreadyOptimized
    {
        get => _isAlreadyOptimized;
        set
        {
            if (SetProperty(ref _isAlreadyOptimized, value))
                OnPropertyChanged(nameof(OptimizationStatusLabel));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string StateDisplay => CurrentState.Equals("Running", StringComparison.OrdinalIgnoreCase)
        ? "Çalışıyor"
        : "Durduruldu";

    public string StartModeDisplay => CurrentStartMode switch
    {
        "Auto" => "Otomatik",
        "Manual" => "Elle (Uyuyor)",
        "Disabled" => "Devre Dışı",
        _ => CurrentStartMode
    };

    public string RecommendedActionLabel => RecommendedAction == ServiceActionType.Disable
        ? "Devre Dışı Bırak"
        : "Uyut (Elle)";

    public string TierLabel => Tier switch
    {
        ServiceSafetyTier.Guvenli => "Güvenli (Önerilen)",
        ServiceSafetyTier.IstegeBagli => "İsteğe Bağlı",
        _ => "Durmuş 3. Parti"
    };

    public bool IsSafeTier => Tier == ServiceSafetyTier.Guvenli;
    public bool IsOptionalTier => Tier == ServiceSafetyTier.IstegeBagli;
    public bool IsThirdPartyTier => Tier == ServiceSafetyTier.DurmusUcuncuParti;

    public string OptimizationStatusLabel => IsAlreadyOptimized ? "Uyuyor / Optimize" : "Optimize Edilebilir";
}

public sealed class ServiceSnapshotEntry
{
    public string ServiceName { get; set; } = string.Empty;
    public string OriginalStartMode { get; set; } = string.Empty; // Auto, Manual, Disabled
    public bool WasDelayedAuto { get; set; }
}
