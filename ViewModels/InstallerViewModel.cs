using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using LRXCore.Helpers;
using LRXCore.Models;
using LRXCore.Services;
using Win32 = Microsoft.Win32;

namespace LRXCore.ViewModels;

public sealed class InstallerViewModel : ObservableObject
{
    private readonly AppCatalogService _catalogService;
    private readonly WingetService _wingetService;
    private readonly SettingsService _settingsService;

    private string _searchText = string.Empty;
    private string _selectedCategory = "Tümü";
    private string _customWingetId = string.Empty;
    private string _customAppName = string.Empty;
    private bool _isInstalling;
    private double _progressPercent;
    private string _currentInstallText = "Kurulum için uygulama seçin ve başlatın.";
    private string _consoleLog = string.Empty;
    private CancellationTokenSource? _installCts;

    public ObservableCollection<AppCatalogItem> AllApps { get; } = new();
    public ObservableCollection<AppCatalogItem> FilteredApps { get; } = new();
    public ObservableCollection<string> Categories { get; } = new()
    {
        "Tümü", "Tarayıcılar", "İletişim", "Yapay Zeka", "Medya", "Oyun", "Araçlar & Geliştirici", "Sürücü & Altyapı", "Özel"
    };

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set { if (SetProperty(ref _selectedCategory, value)) ApplyFilter(); }
    }

    public string CustomWingetId
    {
        get => _customWingetId;
        set => SetProperty(ref _customWingetId, value);
    }

    public string CustomAppName
    {
        get => _customAppName;
        set => SetProperty(ref _customAppName, value);
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        private set => SetProperty(ref _isInstalling, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string CurrentInstallText
    {
        get => _currentInstallText;
        private set => SetProperty(ref _currentInstallText, value);
    }

    public string ConsoleLog
    {
        get => _consoleLog;
        private set => SetProperty(ref _consoleLog, value);
    }

    public int SelectedCount => AllApps.Count(a => a.IsSelected);

    public ICommand SelectAllVisibleCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand SelectRecommendedCommand { get; }
    public ICommand AddCustomAppCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand LoadProfileCommand { get; }
    public ICommand StartInstallCommand { get; }
    public ICommand CancelInstallCommand { get; }

    public InstallerViewModel(AppCatalogService catalogService, WingetService wingetService, SettingsService settingsService)
    {
        _catalogService = catalogService;
        _wingetService = wingetService;
        _settingsService = settingsService;

        foreach (var item in _catalogService.GetDefaultCatalog())
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppCatalogItem.IsSelected))
                    OnPropertyChanged(nameof(SelectedCount));
            };
            AllApps.Add(item);
        }

        SelectAllVisibleCommand = new RelayCommand(() => { foreach (var a in FilteredApps) a.IsSelected = true; });
        ClearAllCommand = new RelayCommand(() => { foreach (var a in AllApps) a.IsSelected = false; });
        SelectRecommendedCommand = new RelayCommand(() =>
        {
            foreach (var a in AllApps)
                a.IsSelected = a.IsRecommended;
        });
        AddCustomAppCommand = new RelayCommand(AddCustomApp);
        SaveProfileCommand = new RelayCommand(SaveProfile);
        LoadProfileCommand = new RelayCommand(LoadProfile);
        StartInstallCommand = new AsyncRelayCommand(StartInstallAsync, () => !IsInstalling && SelectedCount > 0);
        CancelInstallCommand = new RelayCommand(() => _installCts?.Cancel(), () => IsInstalling);

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredApps.Clear();
        var query = SearchText.Trim();

        foreach (var app in AllApps)
        {
            bool catOk = SelectedCategory == "Tümü" || app.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase);
            bool searchOk = string.IsNullOrEmpty(query) ||
                            app.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            app.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            app.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
            if (catOk && searchOk)
                FilteredApps.Add(app);
        }
    }

    private void AddCustomApp()
    {
        var id = CustomWingetId.Trim();
        if (string.IsNullOrWhiteSpace(id)) return;

        var name = string.IsNullOrWhiteSpace(CustomAppName) ? id : CustomAppName.Trim();
        if (AllApps.Any(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            CurrentInstallText = $"'{id}' zaten listede mevcut.";
            return;
        }

        var item = new AppCatalogItem
        {
            Id = id,
            Name = name,
            Category = "Özel",
            Description = $"Kullanıcı tarafından eklenen Winget paketi ({id})",
            IsSelected = true,
            IconGlyph = "\uE756"
        };
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppCatalogItem.IsSelected))
                OnPropertyChanged(nameof(SelectedCount));
        };

        AllApps.Add(item);
        CustomWingetId = string.Empty;
        CustomAppName = string.Empty;
        OnPropertyChanged(nameof(SelectedCount));
        ApplyFilter();
    }

    private void SaveProfile()
    {
        var dlg = new Win32.SaveFileDialog
        {
            InitialDirectory = _settingsService.ProfilesDirectory,
            Filter = "LRX Kurulum Profili (*.json)|*.json",
            FileName = $"LRX_Profil_{DateTime.Now:yyyyMMdd}.json"
        };
        if (dlg.ShowDialog() == true)
        {
            _catalogService.SaveProfile(dlg.FileName, AllApps);
            CurrentInstallText = $"Profil kaydedildi: {Path.GetFileName(dlg.FileName)}";
        }
    }

    private void LoadProfile()
    {
        var dlg = new Win32.OpenFileDialog
        {
            InitialDirectory = _settingsService.ProfilesDirectory,
            Filter = "LRX Kurulum Profili (*.json)|*.json"
        };
        if (dlg.ShowDialog() == true)
        {
            var ids = _catalogService.LoadProfileIds(dlg.FileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var app in AllApps)
                app.IsSelected = ids.Contains(app.Id);
            CurrentInstallText = $"Profil yüklendi ({ids.Count} uygulama seçildi).";
        }
    }

    private async Task StartInstallAsync()
    {
        var queue = AllApps.Where(a => a.IsSelected).ToList();
        if (queue.Count == 0) return;

        IsInstalling = true;
        ProgressPercent = 0;
        _installCts = new CancellationTokenSource();
        var logBuilder = new StringBuilder();
        ConsoleLog = string.Empty;

        foreach (var app in queue)
        {
            app.Status = AppInstallStatus.Bekliyor;
            app.StatusMessage = string.Empty;
        }

        int done = 0;
        int succeeded = 0;
        int failed = 0;

        try
        {
            foreach (var app in queue)
            {
                _installCts.Token.ThrowIfCancellationRequested();

                app.Status = AppInstallStatus.Kuruluyor;
                CurrentInstallText = $"({done + 1}/{queue.Count}) {app.Name} indiriliyor ve kuruluyor...";
                AppendLog(logBuilder, $"==> [{app.Name}] ({app.Id}) başlatıldı...");

                var (ok, already, msg) = await _wingetService.InstallPackageAsync(
                    app.Id,
                    app.Source,
                    line => AppendLog(logBuilder, $"    {line}"),
                    _installCts.Token);

                if (ok)
                {
                    app.Status = already ? AppInstallStatus.ZatenKurulu : AppInstallStatus.Tamamlandi;
                    app.StatusMessage = msg;
                    succeeded++;
                }
                else
                {
                    app.Status = AppInstallStatus.Hata;
                    app.StatusMessage = msg;
                    failed++;
                }

                done++;
                ProgressPercent = done * 100.0 / queue.Count;
            }

            CurrentInstallText = $"Kurulum tamamlandı: {succeeded} başarılı, {failed} hatalı.";
        }
        catch (OperationCanceledException)
        {
            CurrentInstallText = "Kurulum kullanıcı tarafından durduruldu.";
            AppendLog(logBuilder, "!! İşlem iptal edildi.");
        }
        finally
        {
            IsInstalling = false;
            _installCts?.Dispose();
            _installCts = null;
            RelayCommand.RaiseCanExecuteChanged();
        }
    }

    private void AppendLog(StringBuilder sb, string line)
    {
        sb.AppendLine(line);
        if (sb.Length > 16000)
            sb.Remove(0, sb.Length - 12000);
        ConsoleLog = sb.ToString();
    }
}
