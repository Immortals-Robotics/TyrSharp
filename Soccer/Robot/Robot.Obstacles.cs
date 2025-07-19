using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Vision.Data;
using Tyr.Soccer.Navigation.Obstacle;

namespace Tyr.Soccer.Robot;

public partial class Robot
{
    [ConfigEntry] private static float BallAreaRadius { get; set; } = 550.0f;
    [ConfigEntry] private static float PenaltyAreaExtensionBehindGoal { get; set; } = 300.0f;
    [ConfigEntry] private static float BigPenaltyAddition { get; set; } = 220.0f;

    public static float CalculateRobotRadius(RobotState state)
    {
        var extensionFactor = MathF.Max(0.0f, (state.Velocity.Length() - 1000.0f) / 9000.0f);
        return Context.RobotRadius * (1.0f + extensionFactor);
    }

    public static float CalculateOtherRadius(RobotState self, RobotState other)
    {
        const float kMaxExtension = 150.0f;
        const float kSpeedToReachMax = 1500.0f;

        var velocityDiff = other.Velocity - self.Velocity;
        var connection = Vector2.Normalize(other.Position - self.Position);
        var dot = Vector2.Dot(velocityDiff, connection) - 300.0f;

        var extension = Math.Clamp(kMaxExtension * dot / kSpeedToReachMax, 0.0f, kMaxExtension);
        return Context.RobotRadius + extension;
    }

    private void SetObstacles(NavigationFlags flags)
    {
        _obsMap.Reset();

        if (flags.HasFlag(NavigationFlags.NoObstacles))
        {
            Log.ZLogWarning($"Robot {Id} is navigating with no obstacles");
            return;
        }

        var noExtend = flags.HasFlag(NavigationFlags.NoExtraMargin);
        var ourPenalty = !flags.HasFlag(NavigationFlags.NoOwnPenaltyArea) && !Context.Referee.OurBallPlacement();
        var oppPenalty = !Context.Referee.BallPlacement();
        var oppPenaltyBig = Context.Referee.FreeKick() || Context.Referee.Stop();
        var theirHalf = Context.Referee.TheirKickoff() || flags.HasFlag(NavigationFlags.TheirHalf);
        var ballPlacementLine =
            Context.Referee.TheirBallPlacement() || flags.HasFlag(NavigationFlags.BallPlacementLine);

        var robotRadius = noExtend ? Context.RobotRadius : CalculateRobotRadius(State);
        _obsMap.BaseMargin = robotRadius;

        // own robots
        foreach (var own in Context.OwnRobots)
        {
            if (own.Seen && own.Id != Id)
            {
                var radius = CalculateRobotRadius(State);
                _obsMap.AddCircle(own.Position, radius, Physicality.Physical);
            }
        }

        // opponent robots
        foreach (var opp in Context.OppRobots)
        {
            var radius = noExtend ? Context.RobotRadius : CalculateOtherRadius(State, opp.State);
            _obsMap.AddCircle(opp.State.Position, radius, Physicality.Physical);
        }

        // ball obstacles
        var ballRadius = flags switch
        {
            _ when flags.HasFlag(NavigationFlags.BallObstacle) || !Context.Referee.AllowedNearBall() => BallAreaRadius,
            _ when flags.HasFlag(NavigationFlags.BallMediumObstacle) => 230.0f,
            _ when flags.HasFlag(NavigationFlags.BallSmallObstacle) => 60.0f,
            _ when flags.HasFlag(NavigationFlags.DynamicBallObstacle) => DynamicBallObstacleRadius,
            _ => 0.0f
        };

        if (ballRadius > 0.0f)
        {
            _obsMap.Add(new Circle()
                    { Center = Context.Ball.State.Position, Radius = Context.Field.BallRadius!.Value },
                Physicality.Physical);
            _obsMap.Add(new Circle()
                    { Center = Context.Ball.State.Position, Radius = ballRadius },
                Physicality.Virtual);
        }

        // goal posts
        const float wallThickness = 20.0f;
        var goalPostSize = new Vector2(Context.Field.GoalDepth, wallThickness);

        var posts = new[]
        {
            Rectangle.FromCornerAndSize(
                new Vector2(-Context.Field.Width - goalPostSize.X, -Context.Field.GoalWidth / 2f - goalPostSize.Y),
                goalPostSize.X, goalPostSize.Y),
            Rectangle.FromCornerAndSize(
                new Vector2(-Context.Field.Width - goalPostSize.X, Context.Field.GoalWidth / 2f - goalPostSize.Y),
                goalPostSize.X, goalPostSize.Y),
            Rectangle.FromCornerAndSize(
                new Vector2(Context.Field.Width, -Context.Field.GoalWidth / 2f - goalPostSize.Y),
                goalPostSize.X, goalPostSize.Y),
            Rectangle.FromCornerAndSize(new Vector2(Context.Field.Width, Context.Field.GoalWidth / 2f - goalPostSize.Y),
                goalPostSize.X, goalPostSize.Y)
        };

        foreach (var post in posts)
            _obsMap.Add(post, Physicality.Physical);

        var halfPenaltyWidth = Context.Field.PenaltyAreaWidth!.Value / 2f;

        if (ourPenalty)
        {
            var start = new Vector2(
                Context.SideSign * (Context.Field.Width + PenaltyAreaExtensionBehindGoal),
                -halfPenaltyWidth);
            var w = -Context.SideSign *
                    (PenaltyAreaExtensionBehindGoal + Context.Field.PenaltyAreaDepth!.Value);
            float h = Context.Field.PenaltyAreaWidth!.Value;
            _obsMap.Add(Rectangle.FromCornerAndSize(start, w, h), Physicality.Virtual);
        }

        if (oppPenaltyBig)
        {
            var r = Context.Field.PenaltyAreaDepth!.Value + BigPenaltyAddition;
            var w = Context.Field.PenaltyAreaWidth!.Value + 2.0f * BigPenaltyAddition;
            var halfW = w / 2.0f;

            var start = new Vector2(
                -Context.SideSign * (Context.Field.Width + PenaltyAreaExtensionBehindGoal), -halfW);
            var rectW = Context.SideSign * (PenaltyAreaExtensionBehindGoal + r);
            _obsMap.Add(Rectangle.FromCornerAndSize(start, rectW, w), Physicality.Virtual);
        }
        else if (oppPenalty)
        {
            var start = new Vector2(
                -Context.SideSign * (Context.Field.Width + PenaltyAreaExtensionBehindGoal),
                -halfPenaltyWidth);
            var w = Context.SideSign *
                    (PenaltyAreaExtensionBehindGoal + Context.Field.PenaltyAreaDepth!.Value);
            _obsMap.Add(Rectangle.FromCornerAndSize(start, w, Context.Field.PenaltyAreaWidth!.Value),
                Physicality.Virtual);
        }

        if (theirHalf)
            _obsMap.Add(Context.Field.TheirHalf(), Physicality.Virtual);

        // placement line clearance
        if (ballPlacementLine)
        {
            var clearance = BallAreaRadius * 2.0f;
            var ballToDest = Context.Referee.Gc.DesignatedPosition - Context.Ball.State.Position;
            var count = (int)MathF.Ceiling(ballToDest.Length() / (0.5f * clearance));

            for (var i = 0; i < count; i++)
            {
                var t = i / (float)count;
                var pos = Context.Ball.State.Position + ballToDest * t;
                _obsMap.AddCircle(pos, clearance, Physicality.Virtual);
            }
        }
    }
}