using System.Numerics;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Db;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Time;
using Tyr.Soccer.Navigation.Trajectory;
using Command = Tyr.Common.Debug.Drawing.Command;

namespace Tyr.Tests.Soccer;

public sealed class TrajectoryUtilsTests
{
    [Fact]
    public void DrawTrajectory_WithTrajectory2D_PublishesSingleTrajectoryDrawable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TyrSharp.TrajectoryUtilsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var db = new DebugDb(directory).RegisterKnownTypes();
            DebugBus.SetDb(db);
            try
            {
                TrajectoryUtils.DrawTrajectory(CreateTrajectory2D(), Color.Black);
            }
            finally
            {
                DebugBus.SetDb(null);
            }

            var commands = db.QueryAll<Command>(Timestamp.Zero, Timestamp.MaxValue).ToArray();
            Assert.Single(commands);
            Assert.IsType<TrajectoryDrawable>(commands[0].Drawable);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DrawTrajectory_WithGenericTrajectory_FallsBackToLineSegments()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TyrSharp.TrajectoryUtilsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var db = new DebugDb(directory).RegisterKnownTypes();
            DebugBus.SetDb(db);
            try
            {
                TrajectoryUtils.DrawTrajectory(new LinearTrajectory(), Color.Black);
            }
            finally
            {
                DebugBus.SetDb(null);
            }

            var commands = db.QueryAll<Command>(Timestamp.Zero, Timestamp.MaxValue).ToArray();
            Assert.NotEmpty(commands);
            Assert.IsType<LineSegment>(commands[0].Drawable);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Trajectory2D CreateTrajectory2D()
    {
        var trajectoryX = new Trajectory1DPieced();
        trajectoryX.AddPiece(new Trajectory1DConstantAcc
        {
            StartPosition = 0f,
            StartVelocity = 1f,
            Acceleration = 0f,
            StartTime = 0f,
            EndTime = 1f,
        });

        var trajectoryY = new Trajectory1DPieced();
        trajectoryY.AddPiece(new Trajectory1DConstantAcc
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
