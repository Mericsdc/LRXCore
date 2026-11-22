using System.Collections.ObjectModel;
using System.Windows.Input;
using LRXCore.Helpers;
using LRXCore.Models;
using LRXCore.Services;

namespace LRXCore.ViewModels;

public sealed class TweaksViewModel : ObservableObject
{
    private readonly TweaksService _tweaksService;
    private readonly List<TweakItem> _allTweaks = new();

    private bool _isBusy;
    private bool _suppressToggleSync;
    private string _statusMessage = "O&O ShutUp10++ önerileri ve Win11 ayarları mevcut durumlarıyla yüklendi.";
    private string _searchText = string.Empty;
    private string _selectedFilter = "Tümü";

    public ObservableCollection<TweakItem> FilteredTweaks { get; } = new();
    public ObservableCollection<BloatwareAppItem> BloatwareApps { get; } = new();

    public List<string> FilterOptions { get; } = new()
    {
        "Tümü",
        "Sadece Önerilenler",
        "ShutUp10++",
        "Gizlilik",
        "Edge & AI",
        "Arayüz & Performans"
    };

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
    }

    public string SelectedFilter
    {
        get => _selectedFilter;
        set { if (SetProperty(ref _selectedFilter, value)) ApplyFilter(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int RecommendedCount => _allTweaks.Count(t => t.IsRecommended);
    public int RecommendedActiveCount => _allTweaks.Count(t => t.IsRecommended && t.IsEnabled);

    public ICommand ToggleTweakCommand { get; }
    public ICommand OneShotTweakCommand { get; }
    public ICommand ApplyRecommendedPackCommand { get; }
    public ICommand ApplyRecommendedPlusPartialCommand { get; }
    public ICommand CreateRestorePointCommand { get; }
    public ICommand RestartExplorerCommand { get; }
    public ICommand ScanBloatwareCommand { get; }
    public ICommand RemoveSelectedBloatwareCommand { get; }

    public TweaksViewModel(TweaksService tweaksService)
    {
        _tweaksService = tweaksService;

        ToggleTweakCommand = new AsyncRelayCommand(p => OnToggleChangedAsync(p as TweakItem));
        OneShotTweakCommand = new AsyncRelayCommand(p => OnOneShotAsync(p as TweakItem));
        ApplyRecommendedPackCommand = new AsyncRelayCommand(() => ApplyTierPresetAsync(includePartial: false));
        ApplyRecommendedPlusPartialCommand = new AsyncRelayCommand(() => ApplyTierPresetAsync(includePartial: true));
        CreateRestorePointCommand = new AsyncRelayCommand(CreateRestorePointAsync);
        RestartExplorerCommand = new AsyncRelayCommand(RestartExplorerAsync);
        ScanBloatwareCommand = new AsyncRelayCommand(LoadBloatwareAsync);
        RemoveSelectedBloatwareCommand = new AsyncRelayCommand(RemoveSelectedBloatwareAsync);

        LoadTweaks();
    }

    public void LoadTweaks()
    {
        _suppressToggleSync = true;
        _allTweaks.Clear();
        foreach (var t in _tweaksService.GetTweaksWithCurrentState())
            _allTweaks.Add(t);
        _suppressToggleSync = false;

        OnPropertyChanged(nameof(RecommendedCount));
        OnPropertyChanged(nameof(RecommendedActiveCount));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredTweaks.Clear();
        var q = SearchText.Trim();

        foreach (var t in _allTweaks)
        {
            bool filterOk = SelectedFilter switch
            {
                "Sadece Önerilenler" => t.Tier == RecommendationTier.Onerilen,
                "ShutUp10++" => t.SourceTag == "ShutUp10++",
                "Gizlilik" => t.Category is "Gizlilik" or "Uygulama Gizliliği" or "Güncelleme & Sistem",
                "Edge & AI" => t.Category == "Edge & AI",
                "Arayüz & Performans" => t.Category is "Arayüz & Gezgin" or "Performans" or "Bloatware Temizliği",
                _ => true
            };

            bool searchOk = string.IsNullOrEmpty(q) ||
                            t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            t.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            t.Category.Contains(q, StringComparison.OrdinalIgnoreCase);

            if (filterOk && searchOk)
                FilteredTweaks.Add(t);
        }
    }

    private async Task OnToggleChangedAsync(TweakItem? item)
    {
        if (item is null || _suppressToggleSync) return;

        item.IsBusy = true;
        StatusMessage = $"{item.Title} uygulanıyor...";
        try
        {
            var msg = await _tweaksService.ApplyTweakAsync(item.Key, item.IsEnabled);
            OnPropertyChanged(nameof(RecommendedActiveCount));
            StatusMessage = msg;
        }
        catch (Exception ex)
        {
            _suppressToggleSync = true;
            item.IsEnabled = !item.IsEnabled;
            _suppressToggleSync = false;
            StatusMessage = $"Ayar değiştirilemedi: {ex.Message}";
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    private async Task OnOneShotAsync(TweakItem? item)
    {
        if (item is null) return;
        IsBusy = true;
        item.IsBusy = true;
        StatusMessage = $"{item.Title} çalıştırılıyor...";
        try
        {
            var msg = await _tweaksService.ApplyTweakAsync(item.Key, true);
            _suppressToggleSync = true;
            item.IsEnabled = _tweaksService.ReadTweakState(item.Key);
            _suppressToggleSync = false;
            OnPropertyChanged(nameof(RecommendedActiveCount));
            StatusMessage = msg;
        }
        finally
        {
            item.IsBusy = false;
            IsBusy = false;
        }
    }

    private async Task ApplyTierPresetAsync(bool includePartial)
    {
        IsBusy = true;
        StatusMessage = includePartial
            ? "Önerilen ve Kısmen Önerilen tüm ayarlar uygulanıyor..."
            : "O&O ShutUp10++ ve LRX Core 'Önerilen' güvenli ayarlar paketi uygulanıyor...";
        try
        {
            int applied = 0;
            foreach (var item in _allTweaks)
            {
                if (item.IsOneShotAction) continue; // Keep OneDrive removal explicit
                if (item.Tier == RecommendationTier.Onerilen ||
                    (includePartial && item.Tier == RecommendationTier.KismenOnerilen))
                {
                    await _tweaksService.ApplyTweakAsync(item.Key, true);
                    applied++;
                }
            }

            LoadTweaks();
            StatusMessage = $"{applied} önerilen ayar güvenle uygulandı. (Gezgin değişiklikleri için Explorer'ı yenileyebilirsiniz)";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CreateRestorePointAsync()
    {
        IsBusy = true;
        StatusMessage = "Sistem Geri Yükleme Noktası oluşturuluyor...";
        try
        {
            var (_, msg) = await _tweaksService.CreateRestorePointAsync();
            StatusMessage = msg;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RestartExplorerAsync()
    {
        IsBusy = true;
        StatusMessage = "Windows Gezgini (Explorer) yeniden başlatılıyor...";
        try
        {
            await _tweaksService.RestartExplorerAsync();
            StatusMessage = "Explorer yenilendi. Sağ tık ve dosya uzantısı ayarları aktif.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadBloatwareAsync()
    {
        IsBusy = true;
        StatusMessage = "Yüklü ön tanımlı Windows bloatware uygulamaları taranıyor...";
        try
        {
            var list = await _tweaksService.GetInstalledBloatwareAsync();
            BloatwareApps.Clear();
            foreach (var b in list) BloatwareApps.Add(b);
            StatusMessage = list.Count > 0
                ? $"{list.Count} kaldırılabilir ön yüklü Microsoft uygulaması tespit edildi."
                : "Kaldırılacak bilinen bir Microsoft bloatware uygulaması bulunamadı.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RemoveSelectedBloatwareAsync()
    {
        var selected = BloatwareApps.Where(b => b.IsSelected && b.IsInstalled).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Önce kaldırılacak bloatware uygulamalarını seçin.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"{selected.Count} ön yüklü uygulama kaldırılıyor...";
        try
        {
            int count = await _tweaksService.RemoveBloatwareAsync(selected);
            foreach (var item in selected.Where(x => !x.IsInstalled).ToList())
                BloatwareApps.Remove(item);

            StatusMessage = $"{count} ön yüklü uygulama sistemden kaldırıldı.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
