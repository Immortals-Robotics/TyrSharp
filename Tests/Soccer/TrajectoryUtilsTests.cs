using System.Numerics;
using Tyr.Common.Dataflow;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Drawing;
using Tyr.Soccer.Navigation.Trajectory;

namespace Tyr.Tests.Soccer;

public sealed class TrajectoryUtilsTests
{
    [Fact]
    public void DrawTrajectory_WithTrajectory2D_PublishesSingleTrajectoryDrawable()
    {
        using var subscriber = DebugBus.Subscribe<Trajectory2DDrawable>(Mode.All);

        TrajectoryUtils.DrawTrajectory(CreateTrajectory2D(), Color.Black);

        Assert.True(subscriber.Reader.TryRead(out var command));
        Assert.Equal(Color.Black, command.Color);
        Assert.False(subscriber.Reader.TryRead(out _));
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
}
