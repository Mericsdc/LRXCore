using System.IO;
using System.Text.Json;
using LRXCore.Models;
using Microsoft.Win32;

namespace LRXCore.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string AppDataDirectory { get; }
    public string SettingsPath { get; }
    public string BackupsDirectory { get; }
    public string ProfilesDirectory { get; }
    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LRXCore");
        SettingsPath = Path.Combine(AppDataDirectory, "settings.json");
        BackupsDirectory = Path.Combine(AppDataDirectory, "Backups");
        ProfilesDirectory = Path.Combine(AppDataDirectory, "Profiles");

        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(ProfilesDirectory);

        Current = Load();
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                    return loaded;
            }
        }
        catch
        {
            // Fall back to defaults if corrupted
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDataDirectory);
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(SettingsPath, json);
        SyncWindowsStartup(Current.StartWithWindows);
    }

    public static void SyncWindowsStartup(bool enabled)
    {
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "LRXCore";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: true);
            if (key is null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exePath))
                    key.SetValue(valueName, $"\"{exePath}\" --minimized", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Registry write permission issue
        }
    }
}
