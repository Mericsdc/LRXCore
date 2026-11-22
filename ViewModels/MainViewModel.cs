using System.Windows.Input;
using LRXCore.Helpers;
using LRXCore.Services;

namespace LRXCore.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly Lazy<AppCatalogService> _catalogService;
    private readonly Lazy<TweaksService> _tweaksService;
    private readonly Lazy<UninstallerService> _uninstallerService;
    private readonly Lazy<JunkCleanerService> _junkCleanerService;
    private readonly Lazy<RegistryCleanerService> _registryCleanerService;
    private readonly Lazy<ServicesOptimizerService> _servicesOptimizerService;

    private readonly Lazy<InstallerViewModel> _installerVM;
    private readonly Lazy<TweaksViewModel> _tweaksVM;
    private readonly Lazy<ServicesViewModel> _servicesVM;
    private readonly Lazy<UninstallerViewModel> _uninstallerVM;
    private readonly Lazy<CleanerViewModel> _cleanerVM;
    private readonly Lazy<RegistryViewModel> _registryVM;
    private readonly Lazy<SettingsViewModel> _settingsVM;

    private readonly CancellationTokenSource _lifecycleCts = new();
    private string _activeSection = "Dashboard";
    private object _currentViewModel;
    private bool _dashboardLoaded;

    public SettingsService SettingsService { get; }
    public HardwareService HardwareService { get; }
    public WingetService WingetService { get; }
    public SelfUpdateService SelfUpdateService { get; }
    public TrayNotificationService TrayNotificationService { get; }

    public DashboardViewModel DashboardVM { get; }
    public UpdatesViewModel UpdatesVM { get; }

    public InstallerViewModel InstallerVM => _installerVM.Value;
    public TweaksViewModel TweaksVM => _tweaksVM.Value;
    public ServicesViewModel ServicesVM => _servicesVM.Value;
    public UninstallerViewModel UninstallerVM => _uninstallerVM.Value;
    public CleanerViewModel CleanerVM => _cleanerVM.Value;
    public RegistryViewModel RegistryVM => _registryVM.Value;
    public SettingsViewModel SettingsVM => _settingsVM.Value;

    public string ActiveSection
    {
        get => _activeSection;
        set
        {
            if (SetProperty(ref _activeSection, value))
            {
                CurrentViewModel = value switch
                {
                    "Dashboard" => DashboardVM,
                    "Installer" => InstallerVM,
                    "Tweaks" => TweaksVM,
                    "Services" => ServicesVM,
                    "Uninstaller" => UninstallerVM,
                    "Cleaner" => CleanerVM,
                    "Registry" => RegistryVM,
                    "Updates" => UpdatesVM,
                    "Settings" => SettingsVM,
                    _ => DashboardVM
                };

                OnPropertyChanged(nameof(IsDashboardActive));
                OnPropertyChanged(nameof(IsInstallerActive));
                OnPropertyChanged(nameof(IsTweaksActive));
                OnPropertyChanged(nameof(IsServicesActive));
                OnPropertyChanged(nameof(IsUninstallerActive));
                OnPropertyChanged(nameof(IsCleanerActive));
                OnPropertyChanged(nameof(IsRegistryActive));
                OnPropertyChanged(nameof(IsUpdatesActive));
                OnPropertyChanged(nameof(IsSettingsActive));

                if (value == "Dashboard")
                    EnsureDashboardLoaded();
                else if (value == "Uninstaller" && UninstallerVM.TotalAppsCount == 0 && !UninstallerVM.IsBusy)
                    _ = UninstallerVM.LoadAppsAsync();
                else if (value == "Tweaks" && TweaksVM.BloatwareApps.Count == 0 && !TweaksVM.IsBusy)
                    _ = TweaksVM.LoadBloatwareAsync();
                else if (value == "Services" && ServicesVM.TotalCount == 0 && !ServicesVM.IsBusy)
                    _ = ServicesVM.LoadServicesAsync();
            }
        }
    }

    public object CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    public bool IsDashboardActive { get => ActiveSection == "Dashboard"; set { if (value) ActiveSection = "Dashboard"; } }
    public bool IsInstallerActive { get => ActiveSection == "Installer"; set { if (value) ActiveSection = "Installer"; } }
    public bool IsTweaksActive { get => ActiveSection == "Tweaks"; set { if (value) ActiveSection = "Tweaks"; } }
    public bool IsServicesActive { get => ActiveSection == "Services"; set { if (value) ActiveSection = "Services"; } }
    public bool IsUninstallerActive { get => ActiveSection == "Uninstaller"; set { if (value) ActiveSection = "Uninstaller"; } }
    public bool IsCleanerActive { get => ActiveSection == "Cleaner"; set { if (value) ActiveSection = "Cleaner"; } }
    public bool IsRegistryActive { get => ActiveSection == "Registry"; set { if (value) ActiveSection = "Registry"; } }
    public bool IsUpdatesActive { get => ActiveSection == "Updates"; set { if (value) ActiveSection = "Updates"; } }
    public bool IsSettingsActive { get => ActiveSection == "Settings"; set { if (value) ActiveSection = "Settings"; } }

    public ICommand NavigateCommand { get; }

    public MainViewModel(bool startMinimized = false)
    {
        SettingsService = new SettingsService();
        HardwareService = new HardwareService();
        WingetService = new WingetService();
        SelfUpdateService = new SelfUpdateService();
        TrayNotificationService = new TrayNotificationService(WingetService, SettingsService, SelfUpdateService);

        _catalogService = new Lazy<AppCatalogService>(() => new AppCatalogService());
        _tweaksService = new Lazy<TweaksService>(() => new TweaksService());
        _uninstallerService = new Lazy<UninstallerService>(() => new UninstallerService(SettingsService.BackupsDirectory));
        _junkCleanerService = new Lazy<JunkCleanerService>(() => new JunkCleanerService(SettingsService));
        _registryCleanerService = new Lazy<RegistryCleanerService>(() => new RegistryCleanerService(SettingsService.BackupsDirectory));
        _servicesOptimizerService = new Lazy<ServicesOptimizerService>(() => new ServicesOptimizerService(SettingsService.BackupsDirectory));

        DashboardVM = new DashboardViewModel(HardwareService, WingetService, section => ActiveSection = section);
        UpdatesVM = new UpdatesViewModel(WingetService, TrayNotificationService, SelfUpdateService);

        _installerVM = new Lazy<InstallerViewModel>(() => new InstallerViewModel(_catalogService.Value, WingetService, SettingsService));
        _tweaksVM = new Lazy<TweaksViewModel>(() => new TweaksViewModel(_tweaksService.Value));
        _servicesVM = new Lazy<ServicesViewModel>(() => new ServicesViewModel(_servicesOptimizerService.Value));
        _uninstallerVM = new Lazy<UninstallerViewModel>(() => new UninstallerViewModel(_uninstallerService.Value));
        _cleanerVM = new Lazy<CleanerViewModel>(() => new CleanerViewModel(_junkCleanerService.Value, SettingsService));
        _registryVM = new Lazy<RegistryViewModel>(() => new RegistryViewModel(_registryCleanerService.Value));
        _settingsVM = new Lazy<SettingsViewModel>(() => new SettingsViewModel(SettingsService, TrayNotificationService, SelfUpdateService));

        _currentViewModel = DashboardVM;
        NavigateCommand = new RelayCommand(p => { if (p is string s) ActiveSection = s; });

        _ = InitializeAsync(startMinimized);
    }

    public void EnsureDashboardLoaded()
    {
        if (_dashboardLoaded) return;
        _dashboardLoaded = true;
        _ = LoadDashboardAndTrimAsync();
    }

    private async Task LoadDashboardAndTrimAsync()
    {
        await DashboardVM.LoadAsync();
        NativeMethods.TrimWorkingSet(runGc: false);
    }

    private async Task InitializeAsync(bool startMinimized)
    {
        if (!startMinimized)
            EnsureDashboardLoaded();
        else
            NativeMethods.TrimWorkingSet(runGc: true);

        if (SettingsService.Current.AutoCheckUpdates)
        {
            try
            {
                // Defer initial winget check slightly so startup uses minimal CPU/RAM
                await Task.Delay(TimeSpan.FromSeconds(12), _lifecycleCts.Token);
                await TrayNotificationService.CheckUpdatesAndNotifyAsync(showIfEmpty: false);
                NativeMethods.TrimWorkingSet(runGc: false);
            }
            catch (OperationCanceledException) { }
        }
    }

    public void ReleaseIdleMemory()
    {
        NativeMethods.TrimWorkingSet(runGc: true);
    }

    public void Dispose()
    {
        _lifecycleCts.Cancel();
        _lifecycleCts.Dispose();
        TrayNotificationService.Dispose();
    }
}
