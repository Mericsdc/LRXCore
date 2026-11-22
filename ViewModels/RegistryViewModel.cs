using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LRXCore.Helpers;
using LRXCore.Models;
using LRXCore.Services;

namespace LRXCore.ViewModels;

public sealed class RegistryViewModel : ObservableObject
{
    private readonly RegistryCleanerService _registryService;

    private bool _scanUninstall = true;
    private bool _scanStartup = true;
    private bool _scanSharedDlls = true;
    private bool _scanAppPaths = true;
    private bool _scanMuiCache = true;
    private bool _scanFirewall = true;
    private bool _isBusy;
    private string _statusMessage = "Güvenli kayıt defteri kategorilerini seçip taramayı başlatın. Temizlik öncesi otomatik .reg yedeği alınır.";

    public ObservableCollection<RegistryIssue> Issues { get; } = new();
    public ObservableCollection<RegistryBackupItem> Backups { get; } = new();

    public bool ScanUninstall { get => _scanUninstall; set => SetProperty(ref _scanUninstall, value); }
    public bool ScanStartup { get => _scanStartup; set => SetProperty(ref _scanStartup, value); }
    public bool ScanSharedDlls { get => _scanSharedDlls; set => SetProperty(ref _scanSharedDlls, value); }
    public bool ScanAppPaths { get => _scanAppPaths; set => SetProperty(ref _scanAppPaths, value); }
    public bool ScanMuiCache { get => _scanMuiCache; set => SetProperty(ref _scanMuiCache, value); }
    public bool ScanFirewall { get => _scanFirewall; set => SetProperty(ref _scanFirewall, value); }

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

    public ICommand ScanCommand { get; }
    public ICommand CleanCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand RefreshBackupsCommand { get; }
    public ICommand RestoreBackupCommand { get; }

    public RegistryViewModel(RegistryCleanerService registryService)
    {
        _registryService = registryService;

        ScanCommand = new AsyncRelayCommand(ScanAsync);
        CleanCommand = new AsyncRelayCommand(CleanAsync);
        SelectAllCommand = new RelayCommand(() => { foreach (var i in Issues) i.IsSelected = true; });
        ClearAllCommand = new RelayCommand(() => { foreach (var i in Issues) i.IsSelected = false; });
        RefreshBackupsCommand = new RelayCommand(LoadBackups);
        RestoreBackupCommand = new AsyncRelayCommand(p => RestoreBackupAsync(p as RegistryBackupItem));

        LoadBackups();
    }

    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusMessage = "Kayıt defteri güvenli kategorilerde taranıyor...";
        try
        {
            var found = await _registryService.ScanSafeCategoriesAsync(
                ScanUninstall, ScanStartup, ScanSharedDlls, ScanAppPaths, ScanMuiCache, ScanFirewall);

            Issues.Clear();
            foreach (var item in found) Issues.Add(item);

            StatusMessage = found.Count > 0
                ? $"{found.Count} geçersiz/artık kayıt defteri girdisi tespit edildi."
                : "Kayıt defteriniz temiz; seçilen kategorilerde artık kayıt bulunamadı.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CleanAsync()
    {
        var selectedCount = Issues.Count(i => i.IsSelected);
        if (selectedCount == 0)
        {
            StatusMessage = "Temizlemek için en az bir kayıt seçin.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Otomatik .reg yedeği alınıyor ve seçilen kayıtlar temizleniyor...";
        try
        {
            var (cleaned, failed, backupFile) = await _registryService.BackupAndCleanAsync(Issues);
            foreach (var done in Issues.Where(i => i.IsSelected).ToList())
                Issues.Remove(done);

            LoadBackups();
            StatusMessage = $"{cleaned} kayıt temizlendi ({failed} atlandı). Yedek: {Path.GetFileName(backupFile)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void LoadBackups()
    {
        Backups.Clear();
        foreach (var b in _registryService.GetBackups())
            Backups.Add(b);
    }

    private async Task RestoreBackupAsync(RegistryBackupItem? backup)
    {
        if (backup is null) return;
        var res = MessageBox.Show(
            $"'{backup.FileName}' yedeği kayıt defterine geri yüklensin mi?",
            "LRX Core — Kayıt Defteri Geri Yükleme",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            var (_, msg) = await _registryService.RestoreBackupAsync(backup.FullPath);
            StatusMessage = msg;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
