using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LRXCore.Helpers;
using LRXCore.Models;
using LRXCore.Services;

namespace LRXCore.ViewModels;

public sealed class ServicesViewModel : ObservableObject
{
    private readonly ServicesOptimizerService _servicesOptimizer;
    private readonly List<ServiceOptimizationItem> _allServices = new();

    private string _selectedFilter = "Tümü";
    private string _searchText = string.Empty;
    private bool _isBusy;
    private string _statusMessage = "Sistemdeki güvenli uyutulabilir ve kullanılmayan hizmetler analiz ediliyor...";

    public ObservableCollection<ServiceOptimizationItem> FilteredServices { get; } = new();

    public List<string> FilterOptions { get; } = new()
    {
        "Tümü",
        "Güvenli (Önerilen)",
        "İsteğe Bağlı",
        "Durmuş 3. Parti"
    };

    public string SelectedFilter
    {
        get => _selectedFilter;
        set { if (SetProperty(ref _selectedFilter, value)) ApplyFilter(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
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

    public int TotalCount => _allServices.Count;
    public int RunningOptimizableCount => _allServices.Count(s => !s.IsAlreadyOptimized);
    public int OptimizedCount => _allServices.Count(s => s.IsAlreadyOptimized);

    public ICommand RefreshCommand { get; }
    public ICommand SleepSelectedCommand { get; }
    public ICommand ApplyRecommendedCommand { get; }
    public ICommand SelectSafeOnlyCommand { get; }
    public ICommand RestoreSnapshotCommand { get; }

    public ServicesViewModel(ServicesOptimizerService servicesOptimizer)
    {
        _servicesOptimizer = servicesOptimizer;

        RefreshCommand = new AsyncRelayCommand(LoadServicesAsync);
        SleepSelectedCommand = new AsyncRelayCommand(() => ApplyAsync(sleepOnly: true));
        ApplyRecommendedCommand = new AsyncRelayCommand(() => ApplyAsync(sleepOnly: false));
        SelectSafeOnlyCommand = new RelayCommand(SelectSafeOnly);
        RestoreSnapshotCommand = new AsyncRelayCommand(RestoreLatestAsync);
    }

    public async Task LoadServicesAsync()
    {
        IsBusy = true;
        StatusMessage = "Windows hizmetleri ve durmuş 3. parti servisler taranıyor...";
        try
        {
            var items = await _servicesOptimizer.ScanServicesAsync();
            _allServices.Clear();
            _allServices.AddRange(items);

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(RunningOptimizableCount));
            OnPropertyChanged(nameof(OptimizedCount));
            ApplyFilter();

            StatusMessage = $"{items.Count} güvenli aday hizmet incelendi ({RunningOptimizableCount} tanesi uyutulabilir / optimize edilebilir). Çekirdek sistem servisleri koruma altındadır.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredServices.Clear();
        var q = SearchText.Trim();

        foreach (var s in _allServices)
        {
            bool tierOk = SelectedFilter switch
            {
                "Güvenli (Önerilen)" => s.Tier == ServiceSafetyTier.Guvenli,
                "İsteğe Bağlı" => s.Tier == ServiceSafetyTier.IstegeBagli,
                "Durmuş 3. Parti" => s.Tier == ServiceSafetyTier.DurmusUcuncuParti,
                _ => true
            };

            bool searchOk = string.IsNullOrEmpty(q) ||
                            s.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            s.ServiceName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            s.Explanation.Contains(q, StringComparison.OrdinalIgnoreCase);

            if (tierOk && searchOk)
                FilteredServices.Add(s);
        }
    }

    private void SelectSafeOnly()
    {
        foreach (var s in _allServices)
            s.IsSelected = s.Tier == ServiceSafetyTier.Guvenli && !s.IsAlreadyOptimized;
        StatusMessage = "Sadece sistem kararlılığını bozmayan 'Güvenli (Önerilen)' hizmetler seçildi.";
    }

    private async Task ApplyAsync(bool sleepOnly)
    {
        var selectedCount = _allServices.Count(s => s.IsSelected);
        if (selectedCount == 0)
        {
            StatusMessage = "Uyutulacak veya optimize edilecek en az bir hizmet seçin.";
            return;
        }

        IsBusy = true;
        StatusMessage = sleepOnly
            ? $"{selectedCount} hizmetin yedeği alınıyor ve 'Elle (Uyut)' moduna alınıyor..."
            : $"{selectedCount} hizmetin yedeği alınıyor ve önerilen ayar uygulanıyor...";

        try
        {
            var (changed, failed, snapshot) = await _servicesOptimizer.ApplyOptimizationAsync(_allServices, sleepOnly);
            OnPropertyChanged(nameof(RunningOptimizableCount));
            OnPropertyChanged(nameof(OptimizedCount));

            var snapName = string.IsNullOrEmpty(snapshot) ? "" : $" Yedek: {Path.GetFileName(snapshot)}";
            StatusMessage = $"{changed} hizmet güvenle optimize edildi ({failed} erişim kısıtlı/atlandı). Hiçbir hizmet silinmedi.{snapName}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RestoreLatestAsync()
    {
        var confirm = MessageBox.Show(
            "Son alınan hizmet yedeğindeki tüm servisler orijinal başlangıç durumuna geri döndürülsün mü?",
            "LRX Core — Hizmet Yedeğini Geri Yükle",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            var (_, _, msg) = await _servicesOptimizer.RestoreLatestSnapshotAsync();
            await LoadServicesAsync();
            StatusMessage = msg;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
