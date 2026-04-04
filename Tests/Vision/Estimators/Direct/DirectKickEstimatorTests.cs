using System.Numerics;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Detection;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Calibrators;
using Tyr.Vision.Data;
using Tyr.Vision.Estimators.Direct;
using Tyr.Vision.Trajectory;
using RawBall = Tyr.Vision.Data.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;

namespace Tyr.Tests.Vision.Estimators.Direct;

public class DirectKickEstimatorTests
{
    [Fact]
    public void GetKickSpeed_ReturnsRegressionSlope_ForConstantSpeedSamples()
    {
        var kickPosition = new Vector2(50f, -20f);
        var kickDirection = Vector2.Normalize(new Vector2(4f, 3f));
        var kickSpeed = 1200f;

        var records = new List<RawBall>
        {
            CreateRawBall(kickPosition + (kickDirection * kickSpeed * 0.00f), 0.00, 1),
            CreateRawBall(kickPosition + (kickDirection * kickSpeed * 0.05f), 0.05, 2),
            CreateRawBall(kickPosition + (kickDirection * kickSpeed * 0.10f), 0.10, 3),
            CreateRawBall(kickPosition + (kickDirection * kickSpeed * 0.15f), 0.15, 4)
        };

        var estimatedSpeed = DirectKickEstimator.GetKickSpeed(records, kickPosition);

        Assert.InRange(estimatedSpeed, kickSpeed - 1f, kickSpeed + 1f);
    }

    [Fact]
    public void Constructor_ComputesBestFit_ForStraightKick()
    {
        var kickPosition = new Vector2(200f, -50f);
        var kickDirection = Vector2.Normalize(new Vector2(3f, 4f));
        var kickSpeed = 1800f;

        var records = CreateFlatTrajectoryRecords(
            kickPosition,
            kickDirection * kickSpeed,
            2.0,
            0.0, 0.03, 0.06, 0.09, 0.12, 0.16, 0.20, 0.24, 0.30);
        var estimator = new DirectKickEstimator(CreateDetectedKick(records, kickPosition, kickDirection));

        var fit = estimator.GetFitResult();

        Assert.NotNull(fit);
        Assert.Equal(2, estimator.ActiveSolvers.Count);
        Assert.Contains(estimator.ActiveSolvers, solver => solver.SolverName == nameof(StraightKickFixedDirectionLinearFitter));
        Assert.Contains(estimator.ActiveSolvers, solver => solver.SolverName == nameof(StraightKickFixedDirectionNonlinearFitter));
        Assert.True(Vector2.Distance(fit!.KickPosition, kickPosition) < 5f);
        Assert.True(Vector2.Distance(new Vector2(fit.KickVelocity.X, fit.KickVelocity.Y), kickDirection * kickSpeed) < 150f);
        Assert.True(fit.AvgDistance < 5.0);
    }

    [Fact]
    public void GetModelIdentifications_ReturnsStraightCalibration_ForStraightKick()
    {
        var kickPosition = new Vector2(200f, -50f);
        var kickDirection = Vector2.Normalize(new Vector2(3f, 4f));
        var kickSpeed = 1800f;

        var records = CreateFlatTrajectoryRecords(
            kickPosition,
            kickDirection * kickSpeed,
            5.0,
            0.0, 0.03, 0.06, 0.09, 0.12, 0.16, 0.20, 0.24, 0.30, 0.40, 0.55, 0.75,
            0.90, 1.05, 1.20, 1.35, 1.50, 1.65, 1.80, 1.95);
        var estimator = new DirectKickEstimator(CreateDetectedKick(records, kickPosition, kickDirection));

        var identifications = estimator.GetModelIdentifications();

        var straight = Assert.IsType<StraightBallModelCalibrator.StraightBallModelCalibration>(
            Assert.Single(identifications));
        Assert.True(Vector2.Distance(straight.KickPosition, kickPosition) < 0.01f);
        Assert.InRange(straight.AccSlide, -3250.0, -2750.0);
        Assert.InRange(straight.AccRoll, -380.0, -140.0);
        Assert.InRange(straight.KSwitch, 0.56, 0.72);
    }

    private static DetectedKick CreateDetectedKick(
        IReadOnlyList<RawBall> records,
        Vector2 kickPosition,
        Vector2 kickDirection)
    {
        var mergedBalls = records
            .Select(record => new MergedBall
            {
                Position = record.Detection.Position,
                RawPosition = record.Detection.Position,
                Timestamp = record.CaptureTimestamp,
                LatestRawBall = record
            })
            .ToList();

        return new DetectedKick(
            new RobotId { Id = 1 },
            new RobotState
            {
                Position = kickPosition - (kickDirection * 90f),
                Velocity = Vector2.Zero,
                Angle = Angle.FromVector(kickDirection),
                AngularVelocity = Angle.Zero
            },
            kickPosition,
            records[0].CaptureTimestamp,
            false,
            mergedBalls);
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
