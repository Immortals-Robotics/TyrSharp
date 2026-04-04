using System.Numerics;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Detection;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Util;

namespace Tyr.Tests.Vision.Util;

public class KickValidatorTests
{
    [Fact]
    public void Validate_ReturnsTrue_ForConsistentKickSequence()
    {
        var validator = new KickValidator();
        var balls = new List<MergedBall>
        {
            CreateBall(new Vector2(120f, 0f), 1.00),
            CreateBall(new Vector2(150f, 0f), 1.02),
            CreateBall(new Vector2(180f, 0f), 1.04),
            CreateBall(new Vector2(210f, 0f), 1.06),
            CreateBall(new Vector2(240f, 0f), 1.08)
        };

        var robots = CreateRobotHistory(Vector2.Zero, Angle.Zero, balls.Count);

        Assert.True(validator.Validate(balls, robots));
    }

    [Fact]
    public void Validate_ReturnsFalse_WhenBallVelocityIsTooLow()
    {
        var validator = new KickValidator();
        var balls = new List<MergedBall>
        {
            CreateBall(new Vector2(120f, 0f), 1.00),
            CreateBall(new Vector2(150f, 0f), 1.20),
            CreateBall(new Vector2(180f, 0f), 1.40),
            CreateBall(new Vector2(210f, 0f), 1.60),
            CreateBall(new Vector2(240f, 0f), 1.80)
        };

        var robots = CreateRobotHistory(Vector2.Zero, Angle.Zero, balls.Count);

        Assert.False(validator.Validate(balls, robots));
    }

    [Fact]
    public void DirectionBacktrack_ReturnsKickPositionAndTimestamp_ForStraightKick()
    {
        var validator = new KickValidator();
        var expectedKickPosition = new Vector2(105f, 20f);
        var expectedKickTimestamp = Timestamp.FromSeconds(10.0);
        var balls = CreateKickSamples(
            expectedKickPosition,
            Vector2.UnitX,
            speedMmPerSecond: 1000f,
            kickTimeSeconds: 10.0,
            cameraId: 1,
            sampleOffsetsSeconds: [0.03, 0.06, 0.09, 0.12]);

        var backtrack = validator.DirectionBacktrack(balls, [CreateRobot(Vector2.Zero, Angle.Zero)]);

        Assert.NotNull(backtrack);
        Assert.True(Vector2.Distance(backtrack!.Value.Item2, expectedKickPosition) < 0.01f);
        Assert.InRange(
            Math.Abs(backtrack.Value.Item1.Nanoseconds - expectedKickTimestamp.Nanoseconds),
            0,
            1_000);
    }

    [Fact]
    public void DirectionBacktrack_PrefersCameraGroupThatMatchesRobotHeading()
    {
        var validator = new KickValidator();
        var expectedKickPosition = new Vector2(105f, 35f);
        var expectedKickTimestamp = Timestamp.FromSeconds(20.0);

        var offHeadingGroup = CreateKickSamples(
            new Vector2(105f, -45f),
            Vector2.Normalize(new Vector2(1f, 1f)),
            speedMmPerSecond: 800f,
            kickTimeSeconds: 18.0,
            cameraId: 1,
            sampleOffsetsSeconds: [0.02, 0.04, 0.06, 0.08]);

        var alignedGroup = CreateKickSamples(
            expectedKickPosition,
            Vector2.UnitX,
            speedMmPerSecond: 900f,
            kickTimeSeconds: 20.0,
            cameraId: 2,
            sampleOffsetsSeconds: [0.03, 0.05, 0.07, 0.09]);

        var balls = offHeadingGroup.Concat(alignedGroup).ToList();

        var backtrack = validator.DirectionBacktrack(balls, [CreateRobot(Vector2.Zero, Angle.Zero)]);

        Assert.NotNull(backtrack);
        Assert.True(Vector2.Distance(backtrack!.Value.Item2, expectedKickPosition) < 0.5f);
        Assert.InRange(
            Math.Abs(backtrack.Value.Item1.Nanoseconds - expectedKickTimestamp.Nanoseconds),
            0,
            1_000);
    }

    [Fact]
    public void DirectionBacktrack_ReturnsNull_WhenNoCameraGroupHasEnoughSamples()
    {
        var validator = new KickValidator();
        var balls = new List<MergedBall>
        {
            CreateBall(new Vector2(120f, 0f), 3.00),
            CreateBall(new Vector2(150f, 0f), 3.02)
        };

        var backtrack = validator.DirectionBacktrack(balls, [CreateRobot(Vector2.Zero, Angle.Zero)]);

        Assert.Null(backtrack);
    }

    private static List<MergedBall> CreateKickSamples(
        Vector2 kickPosition,
        Vector2 direction,
        float speedMmPerSecond,
        double kickTimeSeconds,
        uint cameraId,
        params double[] sampleOffsetsSeconds)
    {
        var normalizedDirection = Vector2.Normalize(direction);

        return sampleOffsetsSeconds
            .Select((offset, index) =>
            {
                var distance = speedMmPerSecond * (float)offset;
                var position = kickPosition + normalizedDirection * distance;
                return CreateBall(position, kickTimeSeconds + offset, cameraId, (uint)(index + 1));
            })
            .ToList();
    }

    private static List<FilteredRobot> CreateRobotHistory(Vector2 position, Angle angle, int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => CreateRobot(position, angle))
            .ToList();
    }

    private static FilteredRobot CreateRobot(Vector2 position, Angle angle)
    {
        return new FilteredRobot
        {
            Id = new RobotId { Id = 1 },
            Timestamp = Timestamp.Zero,
            State = new RobotState
            {
                Position = position,
                Velocity = Vector2.Zero,
                Angle = angle,
                AngularVelocity = Angle.Zero
            },
            Quality = 1f
        };
    }

    private static MergedBall CreateBall(Vector2 position, double captureTimeSeconds, uint cameraId = 1,
        uint frameNumber = 1)
    {
        var frame = new Frame
        {
            CameraId = cameraId,
            FrameNumber = frameNumber,
            CaptureTimeSeconds = captureTimeSeconds,
            SentTimeSeconds = captureTimeSeconds
        };

        var rawBall = new RawDetection<Ball>(new Ball
        {
            Confidence = 1f,
            X = position.X,
            Y = position.Y,
            PixelX = 0f,
            PixelY = 0f
        }, frame);

        return new MergedBall
        {
            Position = position,
            RawPosition = position,
            Timestamp = rawBall.CaptureTimestamp,
            LatestRawBall = rawBall
        };
    }
}
