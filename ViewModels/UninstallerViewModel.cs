using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using LRXCore.Helpers;
using LRXCore.Models;
using LRXCore.Services;

namespace LRXCore.ViewModels;

public sealed class UninstallerViewModel : ObservableObject
{
    private readonly UninstallerService _uninstallerService;
    private readonly List<InstalledAppInfo> _allApps = new();

    private string _searchText = string.Empty;
    private bool _includeSystemComponents;
    private InstalledAppInfo? _selectedApp;
    private bool _isBusy;
    private bool _isLeftoverPanelOpen;
    private string _statusMessage = "Yüklü uygulamalar listeleniyor...";
    private string _leftoverHeader = string.Empty;

    public ObservableCollection<InstalledAppInfo> FilteredApps { get; } = new();
    public ObservableCollection<LeftoverItem> Leftovers { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
    }

    public bool IncludeSystemComponents
    {
        get => _includeSystemComponents;
        set
        {
            if (SetProperty(ref _includeSystemComponents, value))
                _ = LoadAppsAsync();
        }
    }

    public InstalledAppInfo? SelectedApp
    {
        get => _selectedApp;
        set
        {
            if (SetProperty(ref _selectedApp, value))
                RelayCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsLeftoverPanelOpen
    {
        get => _isLeftoverPanelOpen;
        set => SetProperty(ref _isLeftoverPanelOpen, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string LeftoverHeader
    {
        get => _leftoverHeader;
        private set => SetProperty(ref _leftoverHeader, value);
    }

    public int TotalAppsCount => _allApps.Count;

    public ICommand RefreshCommand { get; }
    public ICommand UninstallAndScanCommand { get; }
    public ICommand ScanLeftoversOnlyCommand { get; }
    public ICommand CleanLeftoversCommand { get; }
    public ICommand CloseLeftoverPanelCommand { get; }
    public ICommand SelectAllLeftoversCommand { get; }
    public ICommand SelectSafeLeftoversCommand { get; }

    public UninstallerViewModel(UninstallerService uninstallerService)
    {
        _uninstallerService = uninstallerService;

        RefreshCommand = new AsyncRelayCommand(LoadAppsAsync);
        UninstallAndScanCommand = new AsyncRelayCommand(UninstallAndScanAsync, () => SelectedApp is not null && !IsBusy);
        ScanLeftoversOnlyCommand = new AsyncRelayCommand(ScanLeftoversOnlyAsync, () => SelectedApp is not null && !IsBusy);
        CleanLeftoversCommand = new AsyncRelayCommand(CleanLeftoversAsync, () => Leftovers.Any(l => l.IsSelected) && !IsBusy);
        CloseLeftoverPanelCommand = new RelayCommand(() => IsLeftoverPanelOpen = false);
        SelectAllLeftoversCommand = new RelayCommand(() => { foreach (var l in Leftovers) l.IsSelected = true; });
        SelectSafeLeftoversCommand = new RelayCommand(() => { foreach (var l in Leftovers) l.IsSelected = l.Risk == LeftoverRisk.Guvenli; });
    }

    public async Task LoadAppsAsync()
    {
        IsBusy = true;
        StatusMessage = "Yüklü programlar ve Microsoft Store uygulamaları taranıyor...";
        try
        {
            var apps = await _uninstallerService.GetInstalledAppsAsync(IncludeSystemComponents);
            _allApps.Clear();
            _allApps.AddRange(apps);
            OnPropertyChanged(nameof(TotalAppsCount));
            ApplyFilter();
            StatusMessage = $"Toplam {apps.Count} yüklü uygulama bulundu.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredApps.Clear();
        var q = SearchText.Trim();
        foreach (var app in _allApps)
        {
            if (string.IsNullOrEmpty(q) ||
                app.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                app.Publisher.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                FilteredApps.Add(app);
            }
        }
    }

    private async Task UninstallAndScanAsync()
    {
        if (SelectedApp is null) return;
        var target = SelectedApp;

        var confirm = MessageBox.Show(
            $"'{target.Name}' uygulamasının kendi kaldırıcısı çalıştırılacak, ardından tüm bilgisayarda çöp dosya ve kayıt defteri kalıntısı taranacaktır.\n\nDevam edilsin mi?",
            "LRX Core — Derin Kaldırma",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        StatusMessage = $"'{target.Name}' resmi kaldırıcısı çalıştırılıyor...";
        try
        {
            var (ran, msg) = await _uninstallerService.RunNativeUninstallerAsync(target);
            StatusMessage = msg;

            // Give Windows a brief moment to release locks after uninstaller exits
            await Task.Delay(700);
            await RunLeftoverScanAsync(target, ran ? "Resmi kaldırıcı tamamlandı." : msg);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ScanLeftoversOnlyAsync()
    {
        if (SelectedApp is null) return;
        IsBusy = true;
        try
        {
            await RunLeftoverScanAsync(SelectedApp, "Doğrudan artık taraması tamamlandı.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunLeftoverScanAsync(InstalledAppInfo target, string prefixMsg)
    {
        StatusMessage = $"'{target.Name}' için dosya, kısayol ve kayıt defteri artıkları taranıyor...";
        var found = await _uninstallerService.ScanLeftoversAsync(target);

        Leftovers.Clear();
        foreach (var l in found)
        {
            l.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LeftoverItem.IsSelected))
                    RelayCommand.RaiseCanExecuteChanged();
            };
            Leftovers.Add(l);
        }

        if (found.Count > 0)
        {
            long totalBytes = found.Sum(x => x.SizeBytes);
            LeftoverHeader = $"'{target.Name}' Kalıntı Raporu — {found.Count} artık ({FormatHelper.FormatBytes(totalBytes)})";
            IsLeftoverPanelOpen = true;
            StatusMessage = $"{prefixMsg} {found.Count} adet potansiyel çöp/kalıntı bulundu ve listelendi.";
        }
        else
        {
            IsLeftoverPanelOpen = false;
            StatusMessage = $"{prefixMsg} Arka planda hiç artık kalıntı tespit edilmedi.";
            await LoadAppsAsync();
        }
    }

    private async Task CleanLeftoversAsync()
    {
        var selected = Leftovers.Where(l => l.IsSelected).ToList();
        if (selected.Count == 0) return;

        IsBusy = true;
        StatusMessage = $"Seçilen {selected.Count} artık yedeklenip temizleniyor...";
        try
        {
            var (cleaned, failed, freed, backupFile) = await _uninstallerService.CleanLeftoversAsync(selected);
            foreach (var item in selected)
                Leftovers.Remove(item);

            if (Leftovers.Count == 0)
                IsLeftoverPanelOpen = false;

            var backupNote = !string.IsNullOrEmpty(backupFile) ? " (Kayıt defteri artıkları yedeklendi)" : "";
            StatusMessage = $"Temizlik tamamlandı: {cleaned} öğe silindi ({FormatHelper.FormatBytes(freed)} açıldı), {failed} atlandı.{backupNote}";
            await LoadAppsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
