using LRXCore.Helpers;

namespace LRXCore.Models;

public sealed class AppUpdateItem : ObservableObject
{
    private bool _isSelected = true;
    private string _statusText = "Güncelleme Mevcut";
    private bool _isUpdating;
    private bool _isUpdated;

    public string Name { get; set; } = string.Empty;
    public string WingetId { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string AvailableVersion { get; set; } = string.Empty;
    public string Source { get; set; } = "winget";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        set
        {
            if (SetProperty(ref _isUpdating, value))
            {
                OnPropertyChanged(nameof(ActionButtonText));
                OnPropertyChanged(nameof(CanUpdate));
            }
        }
    }

    public bool IsUpdated
    {
        get => _isUpdated;
        set
        {
            if (SetProperty(ref _isUpdated, value))
            {
                if (value)
                    IsSelected = false;
                OnPropertyChanged(nameof(CanUpdate));
            }
        }
    }

    public string ActionButtonText => IsUpdating ? "Güncelleniyor..." : "Güncelle";
    public bool CanUpdate => !IsUpdating && !IsUpdated;
}
