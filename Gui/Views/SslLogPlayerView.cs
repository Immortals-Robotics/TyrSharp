using Hexa.NET.ImGui;
using Tyr.Vision;
using Tyr.Common.Config;
using Tyr.Gui.Backend;
using Tyr.Common.Network;
using Tyr.Gui.Platform;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class SslLogPlayerView(SslLogPlayer player) : IDisposable
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.Film} SSL Log Player";
    
    [ConfigEntry("Whether the SSL Log Player window is visible", StorageType.User)] 
    private static bool IsOpen { get; set; } = true;

    private static readonly string CacheDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tyr", "logs");

    private readonly SeafileClient _seafile = new();
    private List<SeafileDirent>? _dirents;
    private string _currentPath = "/gamelogs/";
    private bool _isFetching;
    private bool _showRemoteBrowser;
    private (string Name, float Progress)? _downloading;
    private string? _fetchError;

    private static string FormatTime(double seconds)
    {
        var totalSeconds = (int)seconds;
        var minutes = totalSeconds / 60;
        var remainingSeconds = totalSeconds % 60;
        var milliseconds = (int)((seconds - totalSeconds) * 1000);
        return $"{minutes:D2}:{remainingSeconds:D2}.{milliseconds:D3}";
    }

    private async void FetchRemoteLogs(string path)
    {
        _isFetching = true;
        _fetchError = null;
        try
        {
            _dirents = await _seafile.ListDirentsAsync(path);
            _currentPath = path;
        }
        catch (Exception ex)
        {
            _fetchError = ex.Message;
            _dirents = null;
        }
        finally
        {
            _isFetching = false;
        }
    }

    private async void DownloadAndOpen(SeafileDirent dirent)
    {
        var remotePath = Path.Combine(_currentPath, dirent.EffectiveName).Replace('\\', '/');
        var localPath = Path.Combine(CacheDir, dirent.EffectiveName);

        if (File.Exists(localPath))
        {
            SslLogPlayer.FilePath = localPath;
            _showRemoteBrowser = false;
            return;
        }

        _downloading = (dirent.EffectiveName, 0f);
        try
        {
            var progress = new Progress<float>(p => _downloading = (dirent.EffectiveName, p));
            await _seafile.DownloadFileAsync(remotePath, localPath, progress);
            SslLogPlayer.FilePath = localPath;
            _showRemoteBrowser = false;
        }
        catch (Exception ex)
        {
            _fetchError = $"Download failed: {ex.Message}";
        }
        finally
        {
            _downloading = null;
        }
    }

    public void Draw()
    {
        if (!IsOpen) return;

        if (!ImGui.Begin(WindowTitle, ImGuiWindowFlags.NoResize))
        {
            ImGui.End();
            return;
        }

        var fileLoaded = player.DurationSeconds > 0;

        // Left: Playback controls
        if (ImGui.Button($"{IconFonts.FontAwesome6.RotateLeft}"))
        {
            SslLogPlayer.CurrentTimeSeconds = 0;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset to beginning");

        ImGui.SameLine();
        ImGui.BeginDisabled(!fileLoaded);
        var isPlaying = SslLogPlayer.IsPlaying;
        if (ImGui.Button(isPlaying ? $"{IconFonts.FontAwesome6.Pause}" : $"{IconFonts.FontAwesome6.Play}"))
        {
            SslLogPlayer.IsPlaying = !isPlaying;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(isPlaying ? "Pause" : "Play");
        ImGui.EndDisabled();

        // Middle: Slider
        ImGui.SameLine();
        ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
        // Calculate width: leave space for Speed (60), Remote (35), Open (35), and Close (35) + margins (~25)
        ImGui.SetNextItemWidth(Math.Max(100f, ImGui.GetContentRegionAvail().X - 200f));
        
        var currentTime = (float)SslLogPlayer.CurrentTimeSeconds;
        var duration = (float)player.DurationSeconds;
        string timeLabel = fileLoaded ? $"{FormatTime(currentTime)} / {FormatTime(duration)}" : "No file loaded";
        
        ImGui.BeginDisabled(!fileLoaded);
        if (ImGui.SliderFloat("##time", ref currentTime, 0, Math.Max(0.001f, duration), timeLabel))
        {
            SslLogPlayer.CurrentTimeSeconds = currentTime;
        }
        ImGui.EndDisabled();
        ImGui.PopFont();

        // Right: Modifiers
        ImGui.SameLine();
        ImGui.SetNextItemWidth(60f);
        var speed = (float)SslLogPlayer.PlaybackSpeed;
        if (ImGui.DragFloat("##speed", ref speed, 0.1f, 0.1f, 10f, "%.1fx"))
        {
            SslLogPlayer.PlaybackSpeed = speed;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Playback Speed");

        ImGui.SameLine();
        if (ImGui.Button($"{IconFonts.FontAwesome6.CloudArrowDown}"))
        {
            ImGui.OpenPopup("Seafile Log Browser");
            _showRemoteBrowser = true;
            if (_dirents == null) FetchRemoteLogs(_currentPath);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Download logs from Seafile...");

        ImGui.SameLine();
        if (ImGui.Button($"{IconFonts.FontAwesome6.FolderOpen}"))
        {
            var result = FileDialogService.FileOpen("gz,log.gz;log,sslmsg");
            if (result.IsOk)
            {
                SslLogPlayer.FilePath = result.Path;
            }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(string.IsNullOrEmpty(SslLogPlayer.FilePath) ? "Open Local SSL Log..." : $"Current: {SslLogPlayer.FilePath}");

        if (fileLoaded)
        {
            ImGui.SameLine();
            if (ImGui.Button($"{IconFonts.FontAwesome6.Xmark}"))
            {
                SslLogPlayer.FilePath = "";
                player.Close();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Close log");
        }
        
        DrawRemoteBrowser();

        ImGui.End();
    }

    private void DrawRemoteBrowser()
    {
        if (!_showRemoteBrowser) return;

        var viewport = ImGui.GetMainViewport();
        var center = viewport.Pos + viewport.Size * 0.5f;
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new System.Numerics.Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(800, 500), ImGuiCond.FirstUseEver);

        if (ImGui.BeginPopupModal("Seafile Log Browser", ref _showRemoteBrowser, ImGuiWindowFlags.None))
        {
            // --- Navigation Bar ---
            if (ImGui.Button($"{IconFonts.FontAwesome6.Rotate}")) FetchRemoteLogs(_currentPath);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Refresh current folder");

            ImGui.SameLine();
            var isRoot = _currentPath == "/gamelogs/";
            ImGui.BeginDisabled(isRoot);
            if (ImGui.Button($"{IconFonts.FontAwesome6.ArrowUp}"))
            {
                var parent = _currentPath.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parent.Length > 1)
                {
                    FetchRemoteLogs("/" + string.Join('/', parent.SkipLast(1)) + "/");
                }
                else
                {
                    FetchRemoteLogs("/");
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Go to parent folder");
            ImGui.EndDisabled();

            ImGui.SameLine();
            DrawBreadcrumbs();
            
            ImGui.Separator();

            // --- Status & Error Messages ---
            if (_isFetching)
            {
                ImGui.TextColored(new System.Numerics.Vector4(0.4f, 0.7f, 1.0f, 1.0f), $"{IconFonts.FontAwesome6.Spinner} Fetching file list...");
            }
            else if (_fetchError != null)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.4f, 0.4f, 1), $"{IconFonts.FontAwesome6.TriangleExclamation} {_fetchError}");
            }
            else if (_dirents != null)
            {
                ImGui.BeginDisabled(_downloading.HasValue);
                const ImGuiTableFlags tableFlags = ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY;
                
                if (ImGui.BeginTable("##dirents_table", 4, tableFlags, new System.Numerics.Vector2(0, -35)))
                {
                    ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 25f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0.7f);
                    ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed, 80f);
                    ImGui.TableSetupColumn("Modified", ImGuiTableColumnFlags.WidthFixed, 150f);
                    ImGui.TableHeadersRow();

                    foreach (var dirent in _dirents.OrderBy(d => !d.IsDir).ThenByDescending(d => d.EffectiveName))
                    {
                        var name = dirent.EffectiveName;
                        ImGui.PushID(dirent.UniqueId);
                        
                        ImGui.TableNextRow();
                        
                        // Icon Column
                        ImGui.TableNextColumn();
                        var isDownloading = _downloading.HasValue && _downloading.Value.Name == name;
                        if (isDownloading)
                        {
                            ImGui.TextColored(new System.Numerics.Vector4(0.4f, 0.7f, 1.0f, 1.0f), IconFonts.FontAwesome6.Spinner);
                        }
                        else if (dirent.IsDir)
                        {
                            ImGui.TextColored(new System.Numerics.Vector4(0.9f, 0.8f, 0.4f, 1), IconFonts.FontAwesome6.Folder);
                        }
                        else
                        {
                            var localPath = Path.Combine(CacheDir, name);
                            var isCached = File.Exists(localPath);
                            if (isCached)
                                ImGui.TextColored(new System.Numerics.Vector4(0.4f, 0.9f, 0.4f, 1), IconFonts.FontAwesome6.File);
                            else
                                ImGui.TextColored(new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1), IconFonts.FontAwesome6.Download);
                        }

                        // Name Column
                        ImGui.TableNextColumn();
                        if (ImGui.Selectable(name, false, ImGuiSelectableFlags.SpanAllColumns))
                        {
                            if (dirent.IsDir)
                            {
                                FetchRemoteLogs(Path.Combine(_currentPath, name).Replace('\\', '/') + "/");
                            }
                            else
                            {
                                DownloadAndOpen(dirent);
                            }
                        }

                        // Size Column
                        ImGui.TableNextColumn();
                        if (isDownloading)
                        {
                            var progress = _downloading!.Value.Progress;
                            ImGui.ProgressBar(progress, new System.Numerics.Vector2(-1, 0), $"{progress:P0}");
                        }
                        else if (!dirent.IsDir)
                        {
                            ImGui.TextDisabled($"{(dirent.Size / 1024.0 / 1024.0):F1} MB");
                        }

                        // Modified Column
                        ImGui.TableNextColumn();
                        ImGui.TextDisabled(dirent.LastModified ?? "");

                        ImGui.PopID();
                    }
                    ImGui.EndTable();
                }
                ImGui.EndDisabled();
            }

            // --- Footer Area ---
            ImGui.Separator();
            
            if (!_showRemoteBrowser)
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void DrawBreadcrumbs()
    {
        var parts = _currentPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        string cumulativePath = "/";

        foreach (var part in parts)
        {
            cumulativePath += part + "/";
            if (ImGui.SmallButton(part))
            {
                FetchRemoteLogs(cumulativePath);
            }
            
            ImGui.SameLine(0, 2);
            ImGui.TextDisabled("/");
            ImGui.SameLine(0, 2);
        }
    }

    public void Dispose()
    {
        _seafile.Dispose();
    }
}
