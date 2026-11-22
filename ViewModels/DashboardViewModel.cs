using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using LRXCore.Helpers;
using LRXCore.Models;
using LRXCore.Services;

namespace LRXCore.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly HardwareService _hardwareService;
    private readonly WingetService _wingetService;
    private readonly Action<string> _navigateTo;

    private HardwareInfo _hardware = new();
    private bool _isLoading = true;
    private string _statusMessage = "Donanım bilgileri taranıyor...";

    public HardwareInfo Hardware
    {
        get => _hardware;
        private set => SetProperty(ref _hardware, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<DriverRecommendation> Recommendations { get; } = new();
    public ObservableCollection<DiskInfoItem> Disks { get; } = new();
    public ObservableCollection<GpuInfo> Gpus { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ExecuteRecommendationCommand { get; }
    public ICommand NavigateCommand { get; }

    public DashboardViewModel(HardwareService hardwareService, WingetService wingetService, Action<string> navigateTo)
    {
        _hardwareService = hardwareService;
        _wingetService = wingetService;
        _navigateTo = navigateTo;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        ExecuteRecommendationCommand = new AsyncRelayCommand(p => ExecuteRecommendationAsync(p as DriverRecommendation));
        NavigateCommand = new RelayCommand(p => { if (p is string s) _navigateTo(s); });
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = "Donanım bileşenleri WMI üzerinden okunuyor...";
        try
        {
            var info = await _hardwareService.GetHardwareInfoAsync();
            Hardware = info;

            Gpus.Clear();
            foreach (var g in info.Gpus) Gpus.Add(g);

            Disks.Clear();
            foreach (var d in info.Disks) Disks.Add(d);

            Recommendations.Clear();
            foreach (var r in info.Recommendations) Recommendations.Add(r);

            StatusMessage = $"Donanım analizi tamamlandı — {info.ComputerName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Donanım okuma hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecuteRecommendationAsync(DriverRecommendation? rec)
    {
        if (rec is null) return;

        if (!string.IsNullOrEmpty(rec.DownloadUrl))
        {
            Process.Start(new ProcessStartInfo(rec.DownloadUrl) { UseShellExecute = true });
            StatusMessage = $"Resmi indirme sayfası tarayıcıda açıldı: {rec.Title}";
            return;
        }

        if (!string.IsNullOrEmpty(rec.WingetId))
        {
            IsLoading = true;
            StatusMessage = $"{rec.Title} ({rec.WingetId}) winget ile kuruluyor...";
            try
            {
                var (ok, already, msg) = await _wingetService.InstallPackageAsync(
                    rec.WingetId,
                    rec.Source,
                    line => StatusMessage = $"{rec.Title}: {line}");

                StatusMessage = ok
                    ? $"{rec.Title}: {(already ? "Zaten kurulu." : "Başarıyla kuruldu.")}"
                    : $"{rec.Title}: Kurulum başarısız ({msg}).";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
