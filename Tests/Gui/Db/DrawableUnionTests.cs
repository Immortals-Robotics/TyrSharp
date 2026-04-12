using MemoryPack;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Soccer.Navigation.Trajectory;

namespace Tyr.Tests.Gui.Db;

public sealed class DrawableUnionTests
{
    [Fact]
    public void DrawingCommand_RoundTripsCommonDrawable()
    {
        var command = new Command
        {
            Drawable = new LineSegment
            {
                Start = new(1, 2),
                End = new(3, 4),
            },
            Color = Color.Red,
            Options = Options.Outline(12f),
        };

        var bytes = MemoryPackSerializer.Serialize(command);
        var restored = MemoryPackSerializer.Deserialize<Command>(bytes);

        var segment = Assert.IsType<LineSegment>(restored.Drawable);
        Assert.Equal(new(1, 2), segment.Start);
        Assert.Equal(new(3, 4), segment.End);
    }

    [Fact]
    public void DrawingCommand_RoundTripsSoccerTrajectoryDrawable()
    {
        var command = new Command
        {
            Drawable = new TrajectoryDrawable
            {
                Trajectory = CreateTrajectory2D(),
            },
            Color = Color.Blue300,
            Options = Options.Outline(8f),
        };

        var bytes = MemoryPackSerializer.Serialize(command);
        var restored = MemoryPackSerializer.Deserialize<Command>(bytes);

        var trajectory = Assert.IsType<TrajectoryDrawable>(restored.Drawable);
        Assert.Equal(2, trajectory.Trajectory.TrajectoryX.Count);
        Assert.Equal(new(2f, 3f), trajectory.Trajectory.GetPosition(1f));
    }

    [Fact]
    public void DrawableUnionRegistry_RejectsDuplicateTag()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DrawableUnionRegistry.Register(TrajectoryDrawableRegistration.Tag, typeof(TestDrawable)));

        Assert.Contains(TrajectoryDrawableRegistration.Tag.ToString(), ex.Message);
    }

    private partial record TestDrawable : IDrawable;

    private static Trajectory2D CreateTrajectory2D()
    {
        var trajectoryX = Trajectory1DPieced.Create(
            new Trajectory1DConstantAcc
            {
                StartPosition = 0f,
                StartVelocity = 2f,
                Acceleration = 0f,
                StartTime = 0f,
                EndTime = 1f,
            },
            new Trajectory1DConstantAcc
            {
                StartPosition = 2f,
                StartVelocity = 2f,
                Acceleration = 0f,
                StartTime = 1f,
                EndTime = 2f,
            });

        var trajectoryY = Trajectory1DPieced.Create(
            new Trajectory1DConstantAcc
            {
                StartPosition = 0f,
                StartVelocity = 3f,
                Acceleration = 0f,
                StartTime = 0f,
                EndTime = 1f,
            },
            new Trajectory1DConstantAcc
            {
                StartPosition = 3f,
                StartVelocity = 3f,
                Acceleration = 0f,
                StartTime = 1f,
                EndTime = 2f,
            });

        return new Trajectory2D
        {
            TrajectoryX = trajectoryX,
            TrajectoryY = trajectoryY,
        };
    }
}
