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

    /// <param name="rconPort">Remote control TCP port (our client connects here).</param>
    /// <param name="uiPort">Web UI port (human browser).</param>
    public void Start(int rconPort, int uiPort)
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
            _process?.Dispose();
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
            _process.Exited += (_, _) =>
            {
                if (_status == Status.Running)
                {
                    _statusMessage = $"Process exited (code {_process.ExitCode})";
                    _status = Status.Exited;
                }
            };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _statusMessage = $"Running (PID {_process.Id})";
            _status = Status.Running;
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
