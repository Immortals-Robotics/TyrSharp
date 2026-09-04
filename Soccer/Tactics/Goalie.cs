using System;
using System.Numerics;
using Tyr.Common;
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
    [ConfigEntry] private static partial float TightStartAngle { get; set; } = 25.0f;
    [ConfigEntry] private static partial float GoalieBoxNotch { get; set; } = 600.0f;
    [ConfigEntry] private static partial float GoalieDiveInterceptionOffset { get; set; } = 200.0f;
    [ConfigEntry] private static partial float GoalieChip { get; set; } = 150.0f;
    [ConfigEntry] private static partial DeltaTime DiveHysteresisTime { get; set; } = DeltaTime.FromMilliseconds(100);
    [ConfigEntry] private static partial DeltaTime ClearHysteresisTime { get; set; } = DeltaTime.FromMilliseconds(300);
    public Robot.Robot Robot { get; private set; }

    public enum State
    {
        Normal,
        Dive,
        Clear
    }

    private readonly Fsm<State> _fsm;

    public Goalie(Robot.Robot robot)
    {
        Robot = robot;

        _fsm = new Fsm<State>(State.Normal);

        _fsm.AddState(new BlockState());
        _fsm.AddState(new DiveState(this));
        _fsm.AddState(new ClearState());

        // Normal -> Dive
        _fsm.AddTransition(State.Normal, State.Dive, () => Context.Knowledge.GoalieShouldDive);

        // Normal -> Clear
        _fsm.AddTransition(State.Normal, State.Clear, () => Context.Knowledge.GoalieShouldClear);

        // Dive -> Normal
        _fsm.AddTransition(State.Dive, State.Normal, () => !Context.Knowledge.GoalieShouldDive, DiveHysteresisTime);

        // Clear -> Normal
        _fsm.AddTransition(State.Clear, State.Normal, () => !Context.Knowledge.GoalieShouldClear, ClearHysteresisTime);
    }

    public ISkill? Tick()
    {
        Draw.DrawCircle(Robot.Position, 100f, Context.Knowledge.BallIsGoaling ? Color.Red : Color.Yellow,
            options: Options.Outline());

        return _fsm.Tick();
    }

    public static NavigationFlags GetNavigationFlags()
    {
        var flags = NavigationFlags.NoOwnPenaltyArea;
        if (Context.Knowledge.GoalieDiveAllowed)
            flags |= NavigationFlags.NoBallObstacle;
        if (Context.Referee.Running() || Context.Referee.TheirPenaltyKick())
            flags |= NavigationFlags.NoExtraMargin;
        if (Context.Referee.OurBallPlacement() || Context.Referee.TheirBallPlacement())
            flags |= NavigationFlags.BallPlacementLine;


        return flags;
    }

    private sealed class BlockState() : IState<State>
    {
        public State Type => State.Normal;

        public void Enter()
        {
        }

        public ISkill Tick()
        {
            var penaltyAreaHalfWidth = Context.Field.PenaltyAreaWidth / 2.0f;

            var ballPositionEffect = Context.Knowledge.BallToOwnGoalDistanceX;
            var startAngEffect = TightStartAngle;

            var ballAngle = Math.Clamp(Context.Knowledge.BallOwnGoalAngleRaw - (90f - startAngEffect), 0f,
                startAngEffect);

            var gkMaxDist = penaltyAreaHalfWidth - GoalieBoxNotch;
            var ballAngEffect = gkMaxDist -
                                ((gkMaxDist - (Context.Field.GoalWidth / 2f - Context.RobotRadius)) / startAngEffect) *
                                ballAngle;

            var gkTargetRect = Context.Field.ExtendedOwnPenaltyArea(-GoalieBoxNotch);
            var ballGoalLine = Context.Knowledge.BallToOwnGoalLine;

            var (i0, i1) = Geometry.Intersection(gkTargetRect, ballGoalLine);
            var gkFinalPos = Context.Field.OwnGoal();

            if (i0.HasValue)
            {
                if (Vector2.Distance(i0.Value, Context.Knowledge.BallPredictedPosition) <
                    Vector2.Distance(gkFinalPos, Context.Knowledge.BallPredictedPosition))
                {
                    gkFinalPos = i0.Value;
                }
            }

            if (i1.HasValue)
            {
                if (Vector2.Distance(i1.Value, Context.Knowledge.BallPredictedPosition) <
                    Vector2.Distance(gkFinalPos, Context.Knowledge.BallPredictedPosition))
                {
                    gkFinalPos = i1.Value;
                }
            }

            const float Slope = 0.0001538461538f;
            var speedEffect = Slope * Context.Ball.State.Velocity.Xy().Length();
            speedEffect = Math.Clamp(speedEffect, 0f, 0.9f);

            gkFinalPos -= (gkFinalPos - Context.Field.OwnGoal()) * speedEffect;
            if (ballAngle > 0f && (gkFinalPos - Context.Field.OwnGoal()).Length() > ballAngEffect)
            {
                var dir = Vector2.Normalize(Context.Knowledge.BallPredictedPosition - Context.Field.OwnGoal());
                if (dir.LengthSquared() > 0)
                {
                    gkFinalPos = dir * ballAngEffect + Context.Field.OwnGoal();
                }
            }

            var safeX = Context.Field.OwnGoalSafeX();
            if (MathF.Abs(gkFinalPos.X * Context.SideSign) >= MathF.Abs(safeX * Context.SideSign))
            {
                gkFinalPos.X = safeX;
            }

            Draw.DrawPoint(gkFinalPos, Color.Blue);

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

        public void Exit()
        {
        }
    }

    private sealed class DiveState(Goalie tactic) : IState<State>
    {
        public State Type => State.Dive;
        private BallInterception.InterceptPlan? _previousPlan;

        public void Enter()
        {
            _previousPlan = null;
        }

        public ISkill Tick()
        {
            var profile = VelocityProfile.Kharaki with { Acceleration = VelocityProfile.Kharaki.Acceleration * 3f };
            var allowedArea = Context.Field.OwnPenaltyArea();

            var trajectory = ServiceLocator.BallTrajectoryFactory.FromState(Context.Ball.State);
            var hasPlan = Context.Knowledge.BallInterception.TryFindGoaliePlan(
                Context.Ball,
                trajectory,
                tactic.Robot.Position,
                tactic.Robot.CurrentMotion,
                profile,
                allowedArea,
                out var plan,
                _previousPlan);

            Vector2 target;
            if (hasPlan)
            {
                _previousPlan = plan;
                target = plan.CenterDestination;
            }
            else
            {
                var ballLine = Line.FromPointAndAngle(Context.Ball.State.Position,
                    Context.Ball.State.Velocity.Xy().ToAngle());
                var ballLineClosest = ballLine.ClosestPoint(tactic.Robot.Position);
                target = Context.Knowledge.BallGoalLineIntersection ?? ballLineClosest;
            }

            return new DiveSkill
            {
                Target = target,
                VelocityProfile = profile,
                NavigationFlags = GetNavigationFlags(),
                Chip = GoalieChip
            };
        }

        public void Exit()
        {
        }
    }

    private sealed class ClearState : IState<State>
    {
        public State Type => State.Clear;

        public void Enter()
        {
        }

        public ISkill Tick()
        {
            var targetPos = Context.Field.OppGoal();
            var kickAngle = Context.Knowledge.BallPredictedPosition.AngleWith(targetPos);

            return new KickBall
            {
                Angle = kickAngle,
                Kick = 0f,
                Chip = GoalieChip,
                IsGoalkeeper = true,
            };
        }

        public void Exit()
        {
        }
    }
}