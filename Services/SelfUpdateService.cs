using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using LRXCore.Models;

namespace LRXCore.Services;

public sealed class SelfUpdateService
{
    public const string RepoOwner = "Mericsdc";
    public const string RepoName = "LRXCore";
    public const string RepoUrl = $"https://github.com/{RepoOwner}/{RepoName}";
    public const string LatestReleaseApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LRXCore-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static Version GetCurrentVersion()
    {
        var ver = Assembly.GetEntryAssembly()?.GetName().Version
                  ?? Assembly.GetExecutingAssembly().GetName().Version
                  ?? new Version(1, 0, 0);
        return new Version(ver.Major, Math.Max(0, ver.Minor), Math.Max(0, ver.Build));
    }

    public static string GetCurrentVersionDisplay()
    {
        var v = GetCurrentVersion();
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>
    /// Compares a GitHub release tag (e.g. "v1.2.0" or "1.2.0-beta") with the current version.
    /// Returns > 0 if tagName is newer than currentVersion.
    /// </summary>
    public static int CompareVersions(string? tagName, Version currentVersion)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return 0;

        var clean = tagName.Trim().TrimStart('v', 'V');
        var dashIdx = clean.IndexOfAny(new[] { '-', '+' });
        if (dashIdx > 0)
            clean = clean[..dashIdx];

        if (!Version.TryParse(clean, out var parsed))
            return 0;

        var normalizedRemote = new Version(parsed.Major, Math.Max(0, parsed.Minor), Math.Max(0, parsed.Build));
        var normalizedLocal = new Version(currentVersion.Major, Math.Max(0, currentVersion.Minor), Math.Max(0, currentVersion.Build));
        return normalizedRemote.CompareTo(normalizedLocal);
    }

    public async Task<SelfUpdateInfo> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var currentVer = GetCurrentVersion();
        var currentStr = GetCurrentVersionDisplay();

        try
        {
            using var resp = await Http.GetAsync(LatestReleaseApiUrl, ct);
            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                return new SelfUpdateInfo
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = currentStr,
                    LatestVersion = currentStr,
                    StatusMessage = $"GitHub deposunda ({RepoOwner}/{RepoName}) henüz yayınlanmış bir sürüm (Release) bulunmuyor."
                };
            }

            if (!resp.IsSuccessStatusCode)
            {
                return new SelfUpdateInfo
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = currentStr,
                    LatestVersion = currentStr,
                    StatusMessage = $"Sürüm kontrolü yapılamadı (HTTP {(int)resp.StatusCode})."
                };
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? currentStr : currentStr;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? tag : tag;
            var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            var htmlUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? RepoUrl : RepoUrl;

            string? assetUrl = null;
            string? assetName = null;
            long assetSize = 0;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var aName = a.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                    var aUrl = a.TryGetProperty("browser_download_url", out var au) ? au.GetString() : null;
                    long aSize = a.TryGetProperty("size", out var az) && az.TryGetInt64(out var s) ? s : 0;

                    if (string.IsNullOrEmpty(aUrl) || !aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (aName.Contains("Setup", StringComparison.OrdinalIgnoreCase) || assetUrl is null)
                    {
                        assetUrl = aUrl;
                        assetName = aName;
                        assetSize = aSize;
                        if (aName.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                            break;
                    }
                }
            }

            bool newer = CompareVersions(tag, currentVer) > 0;
            var cleanLatest = tag.Trim().TrimStart('v', 'V');

            return new SelfUpdateInfo
            {
                IsUpdateAvailable = newer,
                CurrentVersion = currentStr,
                LatestVersion = cleanLatest,
                ReleaseTitle = name,
                ReleaseNotes = body,
                ReleasePageUrl = htmlUrl,
                InstallerDownloadUrl = assetUrl,
                InstallerFileName = assetName,
                InstallerSizeBytes = assetSize,
                StatusMessage = newer
                    ? $"Yeni sürüm mevcut: v{cleanLatest} (Mevcut: v{currentStr})"
                    : $"LRX Core güncel (v{currentStr})."
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SelfUpdateInfo
            {
                IsUpdateAvailable = false,
                CurrentVersion = currentStr,
                LatestVersion = currentStr,
                StatusMessage = $"GitHub bağlantı hatası: {ex.Message}"
            };
        }
    }

    public async Task<(bool launched, string message)> DownloadAndLaunchInstallerAsync(
        SelfUpdateInfo info,
        IProgress<double>? progress,
        Action<string>? onStatus,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(info.InstallerDownloadUrl))
        {
            Process.Start(new ProcessStartInfo(info.ReleasePageUrl) { UseShellExecute = true });
            return (false, "Release içinde doğrudan .exe kurulum dosyası bulunamadı; GitHub sürüm sayfası tarayıcıda açıldı.");
        }

        var fileName = string.IsNullOrWhiteSpace(info.InstallerFileName)
            ? $"LRXCore-Setup-{info.LatestVersion}.exe"
            : info.InstallerFileName;

        var targetPath = Path.Combine(Path.GetTempPath(), fileName);

        try
        {
            onStatus?.Invoke($"İndiriliyor: {fileName}...");
            using var downloadClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            downloadClient.DefaultRequestHeaders.UserAgent.ParseAdd("LRXCore-Updater/1.0");

            using var response = await downloadClient.GetAsync(info.InstallerDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? info.InstallerSizeBytes;
            await using (var remoteStream = await response.Content.ReadAsStreamAsync(ct))
            await using (var localStream = File.Create(targetPath))
            {
                var buffer = new byte[81920];
                long readTotal = 0;
                int read;
                while ((read = await remoteStream.ReadAsync(buffer, ct)) > 0)
                {
                    await localStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    readTotal += read;
                    if (totalBytes > 0)
                        progress?.Report(Math.Clamp(readTotal * 100.0 / totalBytes, 0, 100));
                }
            }

            progress?.Report(100);
            onStatus?.Invoke("Kurulum sihirbazı sessiz güncelleme için başlatılıyor...");

            var psi = new ProcessStartInfo(targetPath)
            {
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CLOSEAPPLICATIONS",
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
            return (true, "Güncelleme başlatıldı, LRX Core yeniden açılacak.");
        }
        catch (OperationCanceledException)
        {
            try { if (File.Exists(targetPath)) File.Delete(targetPath); } catch { }
            return (false, "İndirme iptal edildi.");
        }
        catch (Exception ex)
        {
            return (false, $"Güncelleme indirilemedi: {ex.Message}");
        }
    }
}
