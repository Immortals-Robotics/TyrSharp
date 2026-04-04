using System.Numerics;
using Tyr.Common.Data.Ssl.Vision.Detection;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Calibrators;
using Tyr.Vision.Trajectory;
using RawBall = Tyr.Vision.Data.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;

namespace Tyr.Tests.Vision.Calibrators;

public class StraightBallModelCalibratorTests
{
    private const double ExpectedAccSlide = -3000.0;
    private const double ExpectedAccRoll = -260.0;
    private const double ExpectedKSwitch = 0.64;

    [Fact]
    public void Calibrate_RecoversStraightTwoPhaseParameters_ForSyntheticStraightKick()
    {
        var expectedPosition = new Vector2(200f, -50f);
        var expectedDirection = Vector2.Normalize(new Vector2(3f, 4f));
        var expectedSpeed = 1800f;
        var firstTimestampSeconds = 5.0;

        var records = CreateFlatTrajectoryRecords(
            expectedPosition,
            expectedDirection * expectedSpeed,
            firstTimestampSeconds,
            0.0, 0.03, 0.06, 0.09, 0.12, 0.16, 0.20, 0.24, 0.30, 0.40, 0.55, 0.75);

        var calibrator = new StraightBallModelCalibrator();

        var result = calibrator.Calibrate(records);

        Assert.NotNull(result);
        Assert.True(Vector2.Distance(result!.KickPosition, expectedPosition) < 0.01f);
        Assert.Equal(records[0].CaptureTimestamp, result.KickTimestamp);
        Assert.True(Vector2.Distance(
                        new Vector2(result.KickVelocity.X, result.KickVelocity.Y),
                        expectedDirection * expectedSpeed) < 150f);
        Assert.InRange(result.AccSlide, ExpectedAccSlide - 250.0, ExpectedAccSlide + 250.0);
        Assert.InRange(result.AccRoll, ExpectedAccRoll - 120.0, ExpectedAccRoll + 120.0);
        Assert.InRange(result.KSwitch, ExpectedKSwitch - 0.08, ExpectedKSwitch + 0.08);
        Assert.Equal(records.Count, result.SampleAmount);
    }

    [Fact]
    public void Calibrate_ReturnsNull_WhenDirectionCannotBeEstimated()
    {
        var records = new List<RawBall>
        {
            CreateRawBall(new Vector2(100f, 40f), 1.00, 1),
            CreateRawBall(new Vector2(100f, 40f), 1.02, 2),
            CreateRawBall(new Vector2(100f, 40f), 1.04, 3)
        };

        var calibrator = new StraightBallModelCalibrator();

        Assert.Null(calibrator.Calibrate(records));
    }

    [Fact]
    public void Calibrate_ReturnsNull_WhenNoVelocitySamplesCanBeComputed()
    {
        var records = new List<RawBall>
        {
            CreateRawBall(new Vector2(100f, 40f), 1.00, 1),
            CreateRawBall(new Vector2(120f, 60f), 1.00, 2)
        };

        var calibrator = new StraightBallModelCalibrator();

        Assert.Null(calibrator.Calibrate(records));
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
