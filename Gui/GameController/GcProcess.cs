using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tyr.Gui.GameController;

internal sealed class GcProcess : IDisposable
{
    public enum Status { Idle, Downloading, Running, Exited, Error }

    private volatile Status _status = Status.Idle;
    private volatile float _downloadProgress; // 0–1
    private string _statusMessage = "";
    private string? _cachedVersion;

    private Process? _process;
    private CancellationTokenSource? _downloadCts;

    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("TyrSharp2", "1.0") } }
    };

    private static readonly string CacheDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tyr", "gc");

    public Status CurrentStatus     => _status;
    public float  DownloadProgress  => _downloadProgress;
    public string StatusMessage     => _statusMessage;
    public string? CachedVersion    => _cachedVersion;

    public GcProcess()
    {
        Directory.CreateDirectory(CacheDir);
        ScanCache();
        TryFindExistingProcess();
    }

    // ── cache ────────────────────────────────────────────────────────────────

    private void ScanCache()
    {
        // filename: ssl-game-controller_{version}_{os}_{arch}[.exe]
        foreach (var file in Directory.GetFiles(CacheDir, "ssl-game-controller_*"))
        {
            var parts = Path.GetFileNameWithoutExtension(file).Split('_');
            if (parts.Length >= 2)
            {
                _cachedVersion = parts[1]; // "v1.9.0"
                return;
            }
        }
    }

    private string? FindCachedExe() =>
        Directory.GetFiles(CacheDir, "ssl-game-controller_*").FirstOrDefault();

    // ── download ─────────────────────────────────────────────────────────────

    public void StartDownload()
    {
        _downloadCts?.Cancel();
        _downloadCts = new CancellationTokenSource();
        _status = Status.Downloading;
        _downloadProgress = 0f;
        Task.Run(() => DownloadAsync(_downloadCts.Token));
    }

    public void CancelDownload()
    {
        _downloadCts?.Cancel();
    }

    private async Task DownloadAsync(CancellationToken ct)
    {
        try
        {
            var json = await Http.GetStringAsync(
                "https://api.github.com/repos/RoboCup-SSL/ssl-game-controller/releases/latest", ct);

            var release = JsonSerializer.Deserialize<GithubRelease>(json)
                          ?? throw new Exception("Failed to parse GitHub release response");

            var assetName = GetAssetName(release.TagName);
            var asset = release.Assets.FirstOrDefault(a => a.Name == assetName)
                        ?? throw new Exception($"No release asset found for platform: {assetName}");

            var targetPath = Path.Combine(CacheDir, assetName);

            using var response = await Http.GetAsync(
                asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            await using var src = await response.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(targetPath);

            var buf = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read), ct);
                downloaded += read;
                if (total > 0) _downloadProgress = (float)downloaded / total;
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(targetPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            // Remove old cached versions
            foreach (var old in Directory.GetFiles(CacheDir, "ssl-game-controller_*"))
                if (old != targetPath) File.Delete(old);

            _cachedVersion = release.TagName;
            _statusMessage = $"Downloaded {release.TagName}";
            _status = Status.Idle;
        }
        catch (OperationCanceledException)
        {
            _status = Status.Idle;
        }
        catch (Exception ex)
        {
            _statusMessage = ex.Message;
            _status = Status.Error;
        }
    }

    // ── process ──────────────────────────────────────────────────────────────

    private bool TryFindExistingProcess()
    {
        // Search all processes for anything containing the GC name.
        // This is more robust on Unix where names might be truncated or varied.
        var existing = Process.GetProcesses()
            .FirstOrDefault(p =>
            {
                try
                {
                    return p.ProcessName.Contains("ssl-game-controller", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            });

        if (existing == null) return false;

        _process = existing;
        _process.EnableRaisingEvents = true;
        _process.Exited += OnProcessExited;

        if (OperatingSystem.IsWindows())
            Win32JobObject.AssignProcess(_process);

        _statusMessage = $"Attached to PID {_process.Id}";
        _status = Status.Running;
        return true;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_status == Status.Running)
        {
            _statusMessage = $"Process exited (code {_process?.ExitCode ?? -1})";
            _status = Status.Exited;
        }
    }

    /// <param name="rconPort">Remote control TCP port (our client connects here).</param>
    /// <param name="uiPort">Web UI port (human browser).</param>
    /// <returns>True if a new process was started, false if already running or attached.</returns>
    public bool Start(int rconPort, int uiPort)
    {
        if (_status == Status.Running) return false;

        if (TryFindExistingProcess()) return false;

        var exe = FindCachedExe();
        if (exe == null)
        {
            _statusMessage = "No binary cached — download first.";
            _status = Status.Error;
            return false;
        }

        try
        {
            _process?.Dispose();
            
            // Delete state store so we start fresh
            try
            {
                var stateStore = Path.Combine(Environment.CurrentDirectory, "config", "state-store.json.stream");
                if (File.Exists(stateStore))
                    File.Delete(stateStore);
            }
            catch { /* ignore */ }

            _process = new Process();
            _process.StartInfo.FileName = exe;
            _process.StartInfo.Arguments =
                $"-remoteControlAddress :{rconPort} -address :{uiPort}";
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.CreateNoWindow = true;
            // Redirect so the GC doesn't write to our (non-existent) console
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardError  = true;
            _process.EnableRaisingEvents = true;
            _process.OutputDataReceived += (_, _) => { };
            _process.ErrorDataReceived  += (_, _) => { };
            _process.Exited += OnProcessExited;

            _process.Start();

            if (OperatingSystem.IsWindows())
                Win32JobObject.AssignProcess(_process);

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _statusMessage = $"PID {_process.Id}";
            _status = Status.Running;
            return true;
        }
        catch (Exception ex)
        {
            _statusMessage = ex.Message;
            _status = Status.Error;
            return false;
        }
    }

    public void Stop()
    {
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch { /* already dead */ }

        _process?.Dispose();
        _process = null;
        _status = Status.Idle;
        _statusMessage = "";
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static class Win32JobObject
    {
        private static readonly IntPtr JobHandle;

        static Win32JobObject()
        {
            if (!OperatingSystem.IsWindows()) return;

            JobHandle = CreateJobObject(IntPtr.Zero, null);
            var basicInfo = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            };
            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = basicInfo
            };

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
                if (!SetInformationJobObject(JobHandle, JobObjectExtendedLimitInformation, extendedInfoPtr, (uint)length))
                    throw new Exception($"Failed to set Job Object information: {Marshal.GetLastWin32Error()}");
            }
            finally
            {
                Marshal.FreeHGlobal(extendedInfoPtr);
            }
        }

        public static void AssignProcess(Process process)
        {
            if (!OperatingSystem.IsWindows() || JobHandle == IntPtr.Zero) return;
            AssignProcessToJobObject(JobHandle, process.Handle);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll")]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinitiy;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoCounters;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryLimit;
            public UIntPtr PeakJobMemoryLimit;
        }
    }

    private static string GetAssetName(string version)
    {
        var os = OperatingSystem.IsWindows() ? "windows" :
                 OperatingSystem.IsMacOS()   ? "darwin"  : "linux";

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.Arm   => "arm",
            _                  => "amd64",
        };

        var ext = OperatingSystem.IsWindows() ? ".exe" : "";
        return $"ssl-game-controller_{version}_{os}_{arch}{ext}";
    }

    public void Dispose()
    {
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        Stop();
    }
}

// ── GitHub API models (file-scoped, not visible outside this file) ────────────

file sealed class GithubRelease
{
    [JsonPropertyName("tag_name")] public string       TagName { get; set; } = "";
    [JsonPropertyName("assets")]   public GithubAsset[] Assets { get; set; } = [];
}

file sealed class GithubAsset
{
    [JsonPropertyName("name")]                 public string Name               { get; set; } = "";
    [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
}
