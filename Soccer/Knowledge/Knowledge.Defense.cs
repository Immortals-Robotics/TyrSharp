using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Time;

namespace Tyr.Soccer.Knowledge;

[Configurable]
public partial class Knowledge
{
    [ConfigEntry] public static partial DeltaTime DefPredictionTime { get; set; } = DeltaTime.FromMilliseconds(150);
    [ConfigEntry] public static partial float GoalieDiveMaximumTimeToReach { get; set; } = 3.0f;
    [ConfigEntry] public static partial float GoalieMaxBallSpeedForClear { get; set; } = 1000;
    [ConfigEntry] public static partial float PenaltyAreaExtensionSize { get; set; } = 200.0f;
    [ConfigEntry] public static partial float GoalLineExtentionSize { get; set; } = 100.0f;

    private Common.Data.Ssl.Gc.Command? _lastRefCommand;
    private Common.Time.Timestamp _oppRestartTimestamp;

    public bool GoalieDiveAllowed { get; private set; }
    public bool BallIsGoaling { get; private set; }
    public float BallOwnGoalReachTime { get; private set; }
    public bool IsDefending { get; private set; }
    public bool GoalieShouldDive { get; private set; }
    public bool GoalieShouldClear { get; private set; }

    public Vector2 BallPredictedPosition { get; private set; }
    public bool BallInExtendedPenaltyArea { get; private set; }
    public Vector2? BallGoalLineIntersection { get; private set; }
    public Line BallToOwnGoalLine { get; private set; }
    public float BallToOwnGoalDistanceX { get; private set; }
    public float BallToOwnGoalDistance { get; private set; }
    public Angle BallOwnGoalAngle { get; private set; }
    public float BallOwnGoalAngleRaw { get; private set; }

    public Dictionary<int, int> MarkMap { get; } = [];

    private void UpdateDefense()
    {
        var ballVel = Context.Ball.State.Velocity.Xy().Length();

        UpdateIsDefending();
        BallIsGoaling = UpdateBallIsGoaling();

        if (ballVel > float.Epsilon)
        {
            BallOwnGoalReachTime = Vector2.Distance(Context.Ball.State.Position, Context.Field.OwnGoal()) / ballVel;
        }
        else
        {
            BallOwnGoalReachTime = float.MaxValue;
        }

        BallPredictedPosition = BallPrediction.PredictBall(DefPredictionTime).Position;
        BallPredictedPosition = BallPredictedPosition with
        {
            X = Math.Clamp(BallPredictedPosition.X, -Context.Field.Width + Context.Field.BallRadius,
                Context.Field.Width - Context.Field.BallRadius)
        };

        BallInExtendedPenaltyArea =
            Context.Field.ExtendedOwnPenaltyArea(PenaltyAreaExtensionSize).Inside(BallPredictedPosition);

        BallToOwnGoalLine = Line.FromTwoPoints(BallPredictedPosition, Context.Field.OwnGoal());
        BallToOwnGoalDistanceX = MathF.Abs(BallPredictedPosition.X - Context.Field.OwnGoal().X);
        BallToOwnGoalDistance = Vector2.Distance(BallPredictedPosition, Context.Field.OwnGoal());
        BallOwnGoalAngle = (Context.Field.OwnGoal() - BallPredictedPosition).ToAngle() -
                           Angle.FromDeg(Context.SideSign == -1 ? 180f : 0f);
        BallOwnGoalAngleRaw = MathF.Abs(BallOwnGoalAngle.DegNormalized);

        GoalieShouldDive = BallIsGoaling && BallOwnGoalReachTime < GoalieDiveMaximumTimeToReach && GoalieDiveAllowed;
        var maxBallSpeedForClear = GoalieMaxBallSpeedForClear;
        if (BallIsGoaling)
        {
            maxBallSpeedForClear = 50.0f;
        }

        GoalieShouldClear = BallInExtendedPenaltyArea &&
                            Context.Ball.State.Velocity.Xy().Length() < maxBallSpeedForClear && GoalieDiveAllowed &&
                            !BallIsGoaling;
    }

    private void UpdateIsDefending()
    {
        var ballX = Context.Ball.State.Position.X;
        var sideSign = Context.SideSign;

        if (IsDefending)
        {
            if (sideSign * ballX < -500)
            {
                IsDefending = false;
            }
        }
        else if (sideSign * ballX > 500)
        {
            IsDefending = true;
        }
    }

    private bool UpdateBallIsGoaling()
    {
        BallGoalLineIntersection = null;

        if (Context.Ball.State.Velocity.Xy().Length() < 300.0f)
            return false;

        var movingToOurGoal = (Context.SideSign == -1 && Context.Ball.State.Velocity.X < 0) ||
                              (Context.SideSign == 1 && Context.Ball.State.Velocity.X > 0);

        if (!movingToOurGoal)
            return false;

        var ballTrajectory = Line.FromTwoPoints(Context.Ball.State.Position,
            Context.Ball.State.Position + Context.Ball.State.Velocity.Xy());
        var goalLine = Context.Field.OwnGoalLineExtended(GoalLineExtentionSize);

        var intersection = Geometry.Intersection(ballTrajectory, goalLine);

        if (!intersection.HasValue) return false;

        BallGoalLineIntersection = intersection.Value;

        var toIntersection = intersection.Value - Context.Ball.State.Position;
        var dot = Vector2.Dot(toIntersection, Context.Ball.State.Velocity.Xy());
        return dot > 0;
    }
}