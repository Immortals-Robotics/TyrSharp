using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Math;
using Tyr.Soccer.Navigation.Obstacle;
using Tyr.Soccer.Navigation.Planner;
using Tyr.Soccer.Navigation.Trajectory;

namespace Tyr.Soccer.Robot;

public partial class Robot
{
    [ConfigEntry("Run navigation as a job on a separate task")]
    private static bool NavigationJob { get; set; } = true;

    [ConfigEntry("Speed used when we're required by the referee to slow down (eg. stop)")]
    private static float SlowdownSpeed { get; set; } = 900f;

    [ConfigEntry(
        "below this speed we consider the collision \"ok\" even if it happens. We only break when above this speed, and slow down only to below this speed")]
    private static float BreakSpeedThreshold { get; set; } = 500.0f;

    public Trajectory2D Trajectory { get; private set; } = new();

    public Vector2 CurrentMotion =>
        Utils.ApproximatelyZero(Trajectory.Duration)
            ? Vector2.Zero
            : Trajectory.GetVelocity((float)Context.Timer.DeltaTime.Seconds);

    public bool Navigated { get; private set; }

    private readonly Map _obsMap = new();
    private readonly Planner _planner = new();
    private Task? _navigationTask;

    public void Navigate(Vector2 target, VelocityProfile profile, NavigationFlags flags = NavigationFlags.None)
    {
        if (!Seen) return;

        if (Navigated)
        {
            Log.ZLogWarning($"Robot {Id} is navigated more than once");
            WaitForNavigationJob();
        }

        Navigated = true;

        // this is only used for assignment, not navigation
        TargetPosition = target;

        if (NavigationJob)
        {
            _navigationTask = Task.Run(() => NavigateJob(target, profile, flags));
        }
        else
        {
            NavigateJob(target, profile, flags);
        }
    }

    public void WaitForNavigationJob()
    {
        _navigationTask?.Wait();
    }

    public float CalculateReachTime(Vector2 target, VelocityProfile profile)
    {
        var trajectory = TrajectoryBangBang.Make2D(Position, CurrentMotion, target, profile);
        return trajectory.Duration;
    }

    public void FullBrake(float accfactor = 1.0f)
    {
        var profile = VelocityProfile.Mamooli with
        {
            Acceleration = VelocityProfile.Mamooli.Acceleration * accfactor
        };

        var trajectory = Trajectory2DFullStop.Make(Position, CurrentMotion, profile);
        Move(trajectory);
    }

    private void Move(Trajectory2D trajectory)
    {
        TrajectoryUtils.DrawTrajectory(trajectory, Color.Black);
        Trajectory = trajectory;
    }

    private void NavigateJob(Vector2 target, VelocityProfile profile, NavigationFlags flags)
    {
        SetObstacles(flags);

        if (Context.Referee.ShouldSlowDown())
        {
            profile = profile with
            {
                Speed = SlowdownSpeed
            };
        }

        if (Vector2.Distance(Position, target) > 100.0f)
        {
            Draw.DrawPoint(target, Color.Black);
        }

        _planner.Map = _obsMap;
        _planner.Profile = profile;
        var intermediate = _planner.Plan(Position, CurrentMotion, target);

        var margin = Context.Field.BoundaryWidth - Context.RobotRadius;
        var maxX = Context.Field.Width + margin;
        var maxY = Context.Field.Height + margin;

        intermediate.X = MathF.Abs(intermediate.X) > maxX ? MathF.Sign(intermediate.X) * maxX : intermediate.X;
        intermediate.Y = MathF.Abs(intermediate.Y) > maxY ? MathF.Sign(intermediate.Y) * maxY : intermediate.Y;

        var trajectory = TrajectoryBangBang.Make2D(Position, CurrentMotion, intermediate, profile);

        if (!flags.HasFlag(NavigationFlags.NoBreak))
        {
            var stopProfile = VelocityProfile.Mamooli with
            {
                Acceleration = VelocityProfile.Mamooli.Acceleration * 2f
            };

            var extraSpeed = MathF.Max(0f, CurrentMotion.Length() - BreakSpeedThreshold);

            var collisionLookahead = 3.0f * extraSpeed / stopProfile.Acceleration;
            var collisionImminent = _obsMap.HasCollision(trajectory,
                collisionLookahead, 0.1f, Physicality.Physical).collided;

            if (collisionImminent)
            {
                Draw.DrawCircle(Position, Context.RobotRadius * 1.5f, Color.Red, Options.Outline(50f));
                trajectory = Trajectory2DFullStop.Make(Position, CurrentMotion, stopProfile);
            }
        }

        Move(trajectory);
    }
}