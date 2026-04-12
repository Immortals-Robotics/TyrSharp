using System.Numerics;
using Tyr.Common.Dataflow;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Soccer.Navigation.Trajectory;

namespace Tyr.Tests.Soccer;

public sealed class TrajectoryUtilsTests
{
    [Fact]
    public void DrawTrajectory_WithTrajectory2D_PublishesSingleTrajectoryDrawable()
    {
        using var subscriber = DebugBus.Subscribe<Command>(Mode.All);

        TrajectoryUtils.DrawTrajectory(CreateTrajectory2D(), Color.Black);

        Assert.True(subscriber.Reader.TryRead(out var command));
        Assert.IsType<TrajectoryDrawable>(command.Drawable);
        Assert.False(subscriber.Reader.TryRead(out _));
    }

    [Fact]
    public void DrawTrajectory_WithGenericTrajectory_FallsBackToLineSegments()
    {
        using var subscriber = DebugBus.Subscribe<Command>(Mode.All);

        TrajectoryUtils.DrawTrajectory(new LinearTrajectory(), Color.Black);

        Assert.True(subscriber.Reader.TryRead(out var first));
        Assert.IsType<LineSegment>(first.Drawable);
    }

    private static Trajectory2D CreateTrajectory2D()
    {
        var trajectoryX = Trajectory1DPieced.Create(new Trajectory1DConstantAcc
        {
            StartPosition = 0f,
            StartVelocity = 1f,
            Acceleration = 0f,
            StartTime = 0f,
            EndTime = 1f,
        });

        var trajectoryY = Trajectory1DPieced.Create(new Trajectory1DConstantAcc
        {
            StartPosition = 0f,
            StartVelocity = 2f,
            Acceleration = 0f,
            StartTime = 0f,
            EndTime = 1f,
        });

        return new Trajectory2D
        {
            TrajectoryX = trajectoryX,
            TrajectoryY = trajectoryY,
        };
    }

    private sealed class LinearTrajectory : ITrajectory<Vector2>
    {
        public Vector2 GetPosition(float t) => new(t, 2f * t);
        public Vector2 GetVelocity(float t) => new(1f, 2f);
        public Vector2 GetAcceleration(float t) => Vector2.Zero;
        public float StartTime => 0f;
        public float EndTime => 0.3f;
        public float Duration => EndTime - StartTime;
    }
}
