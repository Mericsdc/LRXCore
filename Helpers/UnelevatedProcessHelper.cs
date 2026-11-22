using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace LRXCore.Helpers;

/// <summary>
/// Launches a process under the current desktop user's non-elevated (standard) token
/// when the parent application is running elevated as Administrator.
/// Used for installers (such as Spotify) that reject administrator contexts.
/// </summary>
public static class UnelevatedProcessHelper
{
    public static bool IsCurrentProcessElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static async Task<int> RunWingetUnelevatedAsync(
        string wingetArguments,
        Action<string>? onLine,
        CancellationToken ct = default)
    {
        if (!IsCurrentProcessElevated())
        {
            return await RunDirectAsync("winget.exe", wingetArguments, onLine, ct);
        }

        string id = Guid.NewGuid().ToString("N")[..10];
        string tempDir = Path.GetTempPath();
        string cmdPath = Path.Combine(tempDir, $"lrx_winget_{id}.cmd");
        string logPath = Path.Combine(tempDir, $"lrx_winget_{id}.log");

        // Write UTF-8 without BOM so cmd.exe parses @echo off cleanly
        var scriptContent =
            "@echo off\r\n" +
            "chcp 65001 >nul\r\n" +
            $"winget.exe {wingetArguments} > \"{logPath}\" 2>&1\r\n" +
            "exit /b %ERRORLEVEL%\r\n";

        await File.WriteAllTextAsync(cmdPath, scriptContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
        // Pre-create empty log file so tailing FileStream can open it immediately
        await File.WriteAllTextAsync(logPath, string.Empty, new UTF8Encoding(false), ct);

        IntPtr hShellProc = IntPtr.Zero;
        IntPtr hShellToken = IntPtr.Zero;
        IntPtr hPrimaryToken = IntPtr.Zero;
        var pi = new PROCESS_INFORMATION();

        try
        {
            EnablePrivilegeBestEffort("SeIncreaseQuotaPrivilege");

            uint shellPid = GetDesktopShellPid();
            if (shellPid == 0)
                throw new InvalidOperationException("Masaüstü kabuk süreci (explorer.exe) bulunamadı.");

            hShellProc = OpenProcess(PROCESS_QUERY_INFORMATION, false, shellPid);
            if (hShellProc == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Masaüstü kabuk süreci açılamadı.");

            if (!OpenProcessToken(hShellProc, TOKEN_DUPLICATE, out hShellToken))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Masaüstü kabuk belirteci açılamadı.");

            const uint primaryAccess = TOKEN_QUERY | TOKEN_DUPLICATE | TOKEN_ASSIGN_PRIMARY | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID;
            if (!DuplicateTokenEx(
                    hShellToken,
                    primaryAccess,
                    IntPtr.Zero,
                    SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                    TOKEN_TYPE.TokenPrimary,
                    out hPrimaryToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Standart kullanıcı belirteci çoğaltılamadı.");
            }

            string cmdExe = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            string cmdLine = $"\"{cmdExe}\" /d /c \"\"{cmdPath}\"\"";
            string workDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var si = new STARTUPINFO
            {
                cb = Marshal.SizeOf<STARTUPINFO>(),
                dwFlags = STARTF_USESHOWWINDOW,
                wShowWindow = SW_HIDE
            };

            if (!CreateProcessWithTokenW(
                    hPrimaryToken,
                    0,
                    cmdExe,
                    cmdLine,
                    CREATE_NO_WINDOW,
                    IntPtr.Zero,
                    workDir,
                    ref si,
                    out pi))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Standart kullanıcı işlemi başlatılamadı.");
            }

            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var lineBuffer = new StringBuilder();

            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    try
                    {
                        using var proc = Process.GetProcessById((int)pi.dwProcessId);
                        proc.Kill(entireProcessTree: true);
                    }
                    catch { }
                    ct.ThrowIfCancellationRequested();
                }

                DrainLogReader(reader, lineBuffer, onLine, flushPartial: false);

                uint waitRes = WaitForSingleObject(pi.hProcess, 0);
                if (waitRes == WAIT_OBJECT_0)
                    break;

                await Task.Delay(200, ct);
            }

            // Final flush after process exit
            DrainLogReader(reader, lineBuffer, onLine, flushPartial: true);

            return GetExitCodeProcess(pi.hProcess, out uint exitCode)
                ? unchecked((int)exitCode)
                : -1;
        }
        finally
        {
            if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
            if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
            if (hPrimaryToken != IntPtr.Zero) CloseHandle(hPrimaryToken);
            if (hShellToken != IntPtr.Zero) CloseHandle(hShellToken);
            if (hShellProc != IntPtr.Zero) CloseHandle(hShellProc);

            try { if (File.Exists(cmdPath)) File.Delete(cmdPath); } catch { }
            try { if (File.Exists(logPath)) File.Delete(logPath); } catch { }
        }
    }

    private static void DrainLogReader(StreamReader reader, StringBuilder buffer, Action<string>? onLine, bool flushPartial)
    {
        char[] chunk = new char[1024];
        int read;
        while ((read = reader.Read(chunk, 0, chunk.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                char c = chunk[i];
                if (c == '\r' || c == '\n')
                {
                    FlushBufferLine(buffer, onLine);
                }
                else
                {
                    buffer.Append(c);
                }
            }
        }

        if (flushPartial && buffer.Length > 0)
            FlushBufferLine(buffer, onLine);
    }

    private static void FlushBufferLine(StringBuilder buffer, Action<string>? onLine)
    {
        if (buffer.Length == 0) return;
        var text = buffer.ToString().Trim();
        buffer.Clear();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text is "-" or "\\" or "|" or "/") return;
        onLine?.Invoke(text);
    }

    private static uint GetDesktopShellPid()
    {
        IntPtr hwnd = GetShellWindow();
        if (hwnd != IntPtr.Zero)
        {
            _ = GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != 0) return pid;
        }

        int currentSession = Process.GetCurrentProcess().SessionId;
        var explorer = Process.GetProcessesByName("explorer")
            .FirstOrDefault(p => p.SessionId == currentSession);
        return explorer is not null ? (uint)explorer.Id : 0;
    }

    private static void EnablePrivilegeBestEffort(string privilegeName)
    {
        IntPtr hToken = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
                return;

            if (!LookupPrivilegeValue(null, privilegeName, out LUID luid))
                return;

            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SE_PRIVILEGE_ENABLED
            };

            AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
        finally
        {
            if (hToken != IntPtr.Zero) CloseHandle(hToken);
        }
    }

    private static async Task<int> RunDirectAsync(string fileName, string arguments, Action<string>? onLine, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdoutTask = Task.Run(async () =>
        {
            while (await proc.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                var t = line.Trim();
                if (!string.IsNullOrWhiteSpace(t) && t is not ("-" or "\\" or "|" or "/"))
                    onLine?.Invoke(t);
            }
        }, ct);

        var stderrTask = Task.Run(async () =>
        {
            while (await proc.StandardError.ReadLineAsync(ct) is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    onLine?.Invoke(line.Trim());
            }
        }, ct);

        using var reg = ct.Register(() =>
        {
            try { if (!proc.HasExited) proc.Kill(true); } catch { }
        });

        await Task.WhenAll(stdoutTask, stderrTask, proc.WaitForExitAsync(ct));
        return proc.ExitCode;
    }

    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_ADJUST_DEFAULT = 0x0080;
    private const uint TOKEN_ADJUST_SESSIONID = 0x0100;
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const int STARTF_USESHOWWINDOW = 0x00000001;
    private const short SW_HIDE = 0;
    private const uint WAIT_OBJECT_0 = 0x00000000;

    private enum SECURITY_IMPERSONATION_LEVEL
    {
        SecurityAnonymous = 0,
        SecurityIdentification = 1,
        SecurityImpersonation = 2,
        SecurityDelegation = 3
    }

    private enum TOKEN_TYPE
    {
        TokenPrimary = 1,
        TokenImpersonation = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken,
        uint dwDesiredAccess,
        IntPtr lpTokenAttributes,
        SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
        TOKEN_TYPE TokenType,
        out IntPtr phNewToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr TokenHandle,
        bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        IntPtr PreviousState,
        IntPtr ReturnLength);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessWithTokenW(
        IntPtr hToken,
        uint dwLogonFlags,
        string? lpApplicationName,
        string lpCommandLine,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);
}
