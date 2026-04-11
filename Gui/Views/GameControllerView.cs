using System.Globalization;
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
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.Flag} Game Controller";
    private static readonly string ProcessTabTitle = $"{IconFonts.FontAwesome6.Microchip}  Process";
    private static readonly string RefereeTabTitle = $"{IconFonts.FontAwesome6.Gavel}  Referee";
    private static readonly string YellowTabTitle = $"{IconFonts.FontAwesome6.Robot}  Yellow";
    private static readonly string BlueTabTitle = $"{IconFonts.FontAwesome6.Robot}  Blue";
    private static readonly string DownloadLatestButtonLabel = $"{IconFonts.FontAwesome6.CloudArrowDown}  Download Latest##gcdownload";
    private static readonly string CancelDownloadButtonLabel = $"{IconFonts.FontAwesome6.Xmark}  Cancel##gcdownload";
    private static readonly string StopProcessButtonLabel = $"{IconFonts.FontAwesome6.Stop}  Stop##gcproc";
    private static readonly string StartProcessButtonLabel = $"{IconFonts.FontAwesome6.Play}  Start##gcproc";

    [ConfigEntry("Hostname or IP address where the game controller is running", StorageType.User)]
    private static string GameControllerHost { get; set; } = "localhost";

    [ConfigEntry("TCP port for the team remote control protocol", StorageType.User)]
    private static int RemoteControlPort { get; set; } = 10011;

    [ConfigEntry("HTTP port for the web UI and referee WebSocket API", StorageType.User)]
    private static int WebUiPort { get; set; } = 8081;

    [ConfigEntry("Automatically connect referee API and team rcon after starting the managed process", StorageType.User)]
    private static bool AutoConnect { get; set; } = true;

    private readonly GcProcess    _process    = new();
    private readonly GcApiClient  _apiClient  = new();
    private readonly GcRconClient _rconYellow = new();
    private readonly GcRconClient _rconBlue   = new();

    private bool _autoConnectPending;
    private DateTime _autoConnectDeadline;

    private int  _keeperInputYellow;
    private bool _keeperDirtyYellow;
    private int  _keeperInputBlue;
    private bool _keeperDirtyBlue;

    public void Draw()
    {
        if (!ImGui.Begin(WindowTitle))
        {
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("gc_tabs"))
        {
            if (ImGui.BeginTabItem(ProcessTabTitle))
            {
                DrawProcessPanel();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(RefereeTabTitle))
            {
                DrawRefereePanel();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(YellowTabTitle))
            {
                DrawTeamPanel(TeamColor.Yellow, _rconYellow, ref _keeperInputYellow, ref _keeperDirtyYellow);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(BlueTabTitle))
            {
                DrawTeamPanel(TeamColor.Blue, _rconBlue, ref _keeperInputBlue, ref _keeperDirtyBlue);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();

        HandleAutoConnect();
    }

    // ── Process tab ───────────────────────────────────────────────────────────

    private void DrawProcessPanel()
    {
        var procStatus = _process.CurrentStatus;

        var (statusColor, statusLabel) = procStatus switch
        {
            GcProcess.Status.Running     => (Color.Green400,  $"Running  |  {_process.StatusMessage}"),
            GcProcess.Status.Downloading => (Color.Sky300,    $"Downloading…  {_process.DownloadProgress * 100:F0}%"),
            GcProcess.Status.Exited      => (Color.Orange400, $"Exited: {_process.StatusMessage}"),
            GcProcess.Status.Error       => (Color.Red400,    $"Error: {_process.StatusMessage}"),
            _ => (Color.Zinc500, _process.CachedVersion != null
                                    ? $"Cached: {_process.CachedVersion}"
                                    : "Not downloaded"),
        };

        ImGui.TextColored(statusColor, IconFonts.FontAwesome6.Circle);
        ImGui.SameLine();
        ImGui.TextUnformatted(statusLabel);

        ImGui.SameLine();

        if (procStatus == GcProcess.Status.Downloading)
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

            if (procStatus == GcProcess.Status.Running)
            {
                if (ImGui.Button(StopProcessButtonLabel))
                {
                    _process.Stop();
                    _apiClient.Disconnect();
                }
            }
            else
            {
                ImGui.BeginDisabled(_process.CachedVersion == null);
                if (ImGui.Button(StartProcessButtonLabel))
                    StartProcess();
                ImGui.EndDisabled();
            }
        }

        ImGui.TextColored(Color.Zinc500, IconFonts.FontAwesome6.CircleInfo);
    }

    private void StartProcess()
    {
        _process.Start(RemoteControlPort, WebUiPort);
        if (AutoConnect && _process.CurrentStatus == GcProcess.Status.Running)
        {
            _autoConnectPending  = true;
            _autoConnectDeadline = DateTime.UtcNow.AddSeconds(2);
        }
    }

    private void HandleAutoConnect()
    {
        if (!_autoConnectPending || DateTime.UtcNow < _autoConnectDeadline) return;
        _autoConnectPending = false;

        _apiClient.Connect("localhost", WebUiPort);
        _rconYellow.Connect("localhost", RemoteControlPort, TeamColor.Yellow);
        _rconBlue.Connect("localhost", RemoteControlPort, TeamColor.Blue);
    }

    // ── Referee tab ───────────────────────────────────────────────────────────

    private void DrawRefereePanel()
    {
        var apiState = _apiClient.State;
        DrawApiConnectionBar(apiState);

        if (apiState != GcApiClient.ConnectionState.Connected)
            return;

        ImGui.Spacing();

        var ms  = _apiClient.MatchState;
        var gcs = _apiClient.GcState;

        if (ms != null)
        {
            DrawMatchHeader(ms);
            ImGui.Spacing();
        }

        ImGui.SeparatorText("Commands");
        DrawNeutralCommandButtons();
        ImGui.Spacing();
        DrawTeamCommandButtons("Yellow");
        DrawTeamCommandButtons("Blue");

        if (gcs?.ContinueActions is { Length: > 0 })
        {
            ImGui.Spacing();
            ImGui.SeparatorText("Continue");
            DrawContinueActions(gcs.ContinueActions);
        }

        if (gcs?.ContinueHints is { Length: > 0 })
        {
            ImGui.Spacing();
            foreach (var hint in gcs.ContinueHints)
                ImGui.TextColored(Color.Zinc400, hint.Message ?? "");
        }

        ImGui.Spacing();
        ImGui.SeparatorText("Stage");
        DrawStageControls(ms);
    }

    private void DrawApiConnectionBar(GcApiClient.ConnectionState state)
    {
        var (color, label) = state switch
        {
            GcApiClient.ConnectionState.Connected  => (Color.Green400,  "Connected"),
            GcApiClient.ConnectionState.Connecting => (Color.Yellow300, "Connecting…"),
            GcApiClient.ConnectionState.Error      => (Color.Red400,    $"Error: {_apiClient.LastError}"),
            _                                      => (Color.Zinc500,   "Disconnected"),
        };

        ImGui.TextColored(color, $"{IconFonts.FontAwesome6.Circle}  {label}");

        ImGui.SameLine();

        var disconnected = state is GcApiClient.ConnectionState.Disconnected
                                   or GcApiClient.ConnectionState.Error;
        if (disconnected)
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.Plug}  Connect##apiconn"))
                _apiClient.Connect(GameControllerHost, WebUiPort);
        }
        else
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.PowerOff}  Disconnect##apiconn"))
                _apiClient.Disconnect();
        }

        ImGui.SameLine();
        ImGui.TextColored(Color.Zinc500, $"{GameControllerHost}:{WebUiPort}");
    }

    private void DrawMatchHeader(GcMatchState ms)
    {
        // Stage + time
        var stage     = FormatStage(ms.Stage);
        var timeLeft  = ParseDuration(ms.StageTimeLeft);
        var timeLabel = timeLeft.TotalSeconds > 0
            ? $"{(int)timeLeft.TotalMinutes:D2}:{timeLeft.Seconds:D2}"
            : "";

        ImGui.TextColored(Color.Zinc300, timeLabel.Length > 0
            ? $"{stage}  |  {timeLabel} left"
            : stage);

        // Scoreboard
        var yellowInfo = GetTeam(ms, "YELLOW");
        var blueInfo   = GetTeam(ms, "BLUE");

        var yellowName = yellowInfo?.Name ?? "Yellow";
        var blueName   = blueInfo?.Name   ?? "Blue";
        var yellowGoals = yellowInfo?.Goals ?? 0;
        var blueGoals   = blueInfo?.Goals  ?? 0;

        ImGui.TextColored(Color.Yellow300, yellowName);
        ImGui.SameLine();
        ImGui.Text($"  {yellowGoals}  |  {blueGoals}  ");
        ImGui.SameLine();
        ImGui.TextColored(Color.Sky300, blueName);

        // Current game state
        var gameType = ms.GameState?.Type ?? ms.Command?.Type ?? "";
        var forTeam  = ms.GameState?.ForTeam ?? ms.Command?.ForTeam ?? "";
        var stateColor = gameType switch
        {
            "HALT"  => Color.Red400,
            "STOP"  => Color.Orange400,
            "RUNNING" or "FORCE_START" or "NORMAL_START" => Color.Green400,
            _ => Color.Sky300,
        };
        var stateLabel = string.IsNullOrEmpty(forTeam) || forTeam == "UNKNOWN"
            ? gameType
            : $"{gameType}  ▶  {forTeam}";

        ImGui.TextColored(stateColor, stateLabel);

        if (ms.NextCommand != null &&
            ms.NextCommand.Type != ms.Command?.Type)
        {
            ImGui.SameLine(0, 20f);
            ImGui.TextColored(Color.Zinc400,
                $"next: {ms.NextCommand.Type}");
        }

        if (ms.ActionTimeRemaining is { Length: > 0 })
        {
            var remaining = ParseDuration(ms.ActionTimeRemaining);
            if (remaining.TotalSeconds > 0)
                ImGui.TextColored(Color.Yellow300, $"  {remaining.TotalSeconds:F1}s remaining");
        }

        if (!string.IsNullOrWhiteSpace(ms.StatusMessage))
            ImGui.TextColored(Color.Zinc400, ms.StatusMessage);
    }

    private void DrawNeutralCommandButtons()
    {
        if (ImGui.Button($"{IconFonts.FontAwesome6.Hand}  HALT##ref"))
            _apiClient.Send(GcInput.Command("HALT"));

        ImGui.SameLine();

        if (ImGui.Button($"{IconFonts.FontAwesome6.CircleStop}  STOP##ref"))
            _apiClient.Send(GcInput.Command("STOP"));

        ImGui.SameLine();

        if (ImGui.Button($"{IconFonts.FontAwesome6.Forward}  Force Start##ref"))
            _apiClient.Send(GcInput.Command("FORCE_START"));

        ImGui.SameLine();

        if (ImGui.Button($"{IconFonts.FontAwesome6.Play}  Normal Start##ref"))
            _apiClient.Send(GcInput.Command("NORMAL_START"));
    }

    private void DrawTeamCommandButtons(string team)
    {
        var color = team == "Yellow" ? Color.Yellow300 : Color.Sky300;
        ImGui.TextColored(color, $"{team}:");
        ImGui.SameLine();

        if (ImGui.Button($"Kickoff##{team}"))
            _apiClient.Send(GcInput.Command("KICKOFF", team.ToUpperInvariant()));
        ImGui.SameLine();

        if (ImGui.Button($"Free Kick##{team}"))
            _apiClient.Send(GcInput.Command("DIRECT", team.ToUpperInvariant()));
        ImGui.SameLine();

        if (ImGui.Button($"Penalty##{team}"))
            _apiClient.Send(GcInput.Command("PENALTY", team.ToUpperInvariant()));
        ImGui.SameLine();

        if (ImGui.Button($"Ball Placement##{team}"))
            _apiClient.Send(GcInput.Command("BALL_PLACEMENT", team.ToUpperInvariant()));
        ImGui.SameLine();

        if (ImGui.Button($"Timeout##{team}"))
            _apiClient.Send(GcInput.Command("TIMEOUT", team.ToUpperInvariant()));
    }

    private void DrawContinueActions(GcContinueAction[] actions)
    {
        foreach (var action in actions)
        {
            var ready  = action.State is "READY_MANUAL" or "READY_AUTO";
            var blocked = action.State == "BLOCKED";

            var label = string.IsNullOrEmpty(action.ForTeam) || action.ForTeam == "UNKNOWN"
                ? action.Type ?? ""
                : $"{action.Type}  ▶  {action.ForTeam}";

            ImGui.BeginDisabled(!ready);

            if (ready)
                ImGui.PushStyleColor(ImGuiCol.Button, Color.Green800.RGBA);

            if (ImGui.Button($"{label}##cont"))
                _apiClient.Send(GcInput.Continue(action.Type ?? "", action.ForTeam ?? "UNKNOWN"));

            if (ready)
                ImGui.PopStyleColor();

            ImGui.EndDisabled();

            if (blocked && action.ContinuationIssues is { Length: > 0 })
            {
                ImGui.SameLine();
                ImGui.TextColored(Color.Zinc400, string.Join(", ", action.ContinuationIssues));
            }
        }
    }

    private void DrawStageControls(GcMatchState? ms)
    {
        if (ImGui.Button("Next Stage##ref"))
            _apiClient.Send(GcInput.Continue("NEXT_STAGE"));

        ImGui.SameLine();

        if (ImGui.Button("End Game##ref"))
            _apiClient.Send(GcInput.Continue("END_GAME"));

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, Color.Red900.RGBA);
        if (ImGui.Button($"{IconFonts.FontAwesome6.ArrowRotateLeft}  Reset Match##ref"))
            _apiClient.Send(GcInput.Reset());
        ImGui.PopStyleColor();
    }

    // ── Team tab ──────────────────────────────────────────────────────────────

    private void DrawTeamPanel(TeamColor team, GcRconClient client,
                               ref int keeperInput, ref bool keeperDirty)
    {
        var suffix = team.ToString();
        var state  = client.State;

        var (statusColor, statusLabel) = state switch
        {
            GcRconClient.ConnectionState.Connected  => (Color.Green400,  "Connected"),
            GcRconClient.ConnectionState.Connecting => (Color.Yellow300, "Connecting…"),
            GcRconClient.ConnectionState.Error      => (Color.Red400,    $"Error: {client.LastError}"),
            _                                       => (Color.Zinc500,   "Disconnected"),
        };

        ImGui.TextColored(statusColor, $"{IconFonts.FontAwesome6.Circle}  {statusLabel}");

        ImGui.Spacing();

        var disconnected = state is GcRconClient.ConnectionState.Disconnected
                                   or GcRconClient.ConnectionState.Error;

        if (disconnected)
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.Plug}  Connect##{suffix}"))
                client.Connect(GameControllerHost, RemoteControlPort, team);
        }
        else
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.PowerOff}  Disconnect##{suffix}"))
                client.Disconnect();
        }

        ImGui.SameLine();
        ImGui.TextColored(Color.Zinc500, $"{GameControllerHost}:{RemoteControlPort}");

        if (state == GcRconClient.ConnectionState.Connected)
        {
            ImGui.Separator();
            DrawTeamState(client, ref keeperInput, ref keeperDirty);
        }
    }

    private void DrawTeamState(GcRconClient client, ref int keeperInput, ref bool keeperDirty)
    {
        var state = client.TeamState;
        if (state == null)
        {
            ImGui.TextDisabled("Waiting for state…");
            return;
        }

        var teamColor = state.Team == TeamColor.Yellow ? Color.Yellow300 : Color.Sky300;
        ImGui.TextColored(teamColor, $"{state.Team} Team");

        ImGui.Spacing();

        if (ImGui.BeginTable("gc_team_state", 2, ImGuiTableFlags.SizingStretchProp))
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
        DrawKeeperControl(client, state, ref keeperInput, ref keeperDirty);

        ImGui.Spacing();
        ImGui.SeparatorText("Actions");
        DrawActionButtons(client, state);
    }

    private static void DrawStateRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(label);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(value);
    }

    private static void DrawKeeperControl(GcRconClient client, RemoteControlTeamState state,
                                          ref int keeperInput, ref bool keeperDirty)
    {
        if (!keeperDirty)
            keeperInput = state.KeeperId ?? 0;

        ImGui.SetNextItemWidth(80f);
        if (ImGui.InputInt("##keeper", ref keeperInput))
            keeperDirty = true;

        ImGui.SameLine();

        var canChangeKeeper = state.AvailableRequests.Contains(GcRequestType.ChangeKeeperId);
        ImGui.BeginDisabled(!canChangeKeeper || !keeperDirty);

        if (ImGui.Button("Set##keeper"))
        {
            client.Send(RemoteControlToController.SetKeeper(keeperInput));
            keeperDirty = false;
        }

        ImGui.EndDisabled();

        if (keeperDirty)
        {
            ImGui.SameLine();
            ImGui.TextColored(Color.Yellow300, "(unsent)");
        }
    }

    private static void DrawActionButtons(GcRconClient client, RemoteControlTeamState state)
    {
        var available = state.AvailableRequests;
        var active    = state.ActiveRequests;

        if (active.Contains(GcRequestType.Timeout))
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Color.Yellow700.RGBA);
            if (ImGui.Button($"{IconFonts.FontAwesome6.Clock}  Stop Timeout##gc"))
            {
                if (available.Contains(GcRequestType.StopTimeout))
                    client.Send(RemoteControlToController.StopTimeout());
                else
                    client.Send(RemoteControlToController.SetTimeout(false));
            }
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.BeginDisabled(!available.Contains(GcRequestType.Timeout));
            if (ImGui.Button($"{IconFonts.FontAwesome6.Clock}  Request Timeout##gc"))
                client.Send(RemoteControlToController.SetTimeout(true));
            ImGui.EndDisabled();
        }

        ImGui.SameLine();

        if (active.Contains(GcRequestType.RobotSubstitution))
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Color.Blue700.RGBA);
            if (ImGui.Button($"{IconFonts.FontAwesome6.Robot}  Cancel Substitution##gc"))
                client.Send(RemoteControlToController.SetRobotSubstitution(false));
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.BeginDisabled(!available.Contains(GcRequestType.RobotSubstitution));
            if (ImGui.Button($"{IconFonts.FontAwesome6.Robot}  Robot Substitution##gc"))
                client.Send(RemoteControlToController.SetRobotSubstitution(true));
            ImGui.EndDisabled();
        }

        ImGui.BeginDisabled(!available.Contains(GcRequestType.ChallengeFlag));
        if (ImGui.Button($"{IconFonts.FontAwesome6.Flag}  Challenge Flag##gc"))
            client.Send(RemoteControlToController.ChallengeFlag());
        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(!available.Contains(GcRequestType.FailBallPlacement));
        if (ImGui.Button($"{IconFonts.FontAwesome6.CircleXmark}  Fail Placement##gc"))
            client.Send(RemoteControlToController.FailBallPlacement());
        ImGui.EndDisabled();

        ImGui.Spacing();
        DrawEmergencyStopButton(client, state);
    }

    private static void DrawEmergencyStopButton(GcRconClient client, RemoteControlTeamState state)
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
                client.Send(RemoteControlToController.SetEmergencyStop(false));
            ImGui.PopStyleColor(2);
        }
        else
        {
            ImGui.BeginDisabled(!available.Contains(GcRequestType.EmergencyStop));
            ImGui.PushStyleColor(ImGuiCol.Button,        Color.Red800.RGBA);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Color.Red700.RGBA);
            if (ImGui.Button($"{IconFonts.FontAwesome6.CircleStop}  Emergency Stop##gc"))
                client.Send(RemoteControlToController.SetEmergencyStop(true));
            ImGui.PopStyleColor(2);
            ImGui.EndDisabled();
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static GcTeamInfo? GetTeam(GcMatchState ms, string teamKey)
    {
        if (ms.TeamState == null) return null;
        // Keys may be team color string ("YELLOW"/"BLUE") or team name; try both
        if (ms.TeamState.TryGetValue(teamKey, out var info)) return info;
        var lower = teamKey.ToLowerInvariant();
        var capitalized = char.ToUpper(lower[0]) + lower[1..];
        return ms.TeamState.TryGetValue(capitalized, out info) ? info : null;
    }

    private static string FormatStage(string? stage) => stage switch
    {
        "NORMAL_FIRST_HALF_PRE"    => "Pre-Game",
        "NORMAL_FIRST_HALF"        => "First Half",
        "NORMAL_HALF_TIME"         => "Half Time",
        "NORMAL_SECOND_HALF_PRE"   => "Pre-Second Half",
        "NORMAL_SECOND_HALF"       => "Second Half",
        "EXTRA_TIME_BREAK"         => "Extra Time Break",
        "EXTRA_FIRST_HALF_PRE"     => "Pre-Extra First Half",
        "EXTRA_FIRST_HALF"         => "Extra First Half",
        "EXTRA_HALF_TIME"          => "Extra Half Time",
        "EXTRA_SECOND_HALF_PRE"    => "Pre-Extra Second Half",
        "EXTRA_SECOND_HALF"        => "Extra Second Half",
        "PENALTY_SHOOTOUT_BREAK"   => "Penalty Shootout Break",
        "PENALTY_SHOOTOUT"         => "Penalty Shootout",
        "POST_GAME"                => "Post Game",
        null or ""                 => "|",
        _                          => stage,
    };

    private static TimeSpan ParseDuration(string? s)
    {
        if (string.IsNullOrEmpty(s)) return TimeSpan.Zero;
        // protojson duration: "123s" or "-5s" or "0.5s"
        if (s.EndsWith('s') && double.TryParse(s.TrimEnd('s'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var sec))
            return TimeSpan.FromSeconds(sec);
        return TimeSpan.Zero;
    }

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
        _rconYellow.Dispose();
        _rconBlue.Dispose();
        _apiClient.Dispose();
        _process.Dispose();
    }
}
