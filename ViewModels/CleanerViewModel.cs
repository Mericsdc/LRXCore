using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LRXCore.Helpers;
using LRXCore.Models;
using LRXCore.Services;
using WinForms = System.Windows.Forms;

namespace LRXCore.ViewModels;

public sealed class CleanerViewModel : ObservableObject
{
    private readonly JunkCleanerService _cleanerService;
    private readonly SettingsService _settingsService;

    private bool _isBusy;
    private bool _hasScanned;
    private string _statusMessage = "Önce 'Tara' butonuna basarak sistemdeki gereksiz dosyaları analiz edin.";
    private string _scanSummaryTitle = "Tarama Bekleniyor";
    private string _scanSummaryDetail = "Sol listeden taranacak alanları seçip analiz başlatın. Yüksek yer kaplayan güvenli kalıntılar otomatik önerilir.";
    private int _selectedTabIndex;

    public ObservableCollection<CleanerCategory> AllCategories { get; } = new();
    public ObservableCollection<CleanerCategory> WindowsCommonCategories { get; } = new();
    public ObservableCollection<CleanerCategory> BrowserCommonCategories { get; } = new();
    public ObservableCollection<CleanerCategory> AdvancedCategories { get; } = new();
    public ObservableCollection<CleanerCategory> RecommendedCategories { get; } = new();
    public ObservableCollection<string> CustomFolders { get; } = new();
    public ObservableCollection<LargeFileItem> LargeFiles { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool HasScanned
    {
        get => _hasScanned;
        private set => SetProperty(ref _hasScanned, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ScanSummaryTitle
    {
        get => _scanSummaryTitle;
        private set => SetProperty(ref _scanSummaryTitle, value);
    }

    public string ScanSummaryDetail
    {
        get => _scanSummaryDetail;
        private set => SetProperty(ref _scanSummaryDetail, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public ICommand ScanCommand { get; }
    public ICommand SelectRecommendedCommand { get; }
    public ICommand SelectAllCommonCommand { get; }
    public ICommand CleanSelectedCommand { get; }
    public ICommand AddCustomFolderCommand { get; }
    public ICommand RemoveCustomFolderCommand { get; }
    public ICommand ScanLargeFilesCommand { get; }
    public ICommand DeleteSelectedLargeFilesCommand { get; }

    public CleanerViewModel(JunkCleanerService cleanerService, SettingsService settingsService)
    {
        _cleanerService = cleanerService;
        _settingsService = settingsService;

        foreach (var cat in _cleanerService.CreateCategories())
        {
            AllCategories.Add(cat);
            if (cat.Tab == CleanerTab.Common && cat.Group == "Windows")
                WindowsCommonCategories.Add(cat);
            else if (cat.Tab == CleanerTab.Common && cat.Group == "Tarayıcı")
                BrowserCommonCategories.Add(cat);
            else
                AdvancedCategories.Add(cat);
        }

        foreach (var f in _settingsService.Current.CustomCleanerFolders)
            CustomFolders.Add(f);

        ScanCommand = new AsyncRelayCommand(ScanAsync);
        SelectRecommendedCommand = new RelayCommand(SelectRecommendedOnly);
        SelectAllCommonCommand = new RelayCommand(() =>
        {
            foreach (var c in AllCategories.Where(x => x.Tab == CleanerTab.Common))
                c.IsSelected = true;
        });
        CleanSelectedCommand = new AsyncRelayCommand(CleanSelectedAsync);
        AddCustomFolderCommand = new RelayCommand(AddCustomFolder);
        RemoveCustomFolderCommand = new RelayCommand(p => { if (p is string path) RemoveCustomFolder(path); });
        ScanLargeFilesCommand = new AsyncRelayCommand(ScanLargeFilesAsync);
        DeleteSelectedLargeFilesCommand = new AsyncRelayCommand(DeleteSelectedLargeFilesAsync);
    }

    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusMessage = "Gereksiz dosyalar, önbellekler ve kalıntılar taranıyor...";
        try
        {
            await _cleanerService.ScanCategoriesAsync(AllCategories);
            HasScanned = true;

            RecommendedCategories.Clear();
            foreach (var rec in AllCategories.Where(c => c.IsRecommended || c.ScannedBytes > 0)
                                             .OrderByDescending(c => c.ScannedBytes))
            {
                if (rec.IsRecommended)
                    RecommendedCategories.Add(rec);
            }

            long totalFound = AllCategories.Sum(c => c.ScannedBytes);
            long recFound = RecommendedCategories.Sum(c => c.ScannedBytes);
            int totalItems = AllCategories.Sum(c => c.ScannedFileCount);

            ScanSummaryTitle = $"Toplam {FormatHelper.FormatBytes(totalFound)} gereksiz veri tespit edildi";
            ScanSummaryDetail = RecommendedCategories.Count > 0
                ? $"{totalItems:N0} öğe tarandı. {RecommendedCategories.Count} kategori ({FormatHelper.FormatBytes(recFound)}) 'Temizlenmesi Önerilir' olarak işaretlendi."
                : $"{totalItems:N0} öğe tarandı. Sisteminiz şu anda oldukça temiz görünüyor.";
            StatusMessage = $"Tarama tamamlandı — {FormatHelper.FormatBytes(totalFound)} temizlenebilir alan bulundu.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SelectRecommendedOnly()
    {
        foreach (var cat in AllCategories)
            cat.IsSelected = cat.IsRecommended || (cat.HasData && cat.DefaultChecked);
        StatusMessage = "Önerilen yüksek boyutlu gereksiz dosya kategorileri seçildi.";
    }

    private async Task CleanSelectedAsync()
    {
        var selected = AllCategories.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Temizlemek için en az bir kategori seçin.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"{selected.Count} kategori temizleniyor...";
        try
        {
            var (freed, deleted, skipped) = await _cleanerService.CleanSelectedAsync(AllCategories);

            RecommendedCategories.Clear();
            foreach (var rec in AllCategories.Where(c => c.IsRecommended).OrderByDescending(c => c.ScannedBytes))
                RecommendedCategories.Add(rec);

            long remaining = AllCategories.Sum(c => c.ScannedBytes);
            ScanSummaryTitle = $"{FormatHelper.FormatBytes(freed)} temizlendi";
            ScanSummaryDetail = $"{deleted:N0} dosya silindi, {skipped:N0} kullanımdaki dosya atlandı. Kalan taranabilir boyut: {FormatHelper.FormatBytes(remaining)}.";
            StatusMessage = $"Temizlik tamamlandı: {FormatHelper.FormatBytes(freed)} alan açıldı ({deleted:N0} dosya silindi, {skipped} kullanımda olduğu için atlandı).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddCustomFolder()
    {
        using var dlg = new WinForms.FolderBrowserDialog
        {
            Description = "Özel temizlik listesine eklenecek klasörü seçin"
        };
        if (dlg.ShowDialog() == WinForms.DialogResult.OK)
        {
            var path = dlg.SelectedPath;
            if (FormatHelper.IsProtectedPath(path))
            {
                MessageBox.Show("Kritik sistem veya kullanıcı kök klasörleri özel temizliğe eklenemez.", "Güvenlik Uyarısı",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!CustomFolders.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                CustomFolders.Add(path);
                _settingsService.Current.CustomCleanerFolders = CustomFolders.ToList();
                _settingsService.Save();

                var customCat = AllCategories.FirstOrDefault(c => c.Id == "win_custom");
                if (customCat is not null) customCat.IsSelected = true;
                StatusMessage = $"Özel klasör eklendi: {path}";
            }
        }
    }

    private void RemoveCustomFolder(string path)
    {
        CustomFolders.Remove(path);
        _settingsService.Current.CustomCleanerFolders = CustomFolders.ToList();
        _settingsService.Save();
    }

    private async Task ScanLargeFilesAsync()
    {
        IsBusy = true;
        StatusMessage = "Kullanıcı klasörlerinde 500 MB üzeri büyük dosyalar taranıyor...";
        try
        {
            var files = await _cleanerService.ScanLargeFilesAsync(500);
            LargeFiles.Clear();
            foreach (var f in files) LargeFiles.Add(f);
            StatusMessage = $"{files.Count} adet büyük dosya (>500 MB) bulundu.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteSelectedLargeFilesAsync()
    {
        var selected = LargeFiles.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return;

        long total = selected.Sum(x => x.SizeBytes);
        var confirm = MessageBox.Show(
            $"Seçili {selected.Count} büyük dosya ({FormatHelper.FormatBytes(total)}) kalıcı olarak silinecek.\nBu işlem geri alınamaz. Onaylıyor musunuz?",
            "LRX Core — Büyük Dosya Silme",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            int removed = 0;
            long freed = 0;
            await Task.Run(() =>
            {
                foreach (var f in selected)
                {
                    try
                    {
                        if (File.Exists(f.FilePath))
                        {
                            File.Delete(f.FilePath);
                            freed += f.SizeBytes;
                            removed++;
                        }
                    }
                    catch { }
                }
            });

            foreach (var f in selected.Where(x => !File.Exists(x.FilePath)).ToList())
                LargeFiles.Remove(f);

            StatusMessage = $"{removed} büyük dosya silindi ({FormatHelper.FormatBytes(freed)} açıldı).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
