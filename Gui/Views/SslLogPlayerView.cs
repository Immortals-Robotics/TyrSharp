using Hexa.NET.ImGui;
using NativeFileDialogSharp;
using Tyr.Vision;
using Tyr.Common.Config;
using Tyr.Gui.Backend;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class SslLogPlayerView(SslLogPlayer player)
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.Film} SSL Log Player";
    
    [ConfigEntry("Whether the SSL Log Player window is visible", StorageType.User)] 
    private static bool IsOpen { get; set; } = true;

    private static string FormatTime(double seconds)
    {
        var totalSeconds = (int)seconds;
        var minutes = totalSeconds / 60;
        var remainingSeconds = totalSeconds % 60;
        var milliseconds = (int)((seconds - totalSeconds) * 1000);
        return $"{minutes:D2}:{remainingSeconds:D2}.{milliseconds:D3}";
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
        // Calculate width: leave space for Speed (80), Open (35), and Close (35) + margins (~20)
        ImGui.SetNextItemWidth(Math.Max(100f, ImGui.GetContentRegionAvail().X - 170f));
        
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
        if (ImGui.Button($"{IconFonts.FontAwesome6.FolderOpen}"))
        {
            var result = Dialog.FileOpen("gz,log.gz;log,sslmsg");
            if (result.IsOk)
            {
                SslLogPlayer.FilePath = result.Path;
            }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(string.IsNullOrEmpty(SslLogPlayer.FilePath) ? "Open SSL Log..." : $"Current: {SslLogPlayer.FilePath}");

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
        
        ImGui.End();
    }
}
