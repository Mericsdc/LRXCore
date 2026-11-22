using System.Windows;
using LRXCore.ViewModels;

namespace LRXCore;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\LRXCore_SingleInstance_Mutex_v1";
    private const string RestoreEventName = @"Local\LRXCore_RestoreWindow_Event_v1";

    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;
    private EventWaitHandle? _restoreEvent;
    private RegisteredWaitHandle? _restoreWaitHandle;
    private MainViewModel? _mainViewModel;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            // Signal the already-running instance (visible, minimized, or in tray) to restore and exit cleanly
            try
            {
                using var existingEvent = EventWaitHandle.OpenExisting(RestoreEventName);
                existingEvent.Set();
            }
            catch { }

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown(0);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _restoreEvent = new EventWaitHandle(false, EventResetMode.AutoReset, RestoreEventName);
        _restoreWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            _restoreEvent,
            (_, _) => Dispatcher.BeginInvoke(ShowOrRestoreMainWindow),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        bool startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        _mainViewModel = new MainViewModel(startMinimized);

        _mainViewModel.TrayNotificationService.RestoreMainWindowRequested += () =>
        {
            Dispatcher.BeginInvoke(ShowOrRestoreMainWindow);
        };

        _mainViewModel.TrayNotificationService.OpenUpdatesViewRequested += () =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _mainViewModel.ActiveSection = "Updates";
                ShowOrRestoreMainWindow();
            });
        };

        _mainViewModel.TrayNotificationService.ExitApplicationRequested += () =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _mainWindow?.ForceExit();
                Shutdown(0);
            });
        };

        if (!startMinimized)
        {
            ShowOrRestoreMainWindow();
        }
    }

    private void ShowOrRestoreMainWindow()
    {
        if (_mainViewModel is null) return;
        _mainWindow ??= new MainWindow(_mainViewModel);
        _mainWindow.RestoreFromTray();
        _mainViewModel.EnsureDashboardLoaded();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _restoreWaitHandle?.Unregister(null);
        _restoreWaitHandle = null;

        _restoreEvent?.Dispose();
        _restoreEvent = null;

        _mainViewModel?.Dispose();
        _mainViewModel = null;

        if (_ownsMutex && _singleInstanceMutex is not null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); } catch { }
            _ownsMutex = false;
        }
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;

        base.OnExit(e);
    }
}
