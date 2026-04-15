using System;
using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data;
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
public partial class Goalie : ITactic
{
    [ConfigEntry] private static DeltaTime DefPredictionTime { get; set; } = DeltaTime.FromMilliseconds(150);
    [ConfigEntry] private static float TightStartAngle { get; set; } = 25.0f;
    [ConfigEntry] private static int ShirjeHysteresisTicks { get; set; } = 10;

    public Robot.Robot Robot { get; private set; }

    public enum State
    {
        Block,
        Dive,
        Clear
    }

    private readonly Fsm<State> _fsm;
    private int _shirjeTicksRemaining;

    public Goalie(Robot.Robot robot)
    {
        Robot = robot;

        _fsm = new Fsm<State>(State.Block);

        _fsm.AddState(new BlockState());
        _fsm.AddState(new DiveState(this));
        _fsm.AddState(new ClearState());

        // Block -> Dive
        _fsm.AddTransition(State.Block, State.Dive, () =>
        {
            bool refShirjeAllowed = Context.Referee.Running() || Context.Referee.TheirPenaltyKick();
            var ballVel = Context.Ball.State.Velocity.Xy().Length();
            if (ballVel < 0.001f) return false;
            
            float timeToReach = Vector2.Distance(Context.Ball.State.Position, Robot.Position) / ballVel;
            
            if (Context.Knowledge.BallIsGoaling() && timeToReach < 3.0f && refShirjeAllowed)
            {
                _shirjeTicksRemaining = ShirjeHysteresisTicks;
                return true;
            }
            return false;
        });

        // Block -> Clear
        _fsm.AddTransition(State.Block, State.Clear, () =>
        {
            bool refShirjeAllowed = Context.Referee.Running() || Context.Referee.TheirPenaltyKick();
            var predictedBall = Context.Knowledge.BallPrediction.PredictBall(DefPredictionTime).Position;
            var obs = GetExtendedPenaltyArea();

            if (obs.Inside(predictedBall) && Context.Ball.State.Velocity.Xy().Length() < 1500f && refShirjeAllowed)
            {
                if (Context.Knowledge.BallIsGoaling() && Context.Ball.State.Velocity.Xy().Length() > 50.0f)
                {
                    return false; // should dive or block
                }
                return true;
            }
            return false;
        });

        // Dive -> Block
        _fsm.AddTransition(State.Dive, State.Block, () =>
        {
            if (_shirjeTicksRemaining <= 0)
            {
                return true;
            }
            return false;
        });

        // Clear -> Block
        _fsm.AddTransition(State.Clear, State.Block, () =>
        {
            bool refShirjeAllowed = Context.Referee.Running() || Context.Referee.TheirPenaltyKick();
            var predictedBall = Context.Knowledge.BallPrediction.PredictBall(DefPredictionTime).Position;
            var obs = GetExtendedPenaltyArea();

            if (!obs.Inside(predictedBall) || Context.Ball.State.Velocity.Xy().Length() >= 1500f || !refShirjeAllowed)
            {
                return true;
            }
            if (Context.Knowledge.BallIsGoaling() && Context.Ball.State.Velocity.Xy().Length() > 50.0f)
            {
                return true; // go back to block, then dive
            }
            return false;
        });
    }

    public ISkill? Tick()
    {
        if (_shirjeTicksRemaining > 0)
        {
            _shirjeTicksRemaining--;
        }

        if (Context.Knowledge.BallIsGoaling())
        {
            Draw.DrawCircle(Robot.Position, 100f, Color.Red, options: Options.Outline());
        }
        else
        {
            Draw.DrawCircle(Robot.Position, 100f, Color.Yellow, options: Options.Outline());
        }

        return _fsm.Tick();
    }

    private static Rectangle GetExtendedPenaltyArea()
    {
        const float AreaExtensionSize = 200.0f;
        float penaltyAreaHalfWidth = Context.Field.PenaltyAreaWidth / 2.0f;
        var start = new Vector2(Context.Field.OwnGoal().X, -(penaltyAreaHalfWidth + AreaExtensionSize));
        float w = -Context.SideSign * (AreaExtensionSize + Context.Field.PenaltyAreaDepth);
        float h = Context.Field.PenaltyAreaWidth + 2 * AreaExtensionSize;
        return Rectangle.FromCornerAndSize(start, w, h);
    }

    public static NavigationFlags GetNavigationFlags()
    {
        var flags = NavigationFlags.NoOwnPenaltyArea | NavigationFlags.NoBallObstacle;
        if (Context.Referee.Running() || Context.Referee.TheirPenaltyKick())
            flags |= NavigationFlags.NoExtraMargin;
        if (Context.Referee.OurBallPlacement())
            flags |= NavigationFlags.BallPlacementLine;
        return flags;
    }

    private sealed class BlockState() : IState<State>
    {
        public State Type => State.Block;

        public void Enter() { }

        public ISkill Tick()
        {
            var predictedBall = Context.Knowledge.BallPrediction.PredictBall(DefPredictionTime).Position;

            const float AreaNotch = 600.0f;
            float penaltyAreaHalfWidth = Context.Field.PenaltyAreaWidth / 2.0f;

            var goalLine = Line.FromTwoPoints(Context.Field.OwnGoal() - new Vector2(0, 1000f),
                                              Context.Field.OwnGoal() + new Vector2(0, 1000f));
            float ballPositionEffect = goalLine.Distance(predictedBall);
            float startAngEffect = TightStartAngle;

            var ballAngleRaw = MathF.Abs(((Context.Field.OwnGoal() - predictedBall).ToAngle() - Angle.FromDeg(Context.SideSign == -1 ? 180f : 0f)).DegNormalized);
            var ballAngle = Math.Clamp(ballAngleRaw - (90f - startAngEffect), 0f, startAngEffect);

            var gkMaxDist = penaltyAreaHalfWidth - AreaNotch;
            var ballAngEffect = gkMaxDist - ((gkMaxDist - (Context.Field.GoalWidth / 2f - Context.RobotRadius)) / startAngEffect) * ballAngle;

            float gkTargetW = -Context.SideSign * MathF.Min(Context.Field.PenaltyAreaDepth - AreaNotch, ballPositionEffect);
            float gkTargetH = Context.Field.PenaltyAreaWidth - 2 * AreaNotch;
            var gkTargetAreaStart = new Vector2(Context.Field.OwnGoal().X, -(penaltyAreaHalfWidth - AreaNotch));

            var gkTargetRect = Rectangle.FromCornerAndSize(gkTargetAreaStart, gkTargetW, gkTargetH);
            var ballGoalLine = Line.FromTwoPoints(predictedBall, Context.Field.OwnGoal());
            
            var (i0, i1) = Geometry.Intersection(gkTargetRect, ballGoalLine);
            var gkFinalPos = Context.Field.OwnGoal();
            
            if (i0.HasValue)
            {
                Draw.DrawPoint(i0.Value, Color.Blue);
                if (Vector2.Distance(i0.Value, predictedBall) < Vector2.Distance(gkFinalPos, predictedBall))
                {
                    gkFinalPos = i0.Value;
                }
            }
            if (i1.HasValue)
            {
                Draw.DrawPoint(i1.Value, Color.Blue);
                if (Vector2.Distance(i1.Value, predictedBall) < Vector2.Distance(gkFinalPos, predictedBall))
                {
                    gkFinalPos = i1.Value;
                }
            }

            const float Slope = 0.0001538461538f;
            float speedEffect = Slope * Context.Ball.State.Velocity.Xy().Length();
            speedEffect = Math.Clamp(speedEffect, 0f, 0.9f);
            
            gkFinalPos -= (gkFinalPos - Context.Field.OwnGoal()) * speedEffect;
            if (ballAngle > 0f && (gkFinalPos - Context.Field.OwnGoal()).Length() > ballAngEffect)
            {
                var dir = Vector2.Normalize(predictedBall - Context.Field.OwnGoal());
                if (dir.LengthSquared() > 0)
                {
                    gkFinalPos = dir * ballAngEffect + Context.Field.OwnGoal();
                }
            }

            float safeX = Context.Field.OwnGoal().X - (Context.SideSign * (Context.RobotRadius + 20f));
            if (MathF.Abs(gkFinalPos.X * Context.SideSign) >= MathF.Abs(safeX * Context.SideSign))
            {
                gkFinalPos.X = safeX;
            }

            Draw.DrawLine(ballGoalLine, Color.Red);

            var faceDir = Vector2.Normalize(gkFinalPos - Context.Field.OwnGoal());
            var lookAt = faceDir.LengthSquared() > 0 
                ? Context.Field.OwnGoal() + faceDir * 10000f 
                : Context.Ball.State.Position;

            return new GoToPoint
            {
                Target = gkFinalPos,
                LookAt = lookAt,
                VelocityProfile = VelocityProfile.Mamooli,
                NavigationFlags = GetNavigationFlags()
            };
        }

        public void Exit() { }
    }

    private sealed class DiveState(Goalie tactic) : IState<State>
    {
        public State Type => State.Dive;

        public void Enter() { }

        public ISkill Tick()
        {
            var profile = VelocityProfile.Kharaki with { Acceleration = VelocityProfile.Kharaki.Acceleration * 3f };

            float interceptT = -1.0f;
            float maxWaitT = float.MinValue;
            var goalieCircle = new Circle { Center = Context.Field.OwnGoal(), Radius = Context.Field.PenaltyAreaDepth - 200.0f };

            for (float t = 0.0f; t < 5.0f; t += 0.1f)
            {
                var point = Context.Knowledge.BallPrediction.PredictBall(DeltaTime.FromSeconds(t)).Position;
                if (!goalieCircle.Inside(point) || !Context.Field.RectangleWithBoundary.Inside(point))
                {
                    continue;
                }

                if (MathF.Abs(point.X) > Context.Field.Width)
                {
                    break;
                }

                float robotReachT = tactic.Robot.CalculateReachTime(point, profile);
                float waitT = t - robotReachT;

                if (waitT > maxWaitT)
                {
                    maxWaitT = waitT;
                    interceptT = t;
                }

                if (waitT > 1.5f)
                {
                    break;
                }
            }

            var target = Context.Knowledge.BallPrediction.PredictBall(DeltaTime.FromSeconds(MathF.Max(0f, interceptT))).Position;
            var ballLine = Line.FromPointAndAngle(Context.Ball.State.Position, Context.Ball.State.Velocity.Xy().ToAngle());
            var ballLineClosest = ballLine.ClosestPoint(tactic.Robot.Position);
            float distToClosest = Vector2.Distance(ballLineClosest, tactic.Robot.Position);

            if (interceptT < 0.0f || maxWaitT < 1.0f)
            {
                var intersect = Geometry.Intersection(ballLine, Context.Field.OwnGoalLine());
                target = intersect ?? ballLineClosest;
            }
            else if (distToClosest < Context.RobotRadius * 0.5f)
            {
                target = ballLineClosest;
            }

            return new DiveSkill
            {
                Target = target,
                VelocityProfile = profile,
                NavigationFlags = GetNavigationFlags()
            };
        }

        public void Exit() { }
    }

    private class DiveSkill : ISkill
    {
        public required Vector2 Target { get; init; }
        public required VelocityProfile VelocityProfile { get; init; }
        public NavigationFlags NavigationFlags { get; init; }

        public void Execute(Robot.Robot robot)
        {
            robot.Face(Context.Ball.State.Position);
            robot.Navigate(Target, VelocityProfile, NavigationFlags);
            robot.Chip = 150f;
        }
    }

    private sealed class ClearState : IState<State>
    {
        public State Type => State.Clear;

        public void Enter() { }

        public ISkill Tick()
        {
            var predictedBall = Context.Knowledge.BallPrediction.PredictBall(DefPredictionTime).Position;
            var targetPos = Context.Field.OppGoal();
            var kickAngle = predictedBall.AngleWith(targetPos);

            return new KickBall
            {
                Angle = kickAngle,
                Kick = 0f,
                Chip = 150f,
                IsGoalkeeper = true
            };
        }

        public void Exit() { }
    }
}
