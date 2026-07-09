using System.Numerics;
using NumFlat;
using Tyr.Common.Config;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Time;
using Tyr.Soccer.Robot;
using Tyr.Soccer.Skills;
using Tyr.Soccer.Tactics.Fsm;

namespace Tyr.Soccer.Tactics;

[Configurable]
public partial class BallPlacement : ITactic
{
    [ConfigEntry] public static DeltaTime BPStateDelay { get; set; } = DeltaTime.FromMilliseconds(333);
    [ConfigEntry] public static float BPKissInitDistance { get; set; } = 250f;

    public enum State
    {
        Idle,
        KissInit,
        KissTouch,
        Kissing,
        Stuck,
        LongDistance,
        KissingDone,
        Done
    }

    public Robot.Robot Robot { get; }
    public int PlacerId { get; }

    private readonly Fsm<State> _fsm;

    // Shared data that needs to be persisted across states but is calculated deterministically
    private Vector2 _ballPlacer1FinalPos;
    private Vector2 _ballPlacer2FinalPos;
    private bool _forceStuck;

    public BallPlacement(Robot.Robot robot, int placerId)
    {
        Robot = robot;
        PlacerId = placerId;

        _fsm = new Fsm<State>(State.Idle);

        _fsm.AddState(new IdleState(this));
        _fsm.AddState(new KissInitState(this));
        _fsm.AddState(new KissTouchState(this));
        _fsm.AddState(new KissingState(this));
        _fsm.AddState(new StuckState(this));
        _fsm.AddState(new LongDistanceState(this));
        _fsm.AddState(new KissingDoneState(this));
        _fsm.AddState(new DoneState(this));

        // Idle transitions
        _fsm.AddTransition(State.Idle, State.Idle, () => Context.Ball.State.Velocity.Length() > 100f);
        _fsm.AddTransition(State.Idle, State.Stuck,
            () => CalcMinBallWallDist(out _) < 150f || Context.Knowledge.BallInGoal());
        _fsm.AddTransition(State.Idle, State.LongDistance,
            () => Vector2.Distance(Context.Ball.State.Position, Context.Referee.DesignatedPosition()) > 4000f);
        _fsm.AddTransition(State.Idle, State.KissInit,
            () => Vector2.Distance(Context.Ball.State.Position, Context.Referee.DesignatedPosition()) > 100.0f);
        _fsm.AddTransition(State.Idle, State.Done, () => true); // Fallback to Done if none of the above

        // KissInit transitions
        _fsm.AddTransition(State.KissInit, State.Idle, () => Context.Ball.State.Velocity.Length() >= 100f);
        _fsm.AddTransition(State.KissInit, State.Stuck, () =>
        {
            GenerateKissPoints(BPKissInitDistance, out var ballPlacer1Pos, out var ballPlacer2Pos);
            return !Context.Field.FieldAccessArea().Inside(ballPlacer1Pos) ||
                   !Context.Field.FieldAccessArea().Inside(ballPlacer2Pos);
        });
        _fsm.AddTransition(State.KissInit, State.Kissing, () => AreRobotsAtKissPoints(BPKissInitDistance),
            BPStateDelay);

        // KissTouch transitions
        _fsm.AddTransition(State.KissTouch, State.Idle, () => Context.Ball.State.Velocity.Length() >= 100f);
        _fsm.AddTransition(State.KissTouch, State.Kissing, () => AreRobotsAtKissPoints(75f), BPStateDelay);

        // Kissing transitions
        _fsm.AddTransition(State.Kissing, State.Idle, () => IsPlaceBallLost());
        _fsm.AddTransition(State.Kissing, State.KissingDone, () =>
        {
            var finalBallPos = Context.Referee.DesignatedPosition();
            var ballPlacer1 = GetPlacer(1);
            var ballPlacer2 = GetPlacer(2);
            if (ballPlacer1 == null || ballPlacer2 == null) return false;
            var middle = (ballPlacer1.Position + ballPlacer2.Position) / 2.0f;
            return Vector2.Distance(middle, finalBallPos) < 100f;
        }, BPStateDelay);

        // Stuck transitions
        _fsm.AddTransition(State.Stuck, State.Idle,
            () => CalcMinBallWallDist(out _) >= 150f && !_forceStuck && !Context.Knowledge.BallInGoal());

        // LongDistance transitions
        _fsm.AddTransition(State.LongDistance, State.Idle,
            () => Context.Ball.State.Velocity.Length() < 100f &&
                  Vector2.Distance(Context.Ball.State.Position, Context.Referee.DesignatedPosition()) < 1000f);

        // KissingDone transitions
        _fsm.AddTransition(State.KissingDone, State.Done, () =>
        {
            var ballPlacer1 = GetPlacer(1);
            var ballPlacer2 = GetPlacer(2);
            return ballPlacer1 != null && Vector2.Distance(ballPlacer1.Position, _ballPlacer1FinalPos) < 20f &&
                   ballPlacer2 != null && Vector2.Distance(ballPlacer2.Position, _ballPlacer2FinalPos) < 20f;
        }, BPStateDelay);

        // Done transitions
        _fsm.AddTransition(State.Done, State.Idle, () =>
        {
            var ballPlacer1 = GetPlacer(1);
            var ballPlacer2 = GetPlacer(2);
            var now = Timestamp.Now;
            var finalBallPos = Context.Referee.DesignatedPosition();

            return (now - Context.Ball.LastVisibleTimestamp < DeltaTime.FromSeconds(1) &&
                    Vector2.Distance(Context.Ball.State.Position, finalBallPos) > 80.0) || ballPlacer1 != null &&
                Vector2.Distance(ballPlacer1.Position, _ballPlacer1FinalPos) < 20f &&
                ballPlacer2 != null && Vector2.Distance(ballPlacer2.Position, _ballPlacer2FinalPos) < 20f;
        }, BPStateDelay);
    }

    public ISkill? Tick()
    {
        _fsm.DrawDebug(Robot);
        return _fsm.Tick();
    }

    private static Robot.Robot? GetPlacer(int id) => Context.FindAssignedRobot<Role.BallPlacer>(p => p.PlacerId == id);

    private static bool IsPlaceBallLost()
    {
        var ballPlacer1 = GetPlacer(1);
        var ballPlacer2 = GetPlacer(2);

        if (ballPlacer1 == null || ballPlacer2 == null) return true;
        var robotsSegment = new LineSegment { Start = ballPlacer1.Position, End = ballPlacer2.Position };
        var dist = robotsSegment.Distance(Context.Ball.State.Position);

        return dist > 150;
    }

    private static bool AreRobotsAtKissPoints(float distance)
    {
        GenerateKissPoints(distance, out var ballPlacer1Pos, out var ballPlacer2Pos);
        var ballPlacer1 = GetPlacer(1);
        var ballPlacer2 = GetPlacer(2);
        return ballPlacer1 != null && Vector2.Distance(ballPlacer1.Position, ballPlacer1Pos) < 20.0f &&
               ballPlacer2 != null && Vector2.Distance(ballPlacer2.Position, ballPlacer2Pos) < 20.0f;
    }

    private static void GenerateKissPoints(float distance, out Vector2 ballPlacer1Pos, out Vector2 ballPlacer2Pos)
    {
        var ballPos = Context.Ball.State.Position;
        var finalBallPos = Context.Referee.DesignatedPosition();
        var dir = Vector2.Normalize(finalBallPos - ballPos);
        ballPlacer1Pos = ballPos + dir * distance; // Front
        ballPlacer2Pos = ballPos - dir * distance; // Back
    }

    private static float CalcMinBallWallDist(out Vector2 closestPoint)
    {
        float minDist = float.MaxValue;
        closestPoint = Vector2.Zero;

        var f = Context.Field;
        var b = Context.Field.BoundaryWidth;
        var bg = f.BoundaryWidthGoalLine;

        var fieldTopLeft = new Vector2(-f.Width - bg, f.Height + b);
        var fieldTopRight = new Vector2(f.Width + bg, f.Height + b);
        var fieldBottomLeft = new Vector2(-f.Width - bg, -f.Height - b);
        var fieldBottomRight = new Vector2(f.Width + bg, -f.Height - b);

        var leftGoalTopLeft = new Vector2(-f.Width - bg, f.GoalWidth / 2f);
        var leftGoalTopRight = new Vector2(-f.Width, f.GoalWidth / 2f);
        var leftGoalBottomLeft = new Vector2(-f.Width - bg, -f.GoalWidth / 2f);
        var leftGoalBottomRight = new Vector2(-f.Width, -f.GoalWidth / 2f);

        var rightGoalTopLeft = new Vector2(f.Width, f.GoalWidth / 2f);
        var rightGoalTopRight = new Vector2(f.Width + bg, f.GoalWidth / 2f);
        var rightGoalBottomLeft = new Vector2(f.Width, -f.GoalWidth / 2f);
        var rightGoalBottomRight = new Vector2(f.Width + bg, -f.GoalWidth / 2f);


        var walls = new[]
        {
            new LineSegment { Start = fieldTopLeft, End = fieldTopRight },
            new LineSegment { Start = fieldBottomLeft, End = fieldBottomRight },
            new LineSegment { Start = fieldTopLeft, End = fieldBottomLeft },
            new LineSegment { Start = fieldTopRight, End = fieldBottomRight },
            new LineSegment { Start = leftGoalTopLeft, End = leftGoalTopRight },
            new LineSegment { Start = leftGoalBottomLeft, End = leftGoalBottomRight },
            new LineSegment { Start = rightGoalTopLeft, End = rightGoalTopRight },
            new LineSegment { Start = rightGoalBottomLeft, End = rightGoalBottomRight },
            new LineSegment { Start = leftGoalBottomLeft, End = leftGoalTopLeft },
            new LineSegment { Start = rightGoalBottomRight, End = rightGoalTopRight }
        };

        var ballPos = Context.Ball.State.Position;
        foreach (var wall in walls)
        {
            var cp = wall.ClosestPoint(ballPos);
            var d = Vector2.Distance(cp, ballPos);
            if (d < minDist)
            {
                minDist = d;
                closestPoint = cp;
            }
        }

        return minDist;
    }

    private sealed class IdleState(BallPlacement tactic) : IState<State>
    {
        public State Type => State.Idle;

        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public ISkill? Tick()
        {
            tactic.Robot.Halt();
            return null;
        }
    }

    private sealed class KissInitState(BallPlacement tactic) : IState<State>
    {
        public State Type => State.KissInit;

        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public ISkill? Tick()
        {
            GenerateKissPoints(BPKissInitDistance, out var ballPlacer1Pos, out var ballPlacer2Pos);
            if (!Context.Field.FieldAccessArea().Inside(ballPlacer1Pos) ||
                !Context.Field.FieldAccessArea().Inside(ballPlacer2Pos))
            {
                tactic._forceStuck = true;
            }

            var target = tactic.PlacerId == 1 ? ballPlacer1Pos : ballPlacer2Pos;
            tactic.Robot.Face(Context.Ball.State.Position);
            return new GoToPoint
            {
                Target = target,
                VelocityProfile = VelocityProfile.Mamooli,
                NavigationFlags = NavigationFlags.BallSmallObstacle
            };
        }
    }

    private sealed class KissTouchState(BallPlacement tactic) : IState<State>
    {
        public State Type => State.KissTouch;

        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public ISkill? Tick()
        {
            GenerateKissPoints(75f, out var ballPlacer1Pos, out var ballPlacer2Pos);
            var target = tactic.PlacerId == 1 ? ballPlacer1Pos : ballPlacer2Pos;
            tactic.Robot.Face(Context.Ball.State.Position);
            return new GoToPoint
            {
                Target = target,
                VelocityProfile = VelocityProfile.Sooski,
                NavigationFlags = NavigationFlags.NoObstacles
            };
        }
    }

    private sealed class KissingState(BallPlacement tactic) : IState<State>
    {
        public State Type => State.Kissing;

        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public ISkill? Tick()
        {
            var finalBallPos = Context.Referee.DesignatedPosition();
            var ballPlacer1 = GetPlacer(1);
            var ballPlacer2 = GetPlacer(2);

            if (ballPlacer1 == null || ballPlacer2 == null) return new Halt();
            var direction = Vector2.Normalize((ballPlacer1.Position + ballPlacer2.Position) / 2.0f - finalBallPos);

            if (ballPlacer1 != null && ballPlacer2 != null)
            {
                //var direction = Vector2.Normalize((ballPlacer1.Position + ballPlacer2.Position) / 2.0f - finalBallPos);
                tactic._ballPlacer2FinalPos = finalBallPos +
                                              direction *
                                              BPKissInitDistance;
                tactic._ballPlacer1FinalPos = finalBallPos -
                                              direction *
                                              BPKissInitDistance;
            }

            //var axis = Vector2.Normalize((ballPlacer1?.Position ?? tactic.Robot.Position) - finalBallPos);
            var kissTouch2 = finalBallPos + direction * 75f;
            var kissTouch1 = finalBallPos - direction * 75f;
            return new GoToPoint
            {
                Target = tactic.PlacerId == 1 ? kissTouch1 : kissTouch2,
                VelocityProfile = VelocityProfile.Sooski,
                NavigationFlags = NavigationFlags.NoObstacles | NavigationFlags.NoBallObstacle
            };
        }
    }

    private sealed class StuckState(BallPlacement tactic) : IState<State>
    {
        public State Type => State.Stuck;
        public Timestamp? EnterTime { get; set; }
        private CircleBall? _circleBall;

        public void Enter()
        {
            EnterTime = Context.Time;
            _circleBall = null;
        }

        public void Exit()
        {
            _circleBall = null;
        }

        public ISkill? Tick()
        {
            var ballPos = Context.Ball.State.Position;
            var timeInState = Context.Time - (EnterTime ?? Context.Time);

            if (Context.Ball.State.Velocity.Length() > 20.0f)
            {
                tactic._forceStuck = false;
            }

            if (tactic.PlacerId == 2)
            {
                if (false && timeInState.Seconds >= 1.6f) // 100 frames
                {
                    CalcMinBallWallDist(out var wallPt);
                    var esc = ballPos + Vector2.Normalize(ballPos - wallPt) * 1000f;
                    tactic.Robot.TargetAngle = esc.AngleWith(ballPos);
                    return new GoToPoint { Target = esc, NavigationFlags = NavigationFlags.NoObstacles };
                }
                else
                {
                    CalcMinBallWallDist(out var wallPt);
                    var targetAngle = (ballPos - wallPt).ToAngle() + Angle.FromDeg(60f);
                    _circleBall ??= new CircleBall(tactic.Robot)
                    {
                        TargetAngle = targetAngle
                    };
                    _circleBall.TargetAngle = targetAngle;
                    _circleBall.ShootPower = 3000f;
                    _circleBall.ChipPower = 0f;
                    return _circleBall.Tick();
                }
            }
            else
            {
                tactic.Robot.TargetAngle = tactic.Robot.Position.AngleWith(ballPos);
                tactic.Robot.Halt();
                return null;
            }
        }
    }

    private sealed class LongDistanceState(BallPlacement tactic) : IState<State>
    {
        public State Type => State.LongDistance;
        private CircleBall? _circleBall;

        public void Enter()
        {
            _circleBall = null;
        }

        public void Exit()
        {
            _circleBall = null;
        }

        public ISkill? Tick()
        {
            var finalBallPos = Context.Referee.DesignatedPosition();
            var ballPos = Context.Ball.State.Position;
            var receiverPos = finalBallPos + Vector2.Normalize(finalBallPos - ballPos) * Context.Field.RobotRadius;
            if (tactic.PlacerId == 1)
            {
                return new WaitForBall { StaticPosition = receiverPos };
            }
            else
            {
                var ballPlacer1 = GetPlacer(1);
                var targetAngle = (ballPos - receiverPos).ToAngle();
                var shootPower = ballPlacer1 != null && Vector2.Distance(ballPlacer1.Position, receiverPos) < 100f
                    ? 2000f
                    : 0f;

                _circleBall ??= new CircleBall(tactic.Robot)
                {
                    TargetAngle = targetAngle
                };
                _circleBall.TargetAngle = targetAngle;
                _circleBall.ShootPower = shootPower;
                _circleBall.ChipPower = 0f;
                return _circleBall.Tick();
            }
        }
    }

    private sealed class KissingDoneState(BallPlacement tactic) : IState<State>
    {
        public State Type => State.KissingDone;

        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public ISkill? Tick()
        {
            var finalBallPos = Context.Referee.DesignatedPosition();
            var ballPlacer1 = GetPlacer(1);
            var ballPlacer2 = GetPlacer(2);
            if (ballPlacer1 != null && ballPlacer2 != null)
            {
                tactic._ballPlacer1FinalPos =
                    finalBallPos + Vector2.Normalize(ballPlacer1.Position - finalBallPos) * 600.0f;
                tactic._ballPlacer2FinalPos =
                    finalBallPos + Vector2.Normalize(ballPlacer2.Position - finalBallPos) * 800.0f;
            }

            var fTarget = tactic.PlacerId == 1 ? tactic._ballPlacer1FinalPos : tactic._ballPlacer2FinalPos;
            tactic.Robot.TargetAngle = fTarget.AngleWith(finalBallPos);
            return new GoToPoint
            {
                Target = fTarget,
                VelocityProfile = VelocityProfile.Sooski,
                NavigationFlags = NavigationFlags.BallSmallObstacle
            };
        }
    }

    private sealed class DoneState(BallPlacement tactic) : IState<State>
    {
        public State Type => State.Done;

        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public ISkill? Tick()
        {
            var finalBallPos = Context.Referee.DesignatedPosition();
            var fTarget = tactic.PlacerId == 1 ? tactic._ballPlacer1FinalPos : tactic._ballPlacer2FinalPos;
            tactic.Robot.Face(finalBallPos);
            return new GoToPoint
            {
                Target = fTarget,
                VelocityProfile = VelocityProfile.Aroom,
                NavigationFlags = NavigationFlags.BallObstacle
            };
        }
    }
}