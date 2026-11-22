using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using LRXCore.Helpers;
using LRXCore.Models;
using LRXCore.Services;

namespace LRXCore.ViewModels;

public sealed class UpdatesViewModel : ObservableObject
{
    private readonly WingetService _wingetService;
    private readonly TrayNotificationService _trayService;
    private readonly SelfUpdateService _selfUpdateService;

    private bool _isBusy;
    private string _statusMessage = "Sistemdeki kurulu uygulamalar için güncelleme taraması başlatın.";
    private string _updateLog = string.Empty;

    private SelfUpdateInfo? _latestSelfUpdate;
    private string _selfUpdateStatus = $"Mevcut sürüm: v{SelfUpdateService.GetCurrentVersionDisplay()} — GitHub üzerinden kontrol edilmedi.";
    private bool _isSelfUpdateAvailable;
    private bool _isSelfUpdating;
    private double _selfUpdateProgress;

    public ObservableCollection<AppUpdateItem> Updates { get; } = new();

    public string CurrentAppVersion => $"v{SelfUpdateService.GetCurrentVersionDisplay()}";

    public string SelfUpdateStatus
    {
        get => _selfUpdateStatus;
        private set => SetProperty(ref _selfUpdateStatus, value);
    }

    public bool IsSelfUpdateAvailable
    {
        get => _isSelfUpdateAvailable;
        private set => SetProperty(ref _isSelfUpdateAvailable, value);
    }

    public bool IsSelfUpdating
    {
        get => _isSelfUpdating;
        private set => SetProperty(ref _isSelfUpdating, value);
    }

    public double SelfUpdateProgress
    {
        get => _selfUpdateProgress;
        private set => SetProperty(ref _selfUpdateProgress, value);
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

    public string UpdateLog
    {
        get => _updateLog;
        private set => SetProperty(ref _updateLog, value);
    }

    public int PendingCount => Updates.Count(u => !u.IsUpdated);

    public ICommand ScanUpdatesCommand { get; }
    public ICommand UpdateSingleCommand { get; }
    public ICommand UpdateAllCommand { get; }
    public ICommand CheckSelfUpdateCommand { get; }
    public ICommand InstallSelfUpdateCommand { get; }

    public UpdatesViewModel(WingetService wingetService, TrayNotificationService trayService, SelfUpdateService selfUpdateService)
    {
        _wingetService = wingetService;
        _trayService = trayService;
        _selfUpdateService = selfUpdateService;

        _trayService.UpdatesDetected += OnBackgroundUpdatesDetected;
        _trayService.SelfUpdateDetected += OnBackgroundSelfUpdateDetected;

        ScanUpdatesCommand = new AsyncRelayCommand(ScanUpdatesAsync);
        UpdateSingleCommand = new AsyncRelayCommand(p => UpdateSingleAsync(p as AppUpdateItem));
        UpdateAllCommand = new AsyncRelayCommand(UpdateAllAsync);
        CheckSelfUpdateCommand = new AsyncRelayCommand(CheckSelfUpdateAsync);
        InstallSelfUpdateCommand = new AsyncRelayCommand(InstallSelfUpdateAsync);
    }

    private void OnBackgroundSelfUpdateDetected(SelfUpdateInfo info)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() => ApplySelfUpdateInfo(info));
    }

    public async Task CheckSelfUpdateAsync()
    {
        IsSelfUpdating = true;
        SelfUpdateStatus = "GitHub (Mericsdc/LRXCore) üzerinden yeni sürüm kontrol ediliyor...";
        try
        {
            var info = await _selfUpdateService.CheckForUpdateAsync();
            ApplySelfUpdateInfo(info);
        }
        finally
        {
            IsSelfUpdating = false;
        }
    }

    private void ApplySelfUpdateInfo(SelfUpdateInfo info)
    {
        _latestSelfUpdate = info;
        IsSelfUpdateAvailable = info.IsUpdateAvailable;
        SelfUpdateStatus = info.StatusMessage;
    }

    private async Task InstallSelfUpdateAsync()
    {
        if (_latestSelfUpdate is null || !_latestSelfUpdate.IsUpdateAvailable || IsSelfUpdating)
            return;

        IsSelfUpdating = true;
        SelfUpdateProgress = 0;
        var prog = new Progress<double>(v => SelfUpdateProgress = v);

        try
        {
            var (launched, msg) = await _selfUpdateService.DownloadAndLaunchInstallerAsync(
                _latestSelfUpdate,
                prog,
                status => SelfUpdateStatus = status);

            SelfUpdateStatus = msg;
            if (launched)
            {
                await Task.Delay(350);
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    System.Windows.Application.Current.Shutdown(0));
            }
        }
        finally
        {
            IsSelfUpdating = false;
        }
    }

    private void OnBackgroundUpdatesDetected(List<AppUpdateItem> items)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ApplyUpdatesList(items);
        });
    }

    public async Task ScanUpdatesAsync()
    {
        IsBusy = true;
        StatusMessage = "Winget üzerinden tüm kurulu paketlerin yeni sürümleri kontrol ediliyor...";
        try
        {
            var items = await _wingetService.CheckUpdatesAsync();
            ApplyUpdatesList(items);
            StatusMessage = items.Count > 0
                ? $"{items.Count} uygulama için yeni güncelleme bulundu."
                : "Harika! Tüm desteklenen uygulamalarınız güncel.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Güncelleme kontrolü başarısız: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyUpdatesList(List<AppUpdateItem> items)
    {
        Updates.Clear();
        foreach (var item in items)
            Updates.Add(item);
        OnPropertyChanged(nameof(PendingCount));
    }

    private async Task UpdateSingleAsync(AppUpdateItem? item)
    {
        if (item is null || item.IsUpdating || item.IsUpdated) return;

        IsBusy = true;
        item.IsUpdating = true;
        item.StatusText = "Güncelleniyor...";
        var sb = new StringBuilder(UpdateLog);
        sb.AppendLine($"==> [{item.Name}] {item.CurrentVersion} -> {item.AvailableVersion} güncelleniyor...");
        UpdateLog = sb.ToString();

        try
        {
            var (ok, msg) = await _wingetService.UpgradePackageAsync(
                item.WingetId,
                item.Source,
                line =>
                {
                    sb.AppendLine($"    {line}");
                    if (sb.Length > 16000)
                        sb.Remove(0, sb.Length - 12000);
                    UpdateLog = sb.ToString();
                });

            item.IsUpdated = ok;
            item.StatusText = ok ? "Güncellendi" : msg;
            StatusMessage = ok
                ? $"{item.Name} ({item.AvailableVersion}) başarıyla güncellendi."
                : $"{item.Name} güncellenemedi: {msg}";
            OnPropertyChanged(nameof(PendingCount));
        }
        finally
        {
            item.IsUpdating = false;
            IsBusy = false;
        }
    }

    private async Task UpdateAllAsync()
    {
        var targets = Updates.Where(u => u.IsSelected && !u.IsUpdated).ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "Güncellenecek seçili uygulama bulunamadı.";
            return;
        }

        IsBusy = true;
        var sb = new StringBuilder();
        int okCount = 0;

        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var item = targets[i];
                item.IsUpdating = true;
                item.StatusText = "Güncelleniyor...";
                StatusMessage = $"({i + 1}/{targets.Count}) {item.Name} güncelleniyor...";
                sb.AppendLine($"==> [{item.Name}] ({item.WingetId})");
                UpdateLog = sb.ToString();

                var (ok, msg) = await _wingetService.UpgradePackageAsync(
                    item.WingetId,
                    item.Source,
                    line =>
                    {
                        sb.AppendLine($"    {line}");
                        if (sb.Length > 16000)
                            sb.Remove(0, sb.Length - 12000);
                        UpdateLog = sb.ToString();
                    });

                item.IsUpdating = false;
                item.IsUpdated = ok;
                item.StatusText = ok ? "Güncellendi" : msg;
                if (ok) okCount++;
                OnPropertyChanged(nameof(PendingCount));
            }

            StatusMessage = $"Toplu güncelleme tamamlandı: {okCount}/{targets.Count} uygulama güncellendi.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
