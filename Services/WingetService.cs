using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using LRXCore.Helpers;
using LRXCore.Models;

namespace LRXCore.Services;

public sealed class WingetService
{
    private const int ExitCodeAlreadyInstalledOrNoUpgrade = -1978335189; // 0x8A15002B
    private const int ExitCodeInstallElevationProhibited = -1978335135; // 0x8A150061

    public static bool IsElevationProhibited(int exitCode, string combinedOutput)
    {
        if (exitCode == ExitCodeInstallElevationProhibited)
            return true;

        if (string.IsNullOrWhiteSpace(combinedOutput))
            return false;

        return combinedOutput.Contains("cannot be run from an administrator context", StringComparison.OrdinalIgnoreCase) ||
               combinedOutput.Contains("elevationProhibited", StringComparison.OrdinalIgnoreCase) ||
               combinedOutput.Contains("yönetici bağlamında", StringComparison.OrdinalIgnoreCase) ||
               combinedOutput.Contains("yönetici olarak çalıştırılamaz", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<(bool success, bool alreadyInstalled, string message)> InstallPackageAsync(
        string wingetId,
        string source,
        Action<string>? onLog,
        CancellationToken ct = default)
    {
        var args = new StringBuilder();
        args.Append($"install --id \"{wingetId}\" --exact --silent --accept-package-agreements --accept-source-agreements");
        if (string.Equals(source, "msstore", StringComparison.OrdinalIgnoreCase))
            args.Append(" --source msstore");

        var (exitCode, joined) = await RunWithUnelevatedFallbackAsync(args.ToString(), onLog, ct);

        if (exitCode == ExitCodeAlreadyInstalledOrNoUpgrade ||
            joined.Contains("already installed", StringComparison.OrdinalIgnoreCase) ||
            joined.Contains("zaten yüklü", StringComparison.OrdinalIgnoreCase) ||
            joined.Contains("No available upgrade found", StringComparison.OrdinalIgnoreCase))
        {
            return (true, true, "Zaten kurulu");
        }

        if (exitCode == 0)
            return (true, false, "Başarıyla kuruldu");

        return (false, false, $"Çıkış kodu: {exitCode}");
    }

    public async Task<List<AppUpdateItem>> CheckUpdatesAsync(CancellationToken ct = default)
    {
        var lines = new List<string>();
        await RunWingetProcessAsync("upgrade --accept-source-agreements", line => lines.Add(line), ct, preserveCarriageReturns: true);
        var raw = string.Join("\n", lines);
        return ParseUpgradeOutput(raw);
    }

    public async Task<(bool success, string message)> UpgradePackageAsync(
        string wingetId,
        string source,
        Action<string>? onLog,
        CancellationToken ct = default)
    {
        var args = new StringBuilder();
        args.Append($"upgrade --id \"{wingetId}\" --exact --silent --accept-package-agreements --accept-source-agreements");
        if (string.Equals(source, "msstore", StringComparison.OrdinalIgnoreCase))
            args.Append(" --source msstore");

        var (exitCode, _) = await RunWithUnelevatedFallbackAsync(args.ToString(), onLog, ct);
        return exitCode == 0
            ? (true, "Güncellendi")
            : (false, $"Hata kodu: {exitCode}");
    }

    private static async Task<(int exitCode, string joinedOutput)> RunWithUnelevatedFallbackAsync(
        string args,
        Action<string>? onLog,
        CancellationToken ct)
    {
        var outputLines = new List<string>();
        var exitCode = await RunWingetProcessAsync(args, line =>
        {
            outputLines.Add(line);
            onLog?.Invoke(line);
        }, ct);

        var joined = string.Join("\n", outputLines);

        if (exitCode != 0 && IsElevationProhibited(exitCode, joined))
        {
            onLog?.Invoke("Yönetici kısıtlaması tespit edildi — standart kullanıcı (yönetici olmayan) modunda otomatik yeniden deneniyor...");
            outputLines.Clear();
            try
            {
                exitCode = await UnelevatedProcessHelper.RunWingetUnelevatedAsync(args, line =>
                {
                    outputLines.Add(line);
                    onLog?.Invoke(line);
                }, ct);
                joined = string.Join("\n", outputLines);
            }
            catch (Exception ex)
            {
                onLog?.Invoke($"Standart kullanıcı modunda başlatılamadı: {ex.Message}");
            }
        }

        return (exitCode, joined);
    }

    /// <summary>
    /// Locale-independent parser for `winget upgrade` tabular output.
    /// Locates the dashed separator line and derives column offsets from the header line directly above it.
    /// </summary>
    public static List<AppUpdateItem> ParseUpgradeOutput(string rawOutput)
    {
        var results = new List<AppUpdateItem>();
        if (string.IsNullOrWhiteSpace(rawOutput))
            return results;

        // Normalize carriage-return spinner/progress updates on each line
        var normalizedLines = rawOutput
            .Split('\n')
            .Select(l =>
            {
                if (!l.Contains('\r')) return l.TrimEnd();
                var parts = l.Split('\r');
                for (int i = parts.Length - 1; i >= 0; i--)
                {
                    if (!string.IsNullOrWhiteSpace(parts[i]))
                        return parts[i].TrimEnd();
                }
                return string.Empty;
            })
            .ToList();

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < normalizedLines.Count; i++)
        {
            var trimmed = normalizedLines[i].Trim();
            if (trimmed.Length < 20 || !trimmed.All(c => c == '-'))
                continue;

            var headerLine = normalizedLines[i - 1];
            var colStarts = DetectColumnOffsets(headerLine);
            if (colStarts.Count < 4)
                continue;

            // Parse rows following separator until blank line or next summary/table
            for (int r = i + 1; r < normalizedLines.Count; r++)
            {
                var row = normalizedLines[r];
                if (string.IsNullOrWhiteSpace(row))
                    break;

                // Stop if we hit a footer summary like "13 upgrades available." or another dashed line
                var rowTrim = row.Trim();
                if (rowTrim.All(c => c == '-') || row.Length < colStarts[1] + 2)
                    break;

                string Slice(int colIndex)
                {
                    int start = colStarts[colIndex];
                    if (start >= row.Length) return string.Empty;
                    int end = colIndex + 1 < colStarts.Count ? Math.Min(colStarts[colIndex + 1], row.Length) : row.Length;
                    return start < end ? row[start..end].Trim() : string.Empty;
                }

                var name = Slice(0);
                var id = Slice(1);
                var version = Slice(2);
                var available = Slice(3);
                var source = colStarts.Count >= 5 ? Slice(4) : "winget";

                // Winget package IDs never contain spaces and must look like Vendor.App or StoreId
                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(id) ||
                    id.Contains(' ') ||
                    string.IsNullOrWhiteSpace(version) ||
                    string.IsNullOrWhiteSpace(available))
                {
                    continue;
                }

                if (seenIds.Add(id))
                {
                    results.Add(new AppUpdateItem
                    {
                        Name = name,
                        WingetId = id,
                        CurrentVersion = version,
                        AvailableVersion = available,
                        Source = string.IsNullOrWhiteSpace(source) ? "winget" : source
                    });
                }
            }
        }

        return results;
    }

    private static List<int> DetectColumnOffsets(string headerLine)
    {
        var offsets = new List<int>();
        if (string.IsNullOrWhiteSpace(headerLine))
            return offsets;

        // Column headers are separated by 2+ spaces in winget tables (handles localized multi-word headers if any)
        var matches = Regex.Matches(headerLine, @"\S+(?:\s\S+)*");
        foreach (Match m in matches)
            offsets.Add(m.Index);

        return offsets;
    }

    private static async Task<int> RunWingetProcessAsync(
        string arguments,
        Action<string>? onLine,
        CancellationToken ct,
        bool preserveCarriageReturns = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "winget.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();

        var stdoutTask = Task.Run(async () =>
        {
            if (preserveCarriageReturns)
            {
                var content = await process.StandardOutput.ReadToEndAsync(ct);
                if (!string.IsNullOrEmpty(content))
                    onLine?.Invoke(content);
                return;
            }

            while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                var clean = CleanSpinnerLine(line);
                if (!string.IsNullOrWhiteSpace(clean))
                    onLine?.Invoke(clean);
            }
        }, ct);

        var stderrTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(ct) is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    onLine?.Invoke(line.Trim());
            }
        }, ct);

        using var reg = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        });

        await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(ct));
        return process.ExitCode;
    }

    private static string CleanSpinnerLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed is "-" or "\\" or "|" or "/")
            return string.Empty;
        return trimmed;
    }
}
