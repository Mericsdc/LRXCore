using System.Diagnostics;
using System.Windows.Input;
using LRXCore.Helpers;
using LRXCore.Services;

namespace LRXCore.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly TrayNotificationService _trayService;
    private readonly SelfUpdateService _selfUpdateService;
    private string _statusMessage = string.Empty;

    public List<int> IntervalOptions { get; } = new() { 1, 3, 6, 12, 24 };
    public string AppVersionText => $"LRX Core v{SelfUpdateService.GetCurrentVersionDisplay()}";
    public string GitHubRepoUrl => SelfUpdateService.RepoUrl;

    public bool MinimizeToTrayOnClose
    {
        get => _settingsService.Current.MinimizeToTrayOnClose;
        set
        {
            if (_settingsService.Current.MinimizeToTrayOnClose != value)
            {
                _settingsService.Current.MinimizeToTrayOnClose = value;
                SaveWithNotice();
                OnPropertyChanged();
            }
        }
    }

    public bool AutoCheckUpdates
    {
        get => _settingsService.Current.AutoCheckUpdates;
        set
        {
            if (_settingsService.Current.AutoCheckUpdates != value)
            {
                _settingsService.Current.AutoCheckUpdates = value;
                SaveWithNotice();
                OnPropertyChanged();
            }
        }
    }

    public int UpdateCheckIntervalHours
    {
        get => _settingsService.Current.UpdateCheckIntervalHours;
        set
        {
            if (_settingsService.Current.UpdateCheckIntervalHours != value)
            {
                _settingsService.Current.UpdateCheckIntervalHours = value;
                SaveWithNotice();
                OnPropertyChanged();
            }
        }
    }

    public bool StartWithWindows
    {
        get => _settingsService.Current.StartWithWindows;
        set
        {
            if (_settingsService.Current.StartWithWindows != value)
            {
                _settingsService.Current.StartWithWindows = value;
                SaveWithNotice();
                OnPropertyChanged();
            }
        }
    }

    public string AppDataPath => _settingsService.AppDataDirectory;
    public string BackupsPath => _settingsService.BackupsDirectory;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand OpenBackupsFolderCommand { get; }
    public ICommand TestUpdateNotificationCommand { get; }
    public ICommand OpenGitHubCommand { get; }
    public ICommand CheckSelfUpdateCommand { get; }

    public SettingsViewModel(SettingsService settingsService, TrayNotificationService trayService, SelfUpdateService selfUpdateService)
    {
        _settingsService = settingsService;
        _trayService = trayService;
        _selfUpdateService = selfUpdateService;

        OpenBackupsFolderCommand = new RelayCommand(() =>
            Process.Start(new ProcessStartInfo("explorer.exe", _settingsService.BackupsDirectory) { UseShellExecute = true }));

        OpenGitHubCommand = new RelayCommand(() =>
            Process.Start(new ProcessStartInfo(SelfUpdateService.RepoUrl) { UseShellExecute = true }));

        CheckSelfUpdateCommand = new AsyncRelayCommand(async () =>
        {
            StatusMessage = "GitHub üzerinden LRX Core sürümü kontrol ediliyor...";
            var info = await _selfUpdateService.CheckForUpdateAsync();
            StatusMessage = info.StatusMessage;
        });

        TestUpdateNotificationCommand = new AsyncRelayCommand(async () =>
        {
            StatusMessage = "Arka plan güncelleme taraması tetikleniyor...";
            await _trayService.CheckUpdatesAndNotifyAsync(showIfEmpty: true);
            StatusMessage = "Güncelleme kontrolü ve bildirim testi tamamlandı.";
        });
    }

    private void SaveWithNotice()
    {
        _settingsService.Save();
        _trayService.ApplySettings(_settingsService.Current);
        StatusMessage = "Ayarlar kaydedildi.";
    }
}
