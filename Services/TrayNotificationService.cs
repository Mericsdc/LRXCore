using System.Runtime.InteropServices;
using LRXCore.Models;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace LRXCore.Services;

public sealed class TrayNotificationService : IDisposable
{
    private readonly WingetService _wingetService;
    private readonly SettingsService _settingsService;
    private readonly SelfUpdateService _selfUpdateService;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private System.Threading.Timer? _timer;
    private Drawing.Icon? _trayIcon;
    private IntPtr _iconHandle;
    private int _isChecking;

    public event Action? RestoreMainWindowRequested;
    public event Action? OpenUpdatesViewRequested;
    public event Action? ExitApplicationRequested;
    public event Action<List<AppUpdateItem>>? UpdatesDetected;
    public event Action<SelfUpdateInfo>? SelfUpdateDetected;

    public TrayNotificationService(WingetService wingetService, SettingsService settingsService, SelfUpdateService? selfUpdateService = null)
    {
        _wingetService = wingetService;
        _settingsService = settingsService;
        _selfUpdateService = selfUpdateService ?? new SelfUpdateService();

        _trayIcon = LoadTrayIcon(out _iconHandle);
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "LRX Core — Sistem & Güncelleme Merkezi",
            Visible = true
        };

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("LRX Core'u Aç", null, (_, _) => RestoreMainWindowRequested?.Invoke());
        menu.Items.Add("Güncellemeleri Şimdi Tara", null, async (_, _) => await CheckUpdatesAndNotifyAsync(showIfEmpty: true));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => ExitApplicationRequested?.Invoke());

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => RestoreMainWindowRequested?.Invoke();
        _notifyIcon.BalloonTipClicked += (_, _) =>
        {
            RestoreMainWindowRequested?.Invoke();
            OpenUpdatesViewRequested?.Invoke();
        };

        ApplySettings(_settingsService.Current);
    }

    public void ApplySettings(AppSettings settings)
    {
        _timer?.Dispose();
        _timer = null;

        if (settings.AutoCheckUpdates)
        {
            var interval = TimeSpan.FromHours(Math.Clamp(settings.UpdateCheckIntervalHours, 1, 24));
            _timer = new System.Threading.Timer(
                _ => _ = CheckUpdatesAndNotifyAsync(showIfEmpty: false),
                null,
                interval,
                interval);
        }
    }

    public async Task CheckUpdatesAndNotifyAsync(bool showIfEmpty = false)
    {
        if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0)
            return;

        try
        {
            var updates = await _wingetService.CheckUpdatesAsync();
            UpdatesDetected?.Invoke(updates);

            var selfUpdate = await _selfUpdateService.CheckForUpdateAsync();
            if (selfUpdate.IsUpdateAvailable)
            {
                SelfUpdateDetected?.Invoke(selfUpdate);
                _notifyIcon.ShowBalloonTip(
                    7000,
                    $"LRX Core v{selfUpdate.LatestVersion} Yayında!",
                    "LRX Core için yeni sürüm bulundu. Güncellemek için tıklayın.",
                    WinForms.ToolTipIcon.Info);
            }
            else if (updates.Count > 0)
            {
                var sampleNames = string.Join(", ", updates.Take(3).Select(u => u.Name));
                var extra = updates.Count > 3 ? $" ve +{updates.Count - 3} diğer" : "";
                _notifyIcon.ShowBalloonTip(
                    7000,
                    "LRX Core — Güncelleme Mevcut",
                    $"{updates.Count} uygulama için güncelleme geldi ({sampleNames}{extra}). Güncellemek ister misiniz?",
                    WinForms.ToolTipIcon.Info);
            }
            else if (showIfEmpty)
            {
                _notifyIcon.ShowBalloonTip(
                    4000,
                    "LRX Core — Sistem Güncel",
                    "Tüm desteklenen uygulamalarınız ve LRX Core güncel görünüyor.",
                    WinForms.ToolTipIcon.Info);
            }
        }
        catch
        {
            // Ignore silent background check errors
        }
        finally
        {
            Interlocked.Exchange(ref _isChecking, 0);
        }
    }

    public void ShowTrayMinimizedTip()
    {
        _notifyIcon.ShowBalloonTip(
            3000,
            "LRX Core arka planda çalışıyor",
            "Güncelleme bildirimleri aktif. Çıkmak için tepsi ikonuna sağ tıklayın.",
            WinForms.ToolTipIcon.None);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static Drawing.Icon LoadTrayIcon(out IntPtr handle)
    {
        handle = IntPtr.Zero;
        try
        {
            var streamInfo = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/app_icon.ico", UriKind.Absolute));
            if (streamInfo?.Stream is not null)
            {
                using var s = streamInfo.Stream;
                return new Drawing.Icon(s, new Drawing.Size(32, 32));
            }
        }
        catch { }

        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                var associated = Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (associated is not null)
                    return associated;
            }
        }
        catch { }

        return CreateMatteBlackTrayIcon(out handle);
    }

    private static Drawing.Icon CreateMatteBlackTrayIcon(out IntPtr handle)
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.FromArgb(9, 9, 11));

            using var borderPen = new Drawing.Pen(Drawing.Color.FromArgb(228, 228, 231), 2f);
            g.DrawRectangle(borderPen, 2, 2, 27, 27);

            using var font = new Drawing.Font("Segoe UI", 9.5f, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
            using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(244, 244, 245));
            var sf = new Drawing.StringFormat
            {
                Alignment = Drawing.StringAlignment.Center,
                LineAlignment = Drawing.StringAlignment.Center
            };
            g.DrawString("LRX", font, brush, new Drawing.RectangleF(0, 0, 32, 32), sf);
        }

        handle = bmp.GetHicon();
        return Drawing.Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayIcon?.Dispose();
        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }
    }
}
