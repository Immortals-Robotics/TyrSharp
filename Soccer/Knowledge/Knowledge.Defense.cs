using System.Collections.Generic;
using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Time;

namespace Tyr.Soccer.Knowledge;

[Configurable]
public partial class Knowledge
{
    [ConfigEntry] public static DeltaTime DefPredictionTime { get; set; } = DeltaTime.FromMilliseconds(150);
    [ConfigEntry] public static float GoalieDiveMaximumTimeToReach { get; set; } = 3.0f;
    [ConfigEntry] public static float GoalieMaxBallSpeedForClear { get; set; } = 1000;
    [ConfigEntry] public static float PenaltyAreaExtensionSize { get; set; } = 200.0f;

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
            X = Math.Clamp(BallPredictedPosition.X, -Context.Field.Width + Context.Field.BallRadius, Context.Field.Width - Context.Field.BallRadius)
        };
        
        BallInExtendedPenaltyArea = Context.Field.ExtendedOwnPenaltyArea(PenaltyAreaExtensionSize).Inside(BallPredictedPosition);

        BallToOwnGoalLine = Line.FromTwoPoints(BallPredictedPosition, Context.Field.OwnGoal());
        BallToOwnGoalDistanceX = MathF.Abs(BallPredictedPosition.X - Context.Field.OwnGoal().X);
        BallToOwnGoalDistance = Vector2.Distance(BallPredictedPosition, Context.Field.OwnGoal());
        BallOwnGoalAngle = (Context.Field.OwnGoal() - BallPredictedPosition).ToAngle() - Angle.FromDeg(Context.SideSign == -1 ? 180f : 0f);
        BallOwnGoalAngleRaw = MathF.Abs(BallOwnGoalAngle.DegNormalized);

        GoalieShouldDive = BallIsGoaling && BallOwnGoalReachTime < GoalieDiveMaximumTimeToReach && GoalieDiveAllowed;
        var maxBallSpeedForClear = GoalieMaxBallSpeedForClear;
        if (BallIsGoaling)
        {
            maxBallSpeedForClear = 50.0f;
        }

        GoalieShouldClear = BallInExtendedPenaltyArea && Context.Ball.State.Velocity.Xy().Length() < maxBallSpeedForClear && GoalieDiveAllowed && !BallIsGoaling;
    }

    private void UpdateIsDefending()
    {
        var ballX = Context.Ball.State.Position.X;
        var sideSign = Context.SideSign;

        if (IsDefending)
        {
            if (sideSign * ballX < -800)
            {
                IsDefending = false;
            }
        }
        else if (sideSign * ballX > -300)
        {
            IsDefending = true;
        }

        var currentRefCommand = Context.Referee.Gc.Command;
        if (_lastRefCommand != currentRefCommand)
        {
            if (Context.Referee.TheirRestart())
            {
                _oppRestartTimestamp = Context.Time;
            }
            _lastRefCommand = currentRefCommand;
        }

        if (Context.Referee.TheirRestart() && (Context.Time - _oppRestartTimestamp).Seconds < 2.0)
        {
            IsDefending = true;
        }
    }

    public float CalculateOppThreat(int oppId)
    {
        var oppIndex = Context.OppRobots.FindIndex(r => r.Id.Id == oppId);
        if (oppIndex == -1)
            return -1;

        var opp = Context.OppRobots[oppIndex];
        if (opp.Quality <= 0)
            return -1;

        if (oppId == (int)Context.Referee.OppInfo().Goalkeeper)
            return -1;

        if (Vector2.Distance(opp.State.Position, Context.Ball.State.Position) < 500)
            return -1;

        if (opp.State.Position.X * Context.SideSign < 1000)
            return -1;

        var oppDisToGoal = Vector2.Distance(opp.State.Position, Context.Field.OwnGoal());

        var t1Angle = opp.State.Position.AngleWith(Context.Field.OwnGoalPostBottom());
        var t2Angle = opp.State.Position.AngleWith(Context.Field.OwnGoalPostTop());

        var oppOpenAngleToGoal = MathF.Abs((t2Angle - t1Angle).DegNormalized);

        var oppToBall = Vector2.Normalize(Context.Ball.State.Position - opp.State.Position);
        var oppToGoal = Vector2.Normalize(Context.Field.OwnGoal() - opp.State.Position);

        var oneTouchDot = Vector2.Dot(oppToBall, oppToGoal);

        var ballToOppDis = Vector2.Distance(Context.Ball.State.Position, opp.State.Position);

        float scoreGoalDis;
        if (oppDisToGoal < 3000)
            scoreGoalDis = 1.0f;
        else
            scoreGoalDis = 1.0f - MathF.Pow(MathF.Max(0.0f, (oppDisToGoal - 3000.0f) / 3000.0f), 0.5f);

        float scoreBallDis;
        if (ballToOppDis < 2000)
            scoreBallDis = MathF.Pow(ballToOppDis / 2000.0f, 2.0f);
        else if (ballToOppDis < 6000)
            scoreBallDis = 1.0f;
        else
            scoreBallDis = 1.0f - (ballToOppDis - 6000.0f) / 6000.0f;

        var scoreOpenAngle = oppOpenAngleToGoal / 15.0f;

        float scoreOneTouchAngle;
        if (oneTouchDot >= 0.0f)
            scoreOneTouchAngle = 4 * oneTouchDot - 4 * MathF.Pow(oneTouchDot, 2.0f);
        else
            scoreOneTouchAngle = 0.0f;

        scoreGoalDis = Math.Clamp(scoreGoalDis, 0.0f, 1.0f);
        scoreBallDis = Math.Clamp(scoreBallDis, 0.0f, 1.0f);
        scoreOpenAngle = Math.Clamp(scoreOpenAngle, 0.0f, 1.0f);
        scoreOneTouchAngle = Math.Clamp(scoreOneTouchAngle, 0.0f, 1.0f);

        var finalScoreOneTouch = scoreOneTouchAngle * MathF.Min(scoreBallDis * scoreGoalDis, scoreOpenAngle);
        var finalScoreTurnShoot = MathF.Min(scoreBallDis * scoreGoalDis, scoreOpenAngle);

        return MathF.Max(finalScoreOneTouch, finalScoreTurnShoot);
    }

    public float CalculateMarkCost(int robotId, int oppId)
    {
        var own = Context.OwnRobots[robotId];
        var oppIndex = Context.OppRobots.FindIndex(r => r.Id.Id == oppId);

        if (!own.Seen || oppIndex == -1)
            return -1;

        var opp = Context.OppRobots[oppIndex];
        if (opp.Quality <= 0)
            return -1;

        const float kPredictT = 0.3f;

        var predictedPosOwn = own.State.Position + own.State.Velocity * kPredictT;
        var predictedPosOpp = opp.State.Position + opp.State.Velocity * kPredictT;
        var disPred = Vector2.Distance(predictedPosOwn, predictedPosOpp);

        var alreadyMarked = MarkMap.TryGetValue(robotId, out var currentOpp) && currentOpp == oppId;

        var costStay = alreadyMarked ? (disPred - 500.0f) / 1000.0f : 1.0f;
        var costReach = (disPred - 500.0f) / 10000.0f;

        costStay = Math.Clamp(costStay, 0.0f, 1.0f);
        costReach = Math.Clamp(costReach, 0.0f, 1.0f);

        const float kWeightStay = 0.0f;
        const float kWeightReach = 1.0f;

        return kWeightStay * costStay + kWeightReach * costReach;
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
        var goalLine = Context.Field.OwnGoalLine();

        var intersection = Geometry.Intersection(ballTrajectory, goalLine);

        if (!intersection.HasValue) return false;

        BallGoalLineIntersection = intersection.Value;

        var toIntersection = intersection.Value - Context.Ball.State.Position;
        var dot = Vector2.Dot(toIntersection, Context.Ball.State.Velocity.Xy());
        return dot > 0;
    }
}
