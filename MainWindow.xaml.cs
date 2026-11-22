using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using LRXCore.Helpers;
using LRXCore.ViewModels;

namespace LRXCore;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _forceExit;
    private bool _shownTrayHint;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        SourceInitialized += (_, _) => ApplyDarkWindowAttribute();
        StateChanged += (_, _) =>
        {
            MaxRestoreIcon.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
            if (WindowState == WindowState.Minimized)
                _viewModel.ReleaseIdleMemory();
        };
    }

    private void ApplyDarkWindowAttribute()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int useDark = 1;
        _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
    }

    public void RestoreFromTray()
    {
        if (!IsVisible)
            Show();

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        // Bring reliably to front even when opened from tray or a blocked second launch
        Topmost = true;
        Topmost = false;
        Activate();
        Focus();
    }

    public void ForceExit()
    {
        _forceExit = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_forceExit && _viewModel.SettingsService.Current.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
            if (!_shownTrayHint)
            {
                _shownTrayHint = true;
                _viewModel.TrayNotificationService.ShowTrayMinimizedTip();
            }
            _viewModel.ReleaseIdleMemory();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_forceExit || !_viewModel.SettingsService.Current.MinimizeToTrayOnClose)
            Application.Current?.Shutdown();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
