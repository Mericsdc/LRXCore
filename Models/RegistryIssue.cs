using LRXCore.Helpers;

namespace LRXCore.Models;

public sealed class RegistryIssue : ObservableObject
{
    private bool _isSelected = true;

    public string Category { get; set; } = string.Empty;
    public string RootKey { get; set; } = string.Empty; // HKCU / HKLM
    public string SubKeyPath { get; set; } = string.Empty;
    public string? ValueName { get; set; }
    public string? ValueData { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool DeleteEntireKey { get; set; }

    public string FullPath => string.IsNullOrEmpty(ValueName)
        ? $@"{RootKey}\{SubKeyPath}"
        : $@"{RootKey}\{SubKeyPath} -> {ValueName}";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class RegistryBackupItem
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }

    public string DateDisplay => CreatedAt.ToString("dd.MM.yyyy HH:mm:ss");
    public string SizeDisplay => FormatHelper.FormatBytes(SizeBytes);
}
