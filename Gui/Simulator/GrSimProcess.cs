using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tyr.Gui.Simulator;

// Release asset naming (as of v2.6.0):
//   Windows : grsim_{ver}_win32.zip
//   macOS   : grsim_{ver}_osx-universal.tar.gz   (also a .sh installer — we skip that)
//   Linux   : grsim_{ver}_x86_64.tar.gz           (also .sh and .tar.Z — we skip those)

internal sealed class GrSimProcess : IDisposable
{
    public enum Status { Idle, Downloading, Running, Exited, Error }

    private volatile Status _status = Status.Idle;
    private volatile float _downloadProgress; // 0–1
    private string _statusMessage = "";
    private string? _cachedVersion;

    private Process? _process;
    private CancellationTokenSource? _downloadCts;
    private DateTime _nextExternalScan = DateTime.MinValue;

    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("TyrSharp2", "1.0") } }
    };

    private static readonly string CacheDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tyr", "grsim");

    public Status CurrentStatus    => _status;
    public float  DownloadProgress => _downloadProgress;
    public string StatusMessage    => _statusMessage;
    public string? CachedVersion   => _cachedVersion;

    public GrSimProcess()
    {
        Directory.CreateDirectory(CacheDir);
        ScanCache();
        AttachExternalProcess();
    }

    // ── cache ────────────────────────────────────────────────────────────────

    private void ScanCache()
    {
        if (FindCachedExe() == null) return;

        var versionFile = Path.Combine(CacheDir, "version.txt");
        _cachedVersion = File.Exists(versionFile)
            ? File.ReadAllText(versionFile).Trim()
            : "cached";
    }

    private string? FindCachedExe()
    {
        // Windows zip extracts to something like bin/grSim.exe
        if (OperatingSystem.IsWindows())
        {
            return Directory.GetFiles(CacheDir, "grSim.exe", SearchOption.AllDirectories).FirstOrDefault()
                ?? Directory.GetFiles(CacheDir, "grsim.exe", SearchOption.AllDirectories).FirstOrDefault();
        }

        // macOS / Linux tar.gz extracts a binary named grSim (no extension)
        return Directory.GetFiles(CacheDir, "grSim", SearchOption.AllDirectories).FirstOrDefault()
            ?? Directory.GetFiles(CacheDir, "grsim", SearchOption.AllDirectories).FirstOrDefault();
    }

    // ── external process detection ───────────────────────────────────────────

    /// <summary>
    /// Called every frame from the view. Re-scans for a running grSim process
    /// when we're not tracking one, throttled to once per second.
    /// </summary>
    public void Refresh()
    {
        if (_status is Status.Running or Status.Downloading) return;
        if (DateTime.UtcNow < _nextExternalScan) return;
        _nextExternalScan = DateTime.UtcNow.AddSeconds(1);
        AttachExternalProcess();
    }

    private void AttachExternalProcess()
    {
        // Process.GetProcessesByName is case-insensitive on Windows; on Linux we try both casings.
        var found = Process.GetProcessesByName("grSim").FirstOrDefault()
                 ?? Process.GetProcessesByName("grsim").FirstOrDefault();

        if (found == null) return;

        _process?.Dispose();
        _process = found;

        try
        {
            _process.EnableRaisingEvents = true;
            _process.Exited += (_, _) =>
            {
                if (_status == Status.Running)
                {
                    _statusMessage = "Process exited";
                    _status = Status.Exited;
                }
            };
        }
        catch
        {
            // Process may have already exited between GetProcessesByName and here
            _process.Dispose();
            _process = null;
            return;
        }

        _statusMessage = $"PID {_process.Id} (external)";
        _status = Status.Running;
    }

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
                "https://api.github.com/repos/Immortals-Robotics/grSim/releases/latest", ct);

            var release = JsonSerializer.Deserialize<GithubRelease>(json)
                          ?? throw new Exception("Failed to parse GitHub release response");

            // Asset names: grsim_{ver}_win32.zip  |  *_osx-universal.tar.gz  |  *_x86_64.tar.gz
            var asset = PickAsset(release.Assets)
                        ?? throw new Exception(
                            $"No release asset found for this platform. Available: " +
                            $"{string.Join(", ", release.Assets.Select(a => a.Name))}");

            static GithubAsset? PickAsset(GithubAsset[] assets)
            {
                if (OperatingSystem.IsWindows())
                    return assets.FirstOrDefault(a =>
                        a.Name.Contains("win", StringComparison.OrdinalIgnoreCase) &&
                        a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                if (OperatingSystem.IsMacOS())
                    return assets.FirstOrDefault(a =>
                        a.Name.Contains("osx", StringComparison.OrdinalIgnoreCase) &&
                        a.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));

                // Linux: *_x86_64.tar.gz — no "linux" keyword in the asset name
                return assets.FirstOrDefault(a =>
                    a.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) &&
                    !a.Name.Contains("osx", StringComparison.OrdinalIgnoreCase));
            }

            // Clear old cached files before downloading
            foreach (var old in Directory.GetFiles(CacheDir))
                File.Delete(old);
            foreach (var old in Directory.GetDirectories(CacheDir))
                Directory.Delete(old, recursive: true);

            var targetPath = Path.Combine(CacheDir, asset.Name);

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

            dst.Close();

            if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(targetPath, CacheDir, overwriteFiles: true);
                File.Delete(targetPath);
            }
            else if (asset.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                ExtractTarGz(targetPath, CacheDir);
                File.Delete(targetPath);
                if (!OperatingSystem.IsWindows())
                    MakeExecutable(CacheDir);
            }

            File.WriteAllText(Path.Combine(CacheDir, "version.txt"), release.TagName);

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

    public void Start()
    {
        if (_status == Status.Running) return;

        var exe = FindCachedExe();
        if (exe == null)
        {
            _statusMessage = "No binary cached — download first.";
            _status = Status.Error;
            return;
        }

        try
        {
            LaunchDetached(exe);
            _process?.Dispose();
            _process = null;

            _statusMessage = "Starting detached...";
            _status = Status.Idle;
            _nextExternalScan = DateTime.MinValue;
            AttachExternalProcess();

            if (_status != Status.Running)
            {
                _statusMessage = "Started detached. Waiting for grSim process...";
            }
        }
        catch (Exception ex)
        {
            _statusMessage = ex.Message;
            _status = Status.Error;
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

    private static void LaunchDetached(string exe)
    {
        var workingDirectory = Path.GetDirectoryName(exe)!;

        if (OperatingSystem.IsWindows())
        {
            using var launcher = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"\" \"{exe}\"",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            launcher?.WaitForExit();
            return;
        }

        static string QuoteForShell(string value) =>
            $"'{value.Replace("'", "'\\''")}'";

        var shellCommand = OperatingSystem.IsMacOS()
            ? $"nohup {QuoteForShell(exe)} >/dev/null 2>&1 &"
            : $"setsid {QuoteForShell(exe)} >/dev/null 2>&1 < /dev/null &";

        var unixLauncherInfo = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        unixLauncherInfo.ArgumentList.Add("-c");
        unixLauncherInfo.ArgumentList.Add(shellCommand);

        using var unixLauncher = Process.Start(unixLauncherInfo);

        unixLauncher?.WaitForExit();
    }

    private static void ExtractTarGz(string tarGzPath, string destDir)
    {
        using var fileStream  = File.OpenRead(tarGzPath);
        using var gzipStream  = new GZipStream(fileStream, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(gzipStream, destDir, overwriteFiles: true);
    }

    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private static void MakeExecutable(string dir)
    {
        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            try
            {
                var mode = File.GetUnixFileMode(file);
                if (mode.HasFlag(UnixFileMode.UserRead) && !mode.HasFlag(UnixFileMode.UserExecute))
                    File.SetUnixFileMode(file, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute);
            }
            catch { /* not a regular file */ }
        }
    }

    public void Dispose()
    {
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        // Detach without killing — grSim should outlive our process
        _process?.Dispose();
        _process = null;
    }
}

// ── GitHub API models ─────────────────────────────────────────────────────────

file sealed class GithubRelease
{
    [JsonPropertyName("tag_name")] public string        TagName { get; set; } = "";
    [JsonPropertyName("assets")]   public GithubAsset[] Assets  { get; set; } = [];
}

file sealed class GithubAsset
{
    [JsonPropertyName("name")]                 public string Name               { get; set; } = "";
    [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
}
