using System.Numerics;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Estimators.Direct;
using Tyr.Vision.Trajectory;
using RawBall = Tyr.Vision.Data.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;
using Ball = Tyr.Common.Data.Ssl.Vision.Detection.Ball;
using Frame = Tyr.Common.Data.Ssl.Vision.Detection.Frame;

namespace Tyr.Tests.Vision.Estimators.Direct;

public class StraightKickFixedDirectionNonlinearFitterTests
{
    [Fact]
    public void Solve_FitsFullFlatTrajectory_WithApproximateInitialGuess()
    {
        var expectedPosition = new Vector2(200f, -50f);
        var expectedDirection = Vector2.Normalize(new Vector2(3f, 4f));
        var expectedSpeed = 1800f;
        var firstTimestampSeconds = 5.0;

        var records = CreateFlatTrajectoryRecords(
            expectedPosition,
            expectedDirection * expectedSpeed,
            firstTimestampSeconds,
            [0.0, 0.05, 0.10, 0.15, 0.22, 0.35, 0.50]);

        var fitter = new StraightKickFixedDirectionNonlinearFitter(
            expectedPosition + new Vector2(75f, -40f),
            expectedSpeed - 350f);

        var solved = fitter.Solve(records);

        Assert.NotNull(solved);
        Assert.True(Vector2.Distance(solved!.Position, expectedPosition) < 0.5f);
        Assert.True(Vector2.Distance(new Vector2(solved.Velocity.X, solved.Velocity.Y), expectedDirection * expectedSpeed) <
                    5f);
        Assert.Equal(0f, solved.Velocity.Z);
        Assert.Equal(records[0].CaptureTimestamp, solved.Timestamp);
        Assert.Equal(Vector2.Zero, solved.Spin);
        Assert.Equal(nameof(StraightKickFixedDirectionNonlinearFitter), solved.Name);
    }

    [Fact]
    public void Solve_ReturnsNull_WhenDirectionCannotBeEstimated()
    {
        var records = new List<RawBall>
        {
            CreateRawBall(new Vector2(100f, 40f), 1.00, 1),
            CreateRawBall(new Vector2(100f, 40f), 1.02, 2),
            CreateRawBall(new Vector2(100f, 40f), 1.04, 3)
        };

        var fitter = new StraightKickFixedDirectionNonlinearFitter(new Vector2(100f, 40f), 1000f);

        Assert.Null(fitter.Solve(records));
    }

    [Fact]
    public void Solve_ReturnsNull_WhenNoRecordsAreProvided()
    {
        var fitter = new StraightKickFixedDirectionNonlinearFitter(Vector2.Zero, 0f);

        Assert.Null(fitter.Solve([]));
    }

    private static List<RawBall> CreateFlatTrajectoryRecords(
        Vector2 initialPosition,
        Vector2 initialVelocity,
        double firstTimestampSeconds,
        params double[] sampleOffsetsSeconds)
    {
        var trajectory = new BallFlat(new BallState
        {
            Position3D = new Vector3(initialPosition.X, initialPosition.Y, 0f),
            Velocity = new Vector3(initialVelocity.X, initialVelocity.Y, 0f),
            Acceleration = Vector3.Zero,
            SpinRadians = Vector2.Zero
        });

        return sampleOffsetsSeconds
            .Select((offset, index) =>
            {
                var position = trajectory.GetState(DeltaTime.FromSeconds(offset)).Position;
                return CreateRawBall(position, firstTimestampSeconds + offset, (uint)(index + 1));
            })
            .ToList();
    }

    private static RawBall CreateRawBall(Vector2 position, double captureTimeSeconds, uint frameNumber)
    {
        var frame = new Frame
        {
            CameraId = 1,
            FrameNumber = frameNumber,
            CaptureTimeSeconds = captureTimeSeconds,
            SentTimeSeconds = captureTimeSeconds
        };

        return new RawBall(new Ball
        {
            Confidence = 1f,
            X = position.X,
            Y = position.Y,
            PixelX = 0f,
            PixelY = 0f
        }, frame);
    }
}
