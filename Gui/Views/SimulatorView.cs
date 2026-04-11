using Hexa.NET.ImGui;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Simulation;
using Tyr.Common.Math;
using Tyr.Gui.Backend;
using Tyr.Gui.Simulator;
using Tyr.Vision;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Views;

public sealed class SimulatorView : IDisposable
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.Football} Simulator";

    private static readonly string DownloadLatestButtonLabel = $"{IconFonts.FontAwesome6.CloudArrowDown}  Download Latest##simdownload";
    private static readonly string CancelDownloadButtonLabel = $"{IconFonts.FontAwesome6.Xmark}  Cancel##simdownload";
    private static readonly string StopProcessButtonLabel    = $"{IconFonts.FontAwesome6.Stop}  Stop##simproc";
    private static readonly string StartProcessButtonLabel   = $"{IconFonts.FontAwesome6.Play}  Start##simproc";

    private static readonly string[] TeamNames = ["Yellow", "Blue"];

    private readonly GrSimProcess _process = new();
    public  readonly SimulatorChannel Channel = new();

    // ── ball state (mm) ───────────────────────────────────────────────────────
    private float _ballX, _ballY;
    private float _ballZ;
    private bool  _ballRolling;

    // ── robot state ───────────────────────────────────────────────────────────
    private int   _robotTeamIdx; // 0 = Yellow, 1 = Blue
    private int   _robotId;
    private float _robotX, _robotY; // mm
    private float _robotOrientDeg;

    // ── speed state ───────────────────────────────────────────────────────────
    private float _simSpeed = 1f;

    // ── Draw ──────────────────────────────────────────────────────────────────

    public void Draw()
    {
        _process.Refresh();
        Channel.SimulatorRunning = _process.CurrentStatus == GrSimProcess.Status.Running;

        if (!ImGui.Begin(WindowTitle))
        {
            ImGui.End();
            return;
        }

        ImGui.SeparatorText($"{IconFonts.FontAwesome6.Microchip}  Process");
        DrawProcessPanel();
        ImGui.SeparatorText($"{IconFonts.FontAwesome6.Crosshairs}  Control");
        DrawControlPanel();
        ImGui.SeparatorText($"{IconFonts.FontAwesome6.Sliders}  Config");
        DrawConfigPanel();

        ImGui.End();
    }

    // ── Process tab ───────────────────────────────────────────────────────────

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

    // ── Control tab ───────────────────────────────────────────────────────────

    private void DrawControlPanel()
    {
        // ── Ball ──────────────────────────────────────────────────────────────
        ImGui.SeparatorText("Ball");

        DrawMmInput("X##ballx", ref _ballX); ImGui.SameLine();
        DrawMmInput("Y##bally", ref _ballY); ImGui.SameLine();
        DrawMmInput("Z##ballz", ref _ballZ, minMm: 0f, maxMm: 2000f);

        ImGui.Checkbox("Rolling##ball", ref _ballRolling);
        ImGui.SameLine();

        if (ImGui.Button($"{IconFonts.FontAwesome6.LocationCrosshairs}  Teleport Ball##sim"))
            Channel.TeleportBall(new TeleportBall
            {
                X    = _ballX / 1000f,
                Y    = _ballY / 1000f,
                Z    = _ballZ / 1000f,
                Roll = _ballRolling,
            });

        // ── Robot ─────────────────────────────────────────────────────────────
        ImGui.SeparatorText("Robot");

        var teamColor = _robotTeamIdx == 0 ? Color.Yellow300 : Color.Sky300;
        ImGui.TextColored(teamColor, IconFonts.FontAwesome6.Robot); ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);
        ImGui.Combo("##robotteam", ref _robotTeamIdx, TeamNames, TeamNames.Length); ImGui.SameLine();
        ImGui.Text("ID"); ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);
        ImGui.DragInt("##robotid", ref _robotId, 1f, 0, 15);

        DrawMmInput("X##robotx", ref _robotX); ImGui.SameLine();
        DrawMmInput("Y##roboty", ref _robotY); ImGui.SameLine();
        ImGui.Text("Angle"); ImGui.SameLine();
        ImGui.SetNextItemWidth(75f);
        ImGui.DragFloat("##robotangle", ref _robotOrientDeg, 1f, -180f, 180f, "%.0f°");

        var team = _robotTeamIdx == 0 ? TeamColor.Yellow : TeamColor.Blue;

        if (ImGui.Button($"{IconFonts.FontAwesome6.LocationCrosshairs}  Teleport##robot"))
            Channel.TeleportRobot(team, (uint)_robotId,
                                  _robotX / 1000f, _robotY / 1000f,
                                  Angle.FromDeg(_robotOrientDeg));

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, Color.Red900.RGBA);
        if (ImGui.Button($"{IconFonts.FontAwesome6.CircleXmark}  Remove##robot"))
            Channel.RemoveRobot(team, (uint)_robotId);
        ImGui.PopStyleColor();

        // ── Simulation speed ──────────────────────────────────────────────────
        ImGui.SeparatorText("Simulation Speed");

        ImGui.Text("Speed"); ImGui.SameLine();
        ImGui.SetNextItemWidth(100f);
        ImGui.DragFloat("##simspeed", ref _simSpeed, 0.05f, 0f, 10f, "%.2fx");
        ImGui.SameLine();
        if (ImGui.Button($"{IconFonts.FontAwesome6.Play}  Set##simspeed"))
            Channel.SetSimulationSpeed(_simSpeed);
        ImGui.SameLine();
        if (ImGui.Button($"{IconFonts.FontAwesome6.Pause}  Pause##simspeed"))
            Channel.SetSimulationSpeed(0f);
    }

    // ── Config tab ────────────────────────────────────────────────────────────

    private void DrawConfigPanel()
    {
        ImGui.SeparatorText("Vision");

        var visionPort = SslVisionDataPublisher.SimulatorVisionPort;
        ImGui.TextColored(Color.Zinc400, $"Simulator vision port: {visionPort}");
        ImGui.SameLine();
        if (ImGui.Button($"{IconFonts.FontAwesome6.PaperPlane}  Send##simvision"))
            Channel.SendConfig(visionPort: (uint)visionPort);

        ImGui.Spacing();
        ImGui.SeparatorText("Robot Specs");

        ImGui.TextColored(Color.Zinc500,
            "Edit robot geometry, limits, and wheel angles\n" +
            "in the Configs panel (SimulatorChannel section),\n" +
            "then send them to grSim with the button below.");
        ImGui.Spacing();

        if (ImGui.Button($"{IconFonts.FontAwesome6.PaperPlane}  Send Robot Specs##simspecs"))
            Channel.SendRobotSpecs();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// Renders "Label [drag input] mm" with the label on the left.
    private static void DrawMmInput(string id, ref float valueMm,
                                    float minMm = -10000f, float maxMm = 10000f)
    {
        // Extract the display label (everything before ##)
        var label = id.Contains("##") ? id[..id.IndexOf('#')] : id;
        ImGui.Text(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(75f);
        ImGui.DragFloat($"##{id}", ref valueMm, 10f, minMm, maxMm, "%.0f");
        ImGui.SameLine();
        ImGui.TextColored(Color.Zinc500, "mm");
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        Channel.Dispose();
        _process.Dispose();
    }
}
