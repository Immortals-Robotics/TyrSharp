using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Gui.Backend;
using Tyr.Gui.GameController;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class GameControllerView : IDisposable
{
    // Rcon connection settings
    [ConfigEntry(StorageType.User)] private static string    Host { get; set; } = "localhost";
    [ConfigEntry(StorageType.User)] private static int       Port { get; set; } = 10011;
    [ConfigEntry(StorageType.User)] private static TeamColor Team { get; set; } = TeamColor.Yellow;

    // Managed process settings
    [ConfigEntry(StorageType.User)] private static int  GcRconPort { get; set; } = 10011;
    [ConfigEntry(StorageType.User)] private static int  GcUiPort   { get; set; } = 8081;

    private readonly GcRconClient _client  = new();
    private readonly GcProcess    _process = new();

    // Connection UI state (main thread only)
    private string _hostInput  = Host;
    private int    _portInput  = Port;
    private int    _teamIndex  = (int)Team - 1; // 0=Yellow, 1=Blue
    private int    _keeperInput;
    private bool   _keeperInputDirty;

    // Process UI state
    private int  _gcRconPortInput = GcRconPort;
    private int  _gcUiPortInput   = GcUiPort;
    private bool _autoConnect     = true;
    private bool _autoConnectPending;

    private static readonly string[] TeamNames = ["Yellow", "Blue"];

    public void Draw()
    {
        if (!ImGui.Begin($"{IconFonts.FontAwesome6.Flag} Game Controller"))
        {
            ImGui.End();
            return;
        }

        DrawProcessPanel();

        ImGui.Separator();

        DrawConnectionPanel();

        if (_client.State == GcRconClient.ConnectionState.Connected)
        {
            ImGui.Separator();
            DrawTeamState();
        }

        ImGui.End();

        // Auto-connect after the process has had time to start up
        HandleAutoConnect();
    }

    // ── managed process panel ─────────────────────────────────────────────────

    private void DrawProcessPanel()
    {
        ImGui.SeparatorText("Managed Process");

        var procStatus = _process.CurrentStatus;

        var (statusColor, statusLabel) = procStatus switch
        {
            GcProcess.Status.Running     => (Color.Green400,  $"Running  {_process.StatusMessage}"),
            GcProcess.Status.Downloading => (Color.Sky300,    $"Downloading…  {_process.DownloadProgress * 100:F0}%"),
            GcProcess.Status.Exited      => (Color.Orange400, $"Exited: {_process.StatusMessage}"),
            GcProcess.Status.Error       => (Color.Red400,    $"Error: {_process.StatusMessage}"),
            _                            => (Color.Zinc500,   _process.CachedVersion != null
                                                                ? $"Cached: {_process.CachedVersion}"
                                                                : "Not downloaded"),
        };

        ImGui.TextColored(statusColor, $"{IconFonts.FontAwesome6.Microchip}  {statusLabel}");

        // Download progress bar
        if (procStatus == GcProcess.Status.Downloading)
        {
            ImGui.ProgressBar(_process.DownloadProgress, new System.Numerics.Vector2(-1f, 0f));
        }

        ImGui.Spacing();

        // Port config (only editable when not running)
        var notRunning = procStatus != GcProcess.Status.Running;
        ImGui.BeginDisabled(!notRunning);

        ImGui.SetNextItemWidth(80f);
        if (ImGui.InputInt("RCon port##gcproc", ref _gcRconPortInput))
        {
            GcRconPort = _gcRconPortInput;
            Configurable.MarkChanged(StorageType.User);
        }

        ImGui.SameLine();

        ImGui.SetNextItemWidth(80f);
        if (ImGui.InputInt("UI port##gcproc", ref _gcUiPortInput))
        {
            GcUiPort = _gcUiPortInput;
            Configurable.MarkChanged(StorageType.User);
        }

        ImGui.SameLine();
        ImGui.Checkbox("Auto-connect##gcproc", ref _autoConnect);

        ImGui.EndDisabled();

        ImGui.Spacing();

        // Download button
        if (procStatus == GcProcess.Status.Downloading)
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.Xmark}  Cancel##gcdownload"))
                _process.CancelDownload();
        }
        else
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.CloudArrowDown}  Download Latest##gcdownload"))
                _process.StartDownload();

            ImGui.SameLine();

            // Start / Stop
            if (procStatus == GcProcess.Status.Running)
            {
                if (ImGui.Button($"{IconFonts.FontAwesome6.Stop}  Stop##gcproc"))
                    _process.Stop();
            }
            else
            {
                ImGui.BeginDisabled(_process.CachedVersion == null);
                if (ImGui.Button($"{IconFonts.FontAwesome6.Play}  Start##gcproc"))
                    StartProcess();
                ImGui.EndDisabled();
            }
        }
    }

    private void StartProcess()
    {
        _process.Start(GcRconPort, GcUiPort);
        if (_autoConnect && _process.CurrentStatus == GcProcess.Status.Running)
        {
            // Give the GC ~2 s to bind its port before we connect
            _autoConnectPending = true;
            _autoConnectDeadline = DateTime.UtcNow.AddSeconds(2);
        }
    }

    private DateTime _autoConnectDeadline;

    private void HandleAutoConnect()
    {
        if (!_autoConnectPending) return;
        if (DateTime.UtcNow < _autoConnectDeadline) return;

        _autoConnectPending = false;

        // Override host/port to the managed process values
        _hostInput = "localhost";
        _portInput = GcRconPort;
        Host = _hostInput;
        Port = _portInput;
        Configurable.MarkChanged(StorageType.User);

        _client.Connect("localhost", GcRconPort, Team);
    }

    // ── connection panel ──────────────────────────────────────────────────────

    private void DrawConnectionPanel()
    {
        ImGui.SeparatorText("Connection");

        var state = _client.State;
        var (statusColor, statusLabel) = state switch
        {
            GcRconClient.ConnectionState.Connected  => (Color.Green400,  "Connected"),
            GcRconClient.ConnectionState.Connecting => (Color.Yellow300, "Connecting…"),
            GcRconClient.ConnectionState.Error      => (Color.Red400,    $"Error: {_client.LastError}"),
            _                                       => (Color.Zinc500,   "Disconnected"),
        };

        ImGui.TextColored(statusColor, $"{IconFonts.FontAwesome6.Circle}  {statusLabel}");

        ImGui.Spacing();

        var disconnected = state is GcRconClient.ConnectionState.Disconnected
                                  or GcRconClient.ConnectionState.Error;

        ImGui.BeginDisabled(!disconnected);

        ImGui.SetNextItemWidth(180f);
        if (ImGui.InputText("Host##gcrcon", ref _hostInput, 128))
        {
            Host = _hostInput;
            Configurable.MarkChanged(StorageType.User);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);
        if (ImGui.InputInt("Port##gcrcon", ref _portInput))
        {
            Port = _portInput;
            Configurable.MarkChanged(StorageType.User);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(90f);
        if (ImGui.Combo("Team##gcrcon", ref _teamIndex, TeamNames, TeamNames.Length))
        {
            Team = (TeamColor)(_teamIndex + 1);
            Configurable.MarkChanged(StorageType.User);
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        if (disconnected)
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.Plug}  Connect##gcrcon"))
            {
                Host = _hostInput;
                Port = _portInput;
                Team = (TeamColor)(_teamIndex + 1);
                Configurable.MarkChanged(StorageType.User);
                _client.Connect(Host, Port, Team);
            }
        }
        else
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.PowerOff}  Disconnect##gcrcon"))
                _client.Disconnect();
        }
    }

    // ── team state panel ──────────────────────────────────────────────────────

    private void DrawTeamState()
    {
        var state = _client.TeamState;
        if (state == null)
        {
            ImGui.TextDisabled("Waiting for state…");
            return;
        }

        var teamColor = state.Team == TeamColor.Yellow ? Color.Yellow300 : Color.Sky300;
        ImGui.TextColored(teamColor, $"{state.Team} Team");

        ImGui.Spacing();

        if (ImGui.BeginTable("gc_state", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 2f);

            DrawStateRow("Keeper",     $"{state.KeeperId ?? 0}");
            DrawStateRow("Robots",     $"{state.RobotsOnField ?? 0} / {state.MaxRobots ?? 0}");
            DrawStateRow("Timeouts",   FormatTimeouts(state));
            DrawStateRow("Challenges", $"{state.ChallengeFlagsLeft ?? 0} left");
            DrawStateRow("Bot subs",   FormatBotSubs(state));

            if (state.YellowCardsDue.Count > 0)
                DrawStateRow("Yellow cards",
                    string.Join(", ", state.YellowCardsDue.Select(t => $"{t:F0}s")));

            if (state.EmergencyStopIn is > 0)
                DrawStateRow("Emergency stop in", $"{state.EmergencyStopIn:F1}s");

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.SeparatorText("Keeper ID");
        DrawKeeperControl(state);

        ImGui.Spacing();
        ImGui.SeparatorText("Actions");
        DrawActionButtons(state);
    }

    private static void DrawStateRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(label);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(value);
    }

    private void DrawKeeperControl(RemoteControlTeamState state)
    {
        if (!_keeperInputDirty)
            _keeperInput = state.KeeperId ?? 0;

        ImGui.SetNextItemWidth(80f);
        if (ImGui.InputInt("##keeper", ref _keeperInput))
            _keeperInputDirty = true;

        ImGui.SameLine();

        var canChangeKeeper = state.AvailableRequests.Contains(GcRequestType.ChangeKeeperId);
        ImGui.BeginDisabled(!canChangeKeeper || !_keeperInputDirty);

        if (ImGui.Button("Set##keeper"))
        {
            _client.Send(RemoteControlToController.SetKeeper(_keeperInput));
            _keeperInputDirty = false;
        }

        ImGui.EndDisabled();

        if (_keeperInputDirty)
        {
            ImGui.SameLine();
            ImGui.TextColored(Color.Yellow300, "(unsent)");
        }
    }

    private void DrawActionButtons(RemoteControlTeamState state)
    {
        var available = state.AvailableRequests;
        var active    = state.ActiveRequests;

        // Timeout toggle
        if (active.Contains(GcRequestType.Timeout))
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Color.Yellow700.RGBA);
            if (ImGui.Button($"{IconFonts.FontAwesome6.Clock}  Stop Timeout##gc"))
            {
                if (available.Contains(GcRequestType.StopTimeout))
                    _client.Send(RemoteControlToController.StopTimeout());
                else
                    _client.Send(RemoteControlToController.SetTimeout(false));
            }
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.BeginDisabled(!available.Contains(GcRequestType.Timeout));
            if (ImGui.Button($"{IconFonts.FontAwesome6.Clock}  Request Timeout##gc"))
                _client.Send(RemoteControlToController.SetTimeout(true));
            ImGui.EndDisabled();
        }

        ImGui.SameLine();

        // Robot substitution toggle
        if (active.Contains(GcRequestType.RobotSubstitution))
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Color.Blue700.RGBA);
            if (ImGui.Button($"{IconFonts.FontAwesome6.Robot}  Cancel Substitution##gc"))
                _client.Send(RemoteControlToController.SetRobotSubstitution(false));
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.BeginDisabled(!available.Contains(GcRequestType.RobotSubstitution));
            if (ImGui.Button($"{IconFonts.FontAwesome6.Robot}  Robot Substitution##gc"))
                _client.Send(RemoteControlToController.SetRobotSubstitution(true));
            ImGui.EndDisabled();
        }

        // Challenge flag (one-shot)
        ImGui.BeginDisabled(!available.Contains(GcRequestType.ChallengeFlag));
        if (ImGui.Button($"{IconFonts.FontAwesome6.Flag}  Challenge Flag##gc"))
            _client.Send(RemoteControlToController.ChallengeFlag());
        ImGui.EndDisabled();

        ImGui.SameLine();

        // Fail ball placement (one-shot)
        ImGui.BeginDisabled(!available.Contains(GcRequestType.FailBallPlacement));
        if (ImGui.Button($"{IconFonts.FontAwesome6.CircleXmark}  Fail Placement##gc"))
            _client.Send(RemoteControlToController.FailBallPlacement());
        ImGui.EndDisabled();

        ImGui.Spacing();
        DrawEmergencyStopButton(state);
    }

    private void DrawEmergencyStopButton(RemoteControlTeamState state)
    {
        var active    = state.ActiveRequests;
        var available = state.AvailableRequests;

        if (active.Contains(GcRequestType.EmergencyStop))
        {
            ImGui.PushStyleColor(ImGuiCol.Button,        Color.Red600.RGBA);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Color.Red500.RGBA);
            var label = state.EmergencyStopIn is > 0
                ? $"{IconFonts.FontAwesome6.CircleStop}  Cancel Emergency Stop  ({state.EmergencyStopIn:F1}s)##gc"
                : $"{IconFonts.FontAwesome6.CircleStop}  Cancel Emergency Stop##gc";
            if (ImGui.Button(label))
                _client.Send(RemoteControlToController.SetEmergencyStop(false));
            ImGui.PopStyleColor(2);
        }
        else
        {
            ImGui.BeginDisabled(!available.Contains(GcRequestType.EmergencyStop));
            ImGui.PushStyleColor(ImGuiCol.Button,        Color.Red800.RGBA);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Color.Red700.RGBA);
            if (ImGui.Button($"{IconFonts.FontAwesome6.CircleStop}  Emergency Stop##gc"))
                _client.Send(RemoteControlToController.SetEmergencyStop(true));
            ImGui.PopStyleColor(2);
            ImGui.EndDisabled();
        }
    }

    // ── formatting helpers ────────────────────────────────────────────────────

    private static string FormatTimeouts(RemoteControlTeamState state)
    {
        var left = state.TimeoutsLeft ?? 0;
        if (state.ActiveRequests.Contains(GcRequestType.Timeout) && state.TimeoutTimeLeft is > 0)
            return $"{left} left  ({state.TimeoutTimeLeft:F0}s remaining)";
        return $"{left} left";
    }

    private static string FormatBotSubs(RemoteControlTeamState state)
    {
        var left = state.BotSubstitutionsLeft ?? 0;
        if (state.BotSubstitutionTimeLeft is > 0)
            return $"{left} left  ({state.BotSubstitutionTimeLeft:F0}s)";
        return $"{left} left";
    }

    public void Dispose()
    {
        _client.Dispose();
        _process.Dispose();
    }
}
