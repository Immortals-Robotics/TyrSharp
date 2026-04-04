using System.Numerics;
using Tyr.Common.Data.Ssl.Vision.Detection;
using Tyr.Vision.Estimators.Direct;
using RawBall = Tyr.Vision.Data.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;

namespace Tyr.Tests.Vision.Estimators.Direct;

public class StraightKickFixedDirectionLinearFitterTests
{
    private const float SlideAcceleration = -3000f;

    [Fact]
    public void Solve_ReturnsStateAtFirstRecord_ForSlidingTrajectory()
    {
        var expectedPosition = new Vector2(200f, -50f);
        var expectedDirection = Vector2.Normalize(new Vector2(3f, 4f));
        var expectedSpeed = 1800f;
        var firstTimestampSeconds = 5.0;

        var records = CreateSlidingRecords(
            expectedPosition,
            expectedDirection,
            expectedSpeed,
            firstTimestampSeconds,
            [0.0, 0.02, 0.04, 0.06, 0.08]);

        var solved = StraightKickFixedDirectionLinearFitter.Solve(records);

        Assert.NotNull(solved);
        Assert.True(Vector2.Distance(solved!.Position, expectedPosition) < 0.01f);
        Assert.True(Vector2.Distance(new Vector2(solved.Velocity.X, solved.Velocity.Y), expectedDirection * expectedSpeed) <
                    0.05f);
        Assert.Equal(0f, solved.Velocity.Z);
        Assert.Equal(records[0].CaptureTimestamp, solved.Timestamp);
        Assert.Equal(Vector2.Zero, solved.Spin);
        Assert.Equal(nameof(StraightKickFixedDirectionLinearFitter), solved.Name);
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

        var solved = StraightKickFixedDirectionLinearFitter.Solve(records);

        Assert.Null(solved);
    }

    [Fact]
    public void Solve_ReturnsNull_WhenNoRecordsAreProvided()
    {
        Assert.Null(StraightKickFixedDirectionLinearFitter.Solve([]));
    }

    private static List<RawBall> CreateSlidingRecords(
        Vector2 initialPosition,
        Vector2 direction,
        float initialSpeed,
        double firstTimestampSeconds,
        params double[] sampleOffsetsSeconds)
    {
        var normalizedDirection = Vector2.Normalize(direction);

        return sampleOffsetsSeconds
            .Select((offset, index) =>
            {
                var t = (float)offset;
                var distance = (initialSpeed * t) + (0.5f * SlideAcceleration * t * t);
                var position = initialPosition + normalizedDirection * distance;
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
