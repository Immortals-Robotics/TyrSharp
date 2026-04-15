using System;
using System.Numerics;
using Tyr.Common;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Time;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.Robot;
using Tyr.Soccer.Skills;
using Tyr.Soccer.Tactics.Fsm;

namespace Tyr.Soccer.Tactics;

[Configurable]
public partial class Def : ITactic
{
    [ConfigEntry] private static float TightStartAngle { get; set; } = 40.0f;
    [ConfigEntry] private static float MaxExtensionArea { get; set; } = 1100.0f;
    [ConfigEntry] private static float BallDistanceCoef { get; set; } = 0.7f;
    [ConfigEntry] private static DeltaTime DiveHysteresisTime { get; set; } = DeltaTime.FromMilliseconds(100);

    public Robot.Robot Robot { get; }
    private int DefId { get; }

    public enum State
    {
        Def,
        Dive
    }

    private readonly Fsm<State> _fsm;
    private static int _shirjeSelectHys = 0;

    public Def(Robot.Robot robot, int defId)
    {
        Robot = robot;
        DefId = defId;

        _fsm = new Fsm<State>(State.Def);

        _fsm.AddState(new DefState(this));
        _fsm.AddState(new DiveState(this));

        _fsm.AddTransition(State.Def, State.Dive, () => Context.Knowledge.BallIsGoaling);
        _fsm.AddTransition(State.Dive, State.Def, () => !Context.Knowledge.BallIsGoaling, DiveHysteresisTime);
    }

    public ISkill? Tick()
    {
        return _fsm.Tick();
    }

    private NavigationFlags GetNavigationFlags()
    {
        var flags = NavigationFlags.None;
        if (Context.Referee.OurBallPlacement())
            flags |= NavigationFlags.BallPlacementLine;

        // If ball is further from our goal than the robot, don't avoid it
        var ballDist = Context.Knowledge.BallToOwnGoalDistance;
        var robotDist = Vector2.Distance(Robot.Position, Context.Field.OwnGoal());
        if (ballDist > robotDist)
        {
            flags |= NavigationFlags.NoBallObstacle;
        }

        return flags;
    }

    internal static Vector2 CalculateStaticPos(int defId)
    {
        var predictedBall = Context.Knowledge.BallPredictedPosition;

        float areaExtensionSize = Vector2.Distance(predictedBall, Context.Field.OwnGoal()) * BallDistanceCoef - Context.Field.PenaltyAreaDepth;
        areaExtensionSize = Math.Clamp(areaExtensionSize, Context.Field.RobotRadius, MaxExtensionArea);

        float penaltyAreaHalfWidth = Context.Field.PenaltyAreaWidth / 2.0f;

        var virtualDefenseArea = Context.Field.ExtendedOwnPenaltyArea(areaExtensionSize);

        var ballAngle = Context.Knowledge.BallOwnGoalAngle;
        var ballAngleClamped = Math.Clamp(Context.Knowledge.BallOwnGoalAngleRaw - (90f - TightStartAngle), 0f, TightStartAngle);
        var ballSign = Math.Sign(ballAngle.DegNormalized);

        var goalOffset = new Vector2(0, Context.Field.GoalWidth / 2.0f);
        if (defId == 2) goalOffset = -goalOffset;

        var targetLine = Line.FromTwoPoints(predictedBall, Context.Field.OwnGoal() + goalOffset);

        if (ballAngleClamped > 0.0f)
        {
            var limitBall = Context.Field.OwnGoal() - Angle.FromDeg((90.0f - TightStartAngle) * ballSign).ToUnitVec() * Vector2.Distance(predictedBall, Context.Field.OwnGoal()) * Context.SideSign;

            bool shouldAdjust = defId == 1 ? (ballAngleClamped * ballSign * Context.SideSign >= 0.0f) : (ballAngleClamped * ballSign * Context.SideSign < 0.0f);
            if (shouldAdjust)
            {
                targetLine = Line.FromTwoPoints(limitBall, Context.Field.OwnGoal() + goalOffset);
            }
        }

        var (i1, i2) = Geometry.Intersection(virtualDefenseArea, targetLine);
        var finalPos = (Vector2.Normalize(predictedBall - Context.Field.OwnGoal()) * (penaltyAreaHalfWidth + Context.Field.RobotRadius + 20.0f)) + Context.Field.OwnGoal();

        if (i1.HasValue)
        {
            finalPos = i1.Value;
        }

        if (i2.HasValue && Vector2.Distance(i2.Value, predictedBall) < Vector2.Distance(finalPos, predictedBall))
        {
            finalPos = i2.Value;
        }

        var tangentLine = targetLine.TangentLine(finalPos);
        var ballGoalLine = Context.Knowledge.BallToOwnGoalLine;

        var midPoint = Geometry.Intersection(tangentLine, ballGoalLine);
        if (midPoint.HasValue)
        {
            finalPos += Vector2.Normalize(midPoint.Value - finalPos) * Context.Field.RobotRadius;
        }

        return finalPos;
    }

    private ISkill TickDive()
    {
        var def1 = Context.OwnRobots.FirstOrDefault(r => r.Role is Role.Def d && d.DefId == 1);
        var def2 = Context.OwnRobots.FirstOrDefault(r => r.Role is Role.Def d && d.DefId == 2);

        var def1Pos = def1?.Position ?? CalculateStaticPos(1);
        var def2Pos = def2?.Position ?? CalculateStaticPos(2);

        var ballLine = Line.FromPointAndAngle(Context.Ball.State.Position, Context.Ball.State.Velocity.Xy().ToAngle());
        var oneTouch1 = ballLine.ClosestPoint(def1Pos);
        var oneTouch2 = ballLine.ClosestPoint(def2Pos);

        int diveDefNum = 2;
        if (Vector2.Distance(oneTouch1, def1Pos) < Vector2.Distance(oneTouch2, def2Pos))
        {
            diveDefNum = 1;
            _shirjeSelectHys = 10;
        }

        if (_shirjeSelectHys > 0)
        {
            diveDefNum = 1;
            _shirjeSelectHys--; 
        }

        Vector2 target;
        float chip = 0f;

        if (diveDefNum == 1)
        {
            if (DefId == 1)
            {
                target = oneTouch1;
                chip = 150f;
                Draw.DrawPoint(oneTouch1, Color.Yellow, options: Options.Outline());
                Draw.DrawCircle(Robot.Position, Context.Field.RobotRadius * 2.0f, Color.Red, options: Options.Outline());
            }
            else
            {
                var tangent = ballLine.TangentLine(oneTouch1);
                var nearestToTangent = tangent.ClosestPoint(Robot.Position);
                target = oneTouch1 + Vector2.Normalize(nearestToTangent - oneTouch1) * (Context.Field.RobotRadius * 2.0f);
            }
        }
        else
        {
            if (DefId == 2)
            {
                target = oneTouch2;
                chip = 150f;
                Draw.DrawPoint(oneTouch2, Color.Yellow, options: Options.Outline());
                Draw.DrawCircle(Robot.Position, Context.Field.RobotRadius * 2.0f, Color.Red, options: Options.Outline());
            }
            else
            {
                var tangent = ballLine.TangentLine(oneTouch2);
                var nearestToTangent = tangent.ClosestPoint(Robot.Position);
                target = oneTouch2 + Vector2.Normalize(nearestToTangent - oneTouch2) * (Context.Field.RobotRadius * 2.0f);
            }
        }

        Draw.DrawCircle(oneTouch1, 50f, Color.Yellow, options: Options.Outline());
        Draw.DrawCircle(oneTouch2, 50f, Color.Yellow, options: Options.Outline());

        return new DiveSkill
        {
            Target = target,
            VelocityProfile = VelocityProfile.Kharaki,
            Chip = chip,
            NavigationFlags = GetNavigationFlags()
        };
    }


    private sealed class DefState(Def tactic) : IState<State>
    {
        public State Type => State.Def;
        public void Enter() { }
        public void Exit() { }

        public ISkill Tick()
        {
            var pos = CalculateStaticPos(tactic.DefId);

            return new GoToPoint
            {
                Target = pos,
                LookAt = Context.Ball.State.Position,
                VelocityProfile = VelocityProfile.Mamooli,
                NavigationFlags = tactic.GetNavigationFlags()
            };
        }
    }

    private sealed class DiveState(Def tactic) : IState<State>
    {
        public State Type => State.Dive;
        public void Enter() { }
        public void Exit() { }

        public ISkill Tick() => tactic.TickDive();
    }
}
