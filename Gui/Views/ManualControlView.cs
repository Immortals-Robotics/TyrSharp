using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Gui.Backend;
using Tyr.Soccer;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class ManualControlView
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.Gamepad} Manual Control";

    [ConfigEntry("Team to control from the manual soccer skill view", StorageType.User)]
    private static TeamColor ManualTeam { get; set; } = TeamColor.Yellow;

    private static readonly ManualSkillAction[] ManualActions =
    [
        ManualSkillAction.GoToPoint,
        ManualSkillAction.KickBall,
        ManualSkillAction.WaitForBall,
        ManualSkillAction.InterceptBall,
        ManualSkillAction.InterceptV2,
        ManualSkillAction.CaptureBall,
        ManualSkillAction.OneTouch,
        ManualSkillAction.TurnAndShoot,
        ManualSkillAction.DribbleToDirection,
    ];

    private static readonly string[] ManualActionLabels =
    [
        "Go To Point",
        "KickBall",
        "WaitForBall",
        "InterceptBall",
        "InterceptV2",
        "CaptureBall",
        "OneTouch",
        "TurnAndShoot",
        "DribbleToDirection",
    ];

    private static readonly string[] ManualTeamLabels =
    [
        "Yellow",
        "Blue",
    ];

    private static readonly string[] GoToPointFacingLabels =
    [
        "Angle",
        "Look At Target",
    ];

    private static readonly string[] ManualShotModeLabels =
    [
        "Kick Speed",
        "Chip Distance",
    ];

    private static readonly string[] ManualShotTargetLabels =
    [
        "Target Point",
        "Open Angle",
    ];

    public static TeamColor SelectedManualTeam => ManualTeam;

    public void Draw()
    {
        if (!ImGui.Begin(WindowTitle))
        {
            ImGui.End();
            return;
        }

        DrawManualSkillPanel();
        ImGui.End();
    }

    private static void DrawManualSkillPanel()
    {
        var yellowRunning = TeamRunner.IsRunning(TeamColor.Yellow);
        var blueRunning = TeamRunner.IsRunning(TeamColor.Blue);

        if (yellowRunning && !blueRunning)
        {
            ManualTeam = TeamColor.Yellow;
        }
        else if (blueRunning && !yellowRunning)
        {
            ManualTeam = TeamColor.Blue;
        }

        if (yellowRunning && blueRunning)
        {
            var teamIndex = ManualTeam == TeamColor.Blue ? 1 : 0;
            if (ImGui.Combo("Team", ref teamIndex, ManualTeamLabels, ManualTeamLabels.Length))
            {
                ManualTeam = teamIndex == 0 ? TeamColor.Yellow : TeamColor.Blue;
            }
        }
        else
        {
            ImGui.TextUnformatted($"Team: {ManualTeam}");
        }

        var teamRunning = TeamRunner.IsRunning(ManualTeam);
        var snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);

        if (!teamRunning)
        {
            ImGui.TextColored(Color.Orange400, "Selected team runner is not running.");
        }

        ImGui.BeginDisabled(!teamRunning);

        var enabled = snapshot.Enabled;
        if (ImGui.Checkbox("Enable manual mode", ref enabled))
        {
            TeamRunner.SetManualEnabled(ManualTeam, enabled);
            snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);
        }

        var robotId = snapshot.SelectedRobotId;
        if (ImGui.InputInt("Robot", ref robotId))
        {
            TeamRunner.SetManualSelectedRobot(ManualTeam, robotId);
            snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);
        }

        robotId = Math.Clamp(robotId, 0, 15);
        TeamRunner.SetManualSelectedRobot(ManualTeam, robotId);

        var actionIndex = Array.IndexOf(ManualActions, snapshot.Action);
        if (actionIndex < 0)
        {
            actionIndex = 0;
        }

        if (ImGui.Combo("Action", ref actionIndex, ManualActionLabels, ManualActionLabels.Length))
        {
            TeamRunner.SetManualAction(ManualTeam, ManualActions[actionIndex]);
            snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);
        }

        if (UsesShotTargetMode(snapshot.Action))
        {
            var shotTargetModeIndex = snapshot.ShotTargetMode == ManualShotTargetMode.OpenAngle ? 1 : 0;
            if (ImGui.Combo("Shot Target", ref shotTargetModeIndex, ManualShotTargetLabels, ManualShotTargetLabels.Length))
            {
                TeamRunner.SetManualShotTargetMode(
                    ManualTeam,
                    shotTargetModeIndex == 0 ? ManualShotTargetMode.TargetPoint : ManualShotTargetMode.OpenAngle);
                snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);
            }
        }

        if (UsesShotMode(snapshot.Action))
        {
            var shotModeIndex = snapshot.ShotMode == ManualShotMode.Chip ? 1 : 0;
            if (ImGui.Combo("Shot Type", ref shotModeIndex, ManualShotModeLabels, ManualShotModeLabels.Length))
            {
                TeamRunner.SetManualShotMode(ManualTeam, shotModeIndex == 0 ? ManualShotMode.Kick : ManualShotMode.Chip);
                snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);
            }

            if (snapshot.ShotMode == ManualShotMode.Kick)
            {
                var kickSpeedMps = snapshot.KickSpeedMps;
                if (ImGui.InputFloat("Kick Speed (m/s)", ref kickSpeedMps))
                {
                    TeamRunner.SetManualKickSpeedMps(ManualTeam, kickSpeedMps);
                    snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);
                }
            }
            else
            {
                var chipDistanceMeters = snapshot.ChipDistanceMeters;
                if (ImGui.InputFloat("Chip Distance (m)", ref chipDistanceMeters))
                {
                    TeamRunner.SetManualChipDistanceMeters(ManualTeam, chipDistanceMeters);
                    snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);
                }
            }
        }

        var requiresPoint = RequiresPoint(snapshot);
        if (UsesShotTargetMode(snapshot.Action) && snapshot.ShotTargetMode == ManualShotTargetMode.OpenAngle)
        {
            ImGui.TextColored(Color.Green400, "This action will use the open goal angle.");
        }
        else
        {
            ImGui.TextColored(
                requiresPoint ? Color.Yellow300 : Color.Zinc400,
                requiresPoint ? "This action needs a field point." : "This action does not require a field point.");
        }

        if (snapshot.HasTargetPoint)
        {
            ImGui.TextUnformatted($"Target: ({snapshot.TargetPoint.X:F0}, {snapshot.TargetPoint.Y:F0})");
        }
        else
        {
            ImGui.TextDisabled("Target: none");
        }

        if (requiresPoint || snapshot.HasTargetPoint)
        {
            ImGui.TextColored(Color.Zinc400, "Right-click on the field and choose 'Set Manual Target Here'.");
        }

        if (snapshot.Action == ManualSkillAction.GoToPoint)
        {
            var facingModeIndex = snapshot.GoToPointFacingMode == GoToPointFacingMode.LookAtTarget ? 1 : 0;
            if (ImGui.Combo("Facing", ref facingModeIndex, GoToPointFacingLabels, GoToPointFacingLabels.Length))
            {
                TeamRunner.SetGoToPointFacingMode(
                    ManualTeam,
                    facingModeIndex == 0 ? GoToPointFacingMode.Angle : GoToPointFacingMode.LookAtTarget);
                snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);
            }

            if (snapshot.GoToPointFacingMode == GoToPointFacingMode.Angle)
            {
                var facingAngle = snapshot.GoToPointFacingAngleDeg;
                if (ImGui.InputFloat("Facing Angle", ref facingAngle))
                {
                    TeamRunner.SetGoToPointFacingAngle(ManualTeam, facingAngle);
                    snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);
                }
            }
            else
            {
                if (snapshot.HasLookTarget)
                {
                    ImGui.TextUnformatted($"Look Target: ({snapshot.LookTarget.X:F0}, {snapshot.LookTarget.Y:F0})");
                }
                else
                {
                    ImGui.TextDisabled("Look Target: none");
                }

                if (ImGui.Button($"{IconFonts.FontAwesome6.Crosshairs}  Pick Look Target"))
                {
                    TeamRunner.SetManualAwaitingPoint(ManualTeam, true);
                    snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);
                }

                ImGui.SameLine();

                ImGui.BeginDisabled(!snapshot.HasLookTarget);
                if (ImGui.Button($"{IconFonts.FontAwesome6.Eraser}  Clear Look Target"))
                {
                    TeamRunner.ClearManualLookTarget(ManualTeam);
                    snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);
                }

                ImGui.EndDisabled();
            }
        }

        ImGui.BeginDisabled(!snapshot.HasTargetPoint);
        if (ImGui.Button($"{IconFonts.FontAwesome6.Eraser}  Clear Target"))
        {
            TeamRunner.ClearManualTargetPoint(ManualTeam);
            snapshot = TeamRunner.GetManualControlSnapshot(ManualTeam);
        }

        ImGui.EndDisabled();

        if (snapshot.AwaitingLookTarget)
        {
            ImGui.TextColored(Color.Green400, "Right-click the field and choose 'Set Manual Look Target Here'.");
        }

        if (snapshot.Running)
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.Stop}  Stop Action"))
            {
                TeamRunner.SetManualRunning(ManualTeam, false);
            }
        }
        else
        {
            ImGui.BeginDisabled(requiresPoint && !snapshot.HasTargetPoint);
            if (ImGui.Button($"{IconFonts.FontAwesome6.Play}  Start Action"))
            {
                TeamRunner.SetManualRunning(ManualTeam, true);
            }

            ImGui.EndDisabled();
        }

        ImGui.EndDisabled();
    }

    private static bool RequiresPoint(ManualControlSnapshot snapshot) => snapshot.Action switch
    {
        ManualSkillAction.GoToPoint => true,
        ManualSkillAction.KickBall or
        ManualSkillAction.OneTouch or
        ManualSkillAction.TurnAndShoot => snapshot.ShotTargetMode == ManualShotTargetMode.TargetPoint,
        ManualSkillAction.WaitForBall => true,
        ManualSkillAction.DribbleToDirection => true,
        _ => false
    };

    private static bool UsesShotMode(ManualSkillAction action) => action is
        ManualSkillAction.KickBall or
        ManualSkillAction.OneTouch or
        ManualSkillAction.TurnAndShoot;

    private static bool UsesShotTargetMode(ManualSkillAction action) => action is
        ManualSkillAction.KickBall or
        ManualSkillAction.OneTouch or
        ManualSkillAction.TurnAndShoot;
}
