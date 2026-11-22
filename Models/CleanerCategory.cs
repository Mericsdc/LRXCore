using LRXCore.Helpers;

namespace LRXCore.Models;

public enum CleanerTab
{
    Common,
    Advanced
}

public sealed class CleanerCategory : ObservableObject
{
    private bool _isSelected;
    private long _scannedBytes;
    private int _scannedFileCount;
    private bool _hasScanned;
    private bool _isRecommended;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = "Windows"; // Windows | Tarayıcı | Uygulama & Sistem
    public CleanerTab Tab { get; set; } = CleanerTab.Common;
    public string IconGlyph { get; set; } = "\uE8B7";
    public string Description { get; set; } = string.Empty;
    public bool DefaultChecked { get; set; } = true;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public long ScannedBytes
    {
        get => _scannedBytes;
        set
        {
            if (SetProperty(ref _scannedBytes, value))
            {
                OnPropertyChanged(nameof(SizeDisplay));
                OnPropertyChanged(nameof(HasData));
            }
        }
    }

    public int ScannedFileCount
    {
        get => _scannedFileCount;
        set
        {
            if (SetProperty(ref _scannedFileCount, value))
                OnPropertyChanged(nameof(DetailDisplay));
        }
    }

    public bool HasScanned
    {
        get => _hasScanned;
        set => SetProperty(ref _hasScanned, value);
    }

    public bool IsRecommended
    {
        get => _isRecommended;
        set => SetProperty(ref _isRecommended, value);
    }

    public bool HasData => ScannedBytes > 0 || ScannedFileCount > 0;
    public string SizeDisplay => HasScanned ? FormatHelper.FormatBytes(ScannedBytes) : "—";
    public string DetailDisplay => HasScanned ? $"{ScannedFileCount:N0} öğe • {FormatHelper.FormatBytes(ScannedBytes)}" : Description;
}

public sealed class LargeFileItem : ObservableObject
{
    private bool _isSelected;

    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string SizeDisplay => FormatHelper.FormatBytes(SizeBytes);
}
