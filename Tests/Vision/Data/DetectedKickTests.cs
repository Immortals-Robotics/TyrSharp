using System.Numerics;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Detection;
using Tyr.Common.Math;
using Tyr.Common.Vision.Data;
using Tyr.Tests.Common.Math;
using Tyr.Vision.Data;

namespace Tyr.Tests.Vision.Data;

public class DetectedKickTests
{
    [Fact]
    public void GetKickDirection_ReturnsDirectionOfTravel_ForNegativeSlopeKick()
    {
        var kick = CreateKick(
            CreateBall(0f, 0f, 0.00),
            CreateBall(10f, -10f, 0.01),
            CreateBall(20f, -20f, 0.02));

        var direction = kick.GetKickDirection();

        Assert.NotNull(direction);
        Assert.True(Vector2.Dot(direction!.Value, Vector2.Normalize(new Vector2(1f, -1f))) > 0.999f);
    }

    [Fact]
    public void GetKickDirection_ReturnsVerticalDirection_ForVerticalKick()
    {
        var kick = CreateKick(
            CreateBall(100f, 0f, 0.00),
            CreateBall(100f, -10f, 0.01),
            CreateBall(100f, -20f, 0.02));

        var direction = kick.GetKickDirection();

        Assert.NotNull(direction);
        Assert.True(MathF.Abs(direction!.Value.X) < 0.001f);
        Assert.True(direction.Value.Y < 0f);
    }

    [Fact]
    public void GetKickDirection_UsesChronologicalOrder_WhenSamplesAreUnsorted()
    {
        var kick = CreateKick(
            CreateBall(20f, 0f, 0.02),
            CreateBall(0f, 0f, 0.00),
            CreateBall(10f, 0f, 0.01));

        var direction = kick.GetKickDirection();

        Assert.NotNull(direction);
        Assert.True(Vector2.Dot(direction!.Value, Vector2.UnitX) > 0.999f);
    }

    [Fact]
    public void GetKickDirection_ReturnsDirectionOfTravel_ForNoisyKick()
    {
        var expectedDirection = Vector2.Normalize(new Vector2(1f, 0.6f));
        var kick = CreateKickFromSamples(
            LineTestData.CreateNoisySamples(
                pointOnLine: new Vector2(-20f, 10f),
                direction: expectedDirection,
                count: 24,
                spacing: 12f,
                tangentNoiseStdDev: 0.8f,
                normalNoiseStdDev: 3f,
                seed: 61),
            timeStepSeconds: 0.01,
            reorder: false);

        var direction = kick.GetKickDirection();

        Assert.NotNull(direction);
        Assert.True(Vector2.Dot(direction!.Value, expectedDirection) > 0.998f);
    }

    [Fact]
    public void GetKickDirection_ReturnsDirectionOfTravel_ForNoisyNearVerticalKick_WhenSamplesAreUnsorted()
    {
        var expectedDirection = Vector2.Normalize(new Vector2(-0.04f, 1f));
        var kick = CreateKickFromSamples(
            LineTestData.CreateNoisySamples(
                pointOnLine: new Vector2(80f, -60f),
                direction: expectedDirection,
                count: 28,
                spacing: 10f,
                tangentNoiseStdDev: 0.8f,
                normalNoiseStdDev: 2.5f,
                seed: 67),
            timeStepSeconds: 0.01,
            reorder: true);

        var direction = kick.GetKickDirection();

        Assert.NotNull(direction);
        Assert.True(Vector2.Dot(direction!.Value, expectedDirection) > 0.998f);
    }

    [Fact]
    public void GetKickDirection_ReturnsNull_WhenAllSamplesCollapseToSamePoint()
    {
        var kick = CreateKick(
            CreateBall(10f, 10f, 0.00),
            CreateBall(10f, 10f, 0.01),
            CreateBall(10f, 10f, 0.02));

        Assert.Null(kick.GetKickDirection());
    }

    private static DetectedKick CreateKick(params MergedBall[] balls)
    {
        return new DetectedKick(
            new RobotId { Id = 1 },
            new RobotState { Position = Vector2.Zero, Angle = Angle.Zero },
            balls[0].Position,
            balls[0].Timestamp,
            false,
            balls.ToList());
    }

    private static DetectedKick CreateKickFromSamples(
        IReadOnlyList<Vector2> samples,
        double timeStepSeconds,
        bool reorder)
    {
        var balls = samples
            .Select((sample, index) => CreateBall(sample.X, sample.Y, index * timeStepSeconds))
            .ToList();

        if (reorder)
        {
            balls = balls
                .OrderBy(ball => ball.Position.X)
                .ThenByDescending(ball => ball.Timestamp.Nanoseconds)
                .ToList();
        }

        return CreateKick(balls.ToArray());
    }

    private static MergedBall CreateBall(float x, float y, double captureTimeSeconds)
    {
        var frame = new Frame
        {
            CameraId = 1,
            FrameNumber = (uint)(captureTimeSeconds * 1000),
            CaptureTimeSeconds = captureTimeSeconds,
            SentTimeSeconds = captureTimeSeconds
        };

        var rawBall = new RawDetection<Ball>(new Ball
        {
            Confidence = 1f,
            X = x,
            Y = y,
            PixelX = 0f,
            PixelY = 0f
        }, frame);

        var position = new Vector2(x, y);

        return new MergedBall
        {
            Position = position,
            RawPosition = position,
            Timestamp = rawBall.CaptureTimestamp,
            LatestRawBall = rawBall
        };
    }
}
