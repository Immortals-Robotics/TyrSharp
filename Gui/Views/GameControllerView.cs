using System.Globalization;
using Hexa.NET.ImGui;
using Hexa.NET.SDL3;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Gui.Backend;
using Tyr.Gui.GameController;
using Tyr.Referee;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class GameControllerView : IDisposable
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.Flag} Game Controller";
    private static readonly string RefereeTabTitle = $"{IconFonts.FontAwesome6.Gavel}  Referee";
    private static readonly string TeamsTabTitle = $"{IconFonts.FontAwesome6.PeopleGroup}  Teams";

    [ConfigEntry("Hostname or IP address where the game controller is running", StorageType.User)]
    private static partial string GameControllerHost { get; set; } = "localhost";

    [ConfigEntry("TCP port for the team remote control protocol", StorageType.User)]
    private static partial int RemoteControlPort { get; set; } = 10011;

    [ConfigEntry("HTTP port for the web UI and referee WebSocket API", StorageType.User)]
    private static partial int WebUiPort { get; set; } = 8081;

    [ConfigEntry("Automatically start the  game controller", StorageType.User)]
    private static partial bool AutoStart { get;  set; } = false;
    
    [ConfigEntry("Automatically connect referee API and team rcon after starting the managed process", StorageType.User)]
    private static partial bool AutoConnect { get; set; } = true;

    [ConfigEntry("Team to issue gamepad commands for", StorageType.User)]
    private static partial TeamColor GamepadTeam { get; set; } = TeamColor.Yellow;

    private SDLGamepadPtr _gamepad;
    private readonly bool[] _lastButtons = new bool[32]; // SDL_GAMEPAD_BUTTON_MAX is usually small

    private readonly GcProcess    _process    = new();
    private readonly GcApiClient  _apiClient  = new();
    private readonly GcRconClient _rconYellow = new();
    private readonly GcRconClient _rconBlue   = new();
    private bool _autoConnectPending;
    private bool _autoStartDone;


    private int  _keeperInputYellow;
    private bool _keeperDirtyYellow;
    private int  _keeperInputBlue;
    private bool _keeperDirtyBlue;

    public void Update()
    {
        if (AutoStart && !_autoStartDone)
        {
            _autoStartDone = true;
            if (_process.CurrentStatus != GcProcess.Status.Running)
                StartProcess();
        }

        HandleAutoConnect();

        HandleGamepadInput();
    }

    public void Draw()
    {
        if (!ImGui.Begin(WindowTitle))
        {
            ImGui.End();
            return;
        }

        DrawProcessPanel();

        if (_apiClient.State == GcApiClient.ConnectionState.Connected)
        {
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.BeginTabBar("gc_tabs"))
            {
                if (ImGui.BeginTabItem(RefereeTabTitle))
                {
                    DrawRefereePanel();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(TeamsTabTitle))
                {
                    if (ImGui.BeginTable("gc_teams_split", 2,
                            ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable))
                    {
                        ImGui.TableNextColumn();
                        DrawTeamState(_rconYellow, ref _keeperInputYellow, ref _keeperDirtyYellow);

                        ImGui.TableNextColumn();
                        DrawTeamState(_rconBlue, ref _keeperInputBlue, ref _keeperDirtyBlue);

                        ImGui.EndTable();
                    }

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        ImGui.End();
    }

    // ── Process tab ───────────────────────────────────────────────────────────

    private unsafe void DrawProcessPanel()
    {
        var procStatus = _process.CurrentStatus;

        var (statusColor, statusLabel) = procStatus switch
        {
            GcProcess.Status.Running     => (Color.Green400,  $"{_process.StatusMessage}"),
            GcProcess.Status.Downloading => (Color.Sky300,    $"{_process.DownloadProgress * 100:F0}%"),
            GcProcess.Status.Exited      => (Color.Orange400, $"{_process.StatusMessage}"),
            GcProcess.Status.Error       => (Color.Red400,    $"{_process.StatusMessage}"),
            _ => (Color.Zinc500, _process.CachedVersion != null
                                    ? $"{_process.CachedVersion}"
                                    : "Not downloaded"),
        };

        ImGui.TextColored(statusColor, IconFonts.FontAwesome6.Circle);
        ImGui.SameLine();
        ImGui.TextUnformatted(statusLabel);

        ImGui.SameLine();

        ImGui.PushID("##gcproc"u8);
        if (procStatus == GcProcess.Status.Downloading)
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.Xmark}##gcdownload"))
                _process.CancelDownload();
            ImGui.ProgressBar(_process.DownloadProgress, new System.Numerics.Vector2(-1f, 0f));
        }
        else
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.CloudArrowDown}##gcdownload"))
                _process.StartDownload();

            ImGui.SameLine();

            if (procStatus == GcProcess.Status.Running)
            {
                if (ImGui.Button($"{IconFonts.FontAwesome6.Stop}##gcproc"))
                {
                    _process.Stop();
                    _apiClient.Disconnect();
                }
            }
            else
            {
                ImGui.BeginDisabled(_process.CachedVersion == null);
                if (ImGui.Button($"{IconFonts.FontAwesome6.Play}##gcproc"))
                    StartProcess();
                ImGui.EndDisabled();
            }
        }
        ImGui.PopID();
        
        ImGui.SameLine();
        ImGui.TextColored(Color.Zinc700, "|"u8);
        ImGui.SameLine();
        
        var (color, label) = _apiClient.State switch
        {
            GcApiClient.ConnectionState.Connected  => (Color.Green400,  "Connected"),
            GcApiClient.ConnectionState.Connecting => (Color.Yellow300, "Connecting…"),
            GcApiClient.ConnectionState.Error      => (Color.Red400,    $"Error: {_apiClient.LastError}"),
            _                                      => (Color.Zinc500,   "Disconnected"),
        };

        ImGui.TextColored(color, IconFonts.FontAwesome6.Circle);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(label);

        ImGui.SameLine();
        
        ImGui.PushID("##apiconn"u8);
        switch (_apiClient.State)
        {
            case GcApiClient.ConnectionState.Disconnected or GcApiClient.ConnectionState.Error:
            {
                if (ImGui.Button(IconFonts.FontAwesome6.Plug))
                    _apiClient.Connect(GameControllerHost, WebUiPort);
                break;
            }
            case GcApiClient.ConnectionState.Connecting:
            {
                ImGui.BeginDisabled(true);
                ImGui.Button(IconFonts.FontAwesome6.Plug);
                ImGui.EndDisabled();
                break;
            }
            case GcApiClient.ConnectionState.Connected:
            {
                if (ImGui.Button(IconFonts.FontAwesome6.PowerOff))
                    _apiClient.Disconnect();
                break;
            }
        }
        ImGui.PopID();
        
        ImGui.SameLine();
        
        ImGui.TextColored(Color.Zinc500, $"{GameControllerHost}:{WebUiPort}");

        if (_gamepad.Handle != null)
        {
            ImGui.SameLine();
            ImGui.TextColored(Color.Zinc700, "|"u8);
            ImGui.SameLine();
            
            var name = Gamepad.GetName(_gamepad);
            ImGui.TextColored(Color.Green400, IconFonts.FontAwesome6.Gamepad);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"{name} Connected\n\nA: Force Start\nB: Stop\nX: Direct Free Kick\nY: Kickoff\nStart: Halt\nRB: Normal Start");
            }
        }
    }

    private void StartProcess()
    {
        _process.Start(RemoteControlPort, WebUiPort, GcDataPublisher.GcAddress);
        
        if (_process.CurrentStatus == GcProcess.Status.Running && AutoConnect)
        {
            _autoConnectPending = true;
        }
    }

    private void HandleAutoConnect()
    {
        if (!_autoConnectPending) return;

        if (_apiClient.State == GcApiClient.ConnectionState.Connected)
        {
            _autoConnectPending = false;
            return;
        }

        if (_apiClient.State == GcApiClient.ConnectionState.Connecting) return;

        _apiClient.Connect(GameControllerHost, WebUiPort);
        _rconYellow.Connect(GameControllerHost, RemoteControlPort, TeamColor.Yellow);
        _rconBlue.Connect(GameControllerHost, RemoteControlPort, TeamColor.Blue);
    }

    // ── Referee tab ───────────────────────────────────────────────────────────

    private void DrawRefereePanel()
    {
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
        DrawStageControls();
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

        ImGui.SameLine();
        ImGui.TextColored(Color.Zinc700, "|"u8);
        ImGui.SameLine();
        
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
        
        ImGui.SameLine();
        ImGui.TextColored(Color.Zinc700, "|"u8);
        ImGui.SameLine();

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

    private void DrawStageControls()
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

    private void DrawTeamState(GcRconClient client, ref int keeperInput, ref bool keeperDirty)
    {
        if (client.State != GcRconClient.ConnectionState.Connected) return;
        
        var state = client.TeamState;
        if (state == null)
        {
            ImGui.TextDisabled("Waiting for state…");
            return;
        }

        ImGui.PushID(state.Team.ToString());
        
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
        
        ImGui.PopID();
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
                client.Send(available.Contains(GcRequestType.StopTimeout)
                    ? RemoteControlToController.StopTimeout()
                    : RemoteControlToController.SetTimeout(false));
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

    private void HandleGamepadInput()
    {
        if (RobotDebugView.CapturesGamepad)
        {
            Gamepad.Close(ref _gamepad);
            Array.Clear(_lastButtons);
            return;
        }

        Gamepad.Refresh(ref _gamepad);
        if (!Gamepad.IsConnected(_gamepad)) return;

        var team = GamepadTeam.ToString().ToUpperInvariant();
        var connected = _apiClient.State == GcApiClient.ConnectionState.Connected;

        PollButton(SDLGamepadButton.South, () => { if (connected) _apiClient.Send(GcInput.Command("FORCE_START")); }); // A
        PollButton(SDLGamepadButton.East, () => { if (connected) _apiClient.Send(GcInput.Command("STOP")); });         // B
        PollButton(SDLGamepadButton.West, () => { if (connected) _apiClient.Send(GcInput.Command("DIRECT", team)); }); // X
        PollButton(SDLGamepadButton.North, () => { if (connected) _apiClient.Send(GcInput.Command("KICKOFF", team)); }); // Y
        PollButton(SDLGamepadButton.Start, () => { if (connected) _apiClient.Send(GcInput.Command("HALT")); });       // Options/Start
        PollButton(SDLGamepadButton.RightShoulder, () => { if (connected) _apiClient.Send(GcInput.Command("NORMAL_START")); }); // RB
    }

    private void PollButton(SDLGamepadButton button, Action action)
    {
        bool down = SDL.GetGamepadButton(_gamepad, button);
        int idx = (int)button;
        if (down && !_lastButtons[idx]) action();
        _lastButtons[idx] = down;
    }

    public void Dispose()
    {
        Gamepad.Close(ref _gamepad);
        _rconYellow.Dispose();
        _rconBlue.Dispose();
        _apiClient.Dispose();
        _process.Dispose();
    }
}
