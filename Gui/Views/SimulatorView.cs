using Hexa.NET.ImGui;
using Tyr.Gui.Backend;
using Tyr.Gui.Simulator;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Views;

public sealed class SimulatorView : IDisposable
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.Football} Simulator";

    private static readonly string DownloadLatestButtonLabel = $"{IconFonts.FontAwesome6.CloudArrowDown}  Download Latest##simdownload";
    private static readonly string CancelDownloadButtonLabel = $"{IconFonts.FontAwesome6.Xmark}  Cancel##simdownload";
    private static readonly string StopProcessButtonLabel    = $"{IconFonts.FontAwesome6.Stop}  Stop##simproc";
    private static readonly string StartProcessButtonLabel   = $"{IconFonts.FontAwesome6.Play}  Start##simproc";

    private readonly GrSimProcess _process = new();

    public void Draw()
    {
        _process.Refresh();

        if (!ImGui.Begin(WindowTitle))
        {
            ImGui.End();
            return;
        }

        DrawProcessPanel();

        ImGui.End();
    }

    private void DrawProcessPanel()
    {
        var procStatus = _process.CurrentStatus;

        var (statusColor, statusLabel) = procStatus switch
        {
            GrSimProcess.Status.Running     => (Color.Green400,  $"Running  |  {_process.StatusMessage}"),
            GrSimProcess.Status.Downloading => (Color.Sky300,    $"Downloading…  {_process.DownloadProgress * 100:F0}%"),
            GrSimProcess.Status.Exited      => (Color.Orange400, $"Exited: {_process.StatusMessage}"),
            GrSimProcess.Status.Error       => (Color.Red400,    $"Error: {_process.StatusMessage}"),
            _ => (Color.Zinc500, _process.CachedVersion != null
                                    ? $"Cached: {_process.CachedVersion}"
                                    : "Not downloaded"),
        };

        ImGui.TextColored(statusColor, IconFonts.FontAwesome6.Circle);
        ImGui.SameLine();
        ImGui.TextUnformatted(statusLabel);

        ImGui.SameLine();

        if (procStatus == GrSimProcess.Status.Downloading)
        {
            if (ImGui.Button(CancelDownloadButtonLabel))
                _process.CancelDownload();
            ImGui.ProgressBar(_process.DownloadProgress, new System.Numerics.Vector2(-1f, 0f));
        }
        else
        {
            if (ImGui.Button(DownloadLatestButtonLabel))
                _process.StartDownload();

            ImGui.SameLine();

            if (procStatus == GrSimProcess.Status.Running)
            {
                if (ImGui.Button(StopProcessButtonLabel))
                    _process.Stop();
            }
            else
            {
                ImGui.BeginDisabled(_process.CachedVersion == null);
                if (ImGui.Button(StartProcessButtonLabel))
                    _process.Start();
                ImGui.EndDisabled();
            }
        }

        ImGui.TextColored(Color.Zinc500, IconFonts.FontAwesome6.CircleInfo);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("grSim is a GUI application — it will open its own window.\nDownloads from github.com/Immortals-Robotics/grSim");
    }

    public void Dispose()
    {
        _process.Dispose();
    }
}
