using System.Numerics;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Detection;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Estimators;
using Tyr.Vision.Trajectory;
using RawBall = Tyr.Vision.Data.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;

namespace Tyr.Tests.Vision.Estimators;

public class KickEstimatorsTests
{
    [Fact]
    public void Process_SpawnsFlatEstimatorAndReturnsBestFit()
    {
        var kickPosition = new Vector2(200f, -50f);
        var kickDirection = Vector2.Normalize(new Vector2(3f, 4f));
        var records = CreateFlatTrajectoryRecords(kickPosition, kickDirection * 1800f, 2.0,
            0.0, 0.03, 0.06, 0.09, 0.12, 0.16, 0.20, 0.24, 0.30);

        var kickEstimators = new KickEstimators();

        var result = kickEstimators.Process(
            CreateDetectedKick(records, kickPosition, kickDirection),
            null,
            [],
            records[^1].CaptureTimestamp,
            CreateFilteredBall(records[0]));

        Assert.NotNull(result.BestFitResult);
        Assert.Single(kickEstimators.ActiveEstimators);
        Assert.Single(kickEstimators.KickEventHistory);
        Assert.True(Vector2.Distance(result.BestFitResult!.KickPosition, kickPosition) < 5f);
    }

    [Fact]
    public void Process_ReusesEstimator_WhenNewKickMatchesCurrentFit()
    {
        var kickPosition = new Vector2(200f, -50f);
        var kickDirection = Vector2.Normalize(new Vector2(3f, 4f));
        var records = CreateFlatTrajectoryRecords(kickPosition, kickDirection * 1800f, 2.0,
            0.0, 0.03, 0.06, 0.09, 0.12, 0.16, 0.20, 0.24, 0.30);

        var kickEstimators = new KickEstimators();
        kickEstimators.Process(
            CreateDetectedKick(records, kickPosition, kickDirection),
            null,
            [],
            records[^1].CaptureTimestamp,
            CreateFilteredBall(records[0]));
        var firstEstimator = Assert.Single(kickEstimators.ActiveEstimators);

        var nearKickPosition = kickPosition + new Vector2(40f, -20f);
        var result = kickEstimators.Process(
            CreateDetectedKick(records, nearKickPosition, kickDirection),
            null,
            [],
            records[^1].CaptureTimestamp + DeltaTime.FromSeconds(0.02),
            CreateFilteredBall(records[0]));

        Assert.NotNull(result.BestFitResult);
        Assert.Same(firstEstimator, Assert.Single(kickEstimators.ActiveEstimators));
    }

    [Fact]
    public void Process_ReplacesEstimator_WhenKickDeviationIsLarge()
    {
        var kickPosition = new Vector2(200f, -50f);
        var kickDirection = Vector2.Normalize(new Vector2(3f, 4f));
        var records = CreateFlatTrajectoryRecords(kickPosition, kickDirection * 1800f, 2.0,
            0.0, 0.03, 0.06, 0.09, 0.12, 0.16, 0.20, 0.24, 0.30);

        var kickEstimators = new KickEstimators();
        kickEstimators.Process(
            CreateDetectedKick(records, kickPosition, kickDirection),
            null,
            [],
            records[^1].CaptureTimestamp,
            CreateFilteredBall(records[0]));
        var firstEstimator = Assert.Single(kickEstimators.ActiveEstimators);

        var farKickPosition = kickPosition + new Vector2(800f, 0f);
        var farRecords = CreateFlatTrajectoryRecords(farKickPosition, kickDirection * 1800f, 3.0,
            0.0, 0.03, 0.06, 0.09, 0.12, 0.16, 0.20, 0.24, 0.30);
        kickEstimators.Process(
            CreateDetectedKick(farRecords, farKickPosition, kickDirection),
            null,
            [],
            farRecords[^1].CaptureTimestamp,
            CreateFilteredBall(farRecords[0]));

        Assert.NotSame(firstEstimator, Assert.Single(kickEstimators.ActiveEstimators));
    }

    [Fact]
    public void Process_RemovesFinishedEstimator_WhenEstimatorFinishes()
    {
        var kickPosition = new Vector2(200f, -50f);
        var kickDirection = Vector2.Normalize(new Vector2(3f, 4f));
        var records = CreateFlatTrajectoryRecords(kickPosition, kickDirection * 1800f, 5.0,
            0.0, 0.03, 0.06, 0.09, 0.12, 0.16, 0.20, 0.24, 0.30, 0.40, 0.55, 0.75,
            0.90, 1.05, 1.20, 1.35, 1.50, 1.65, 1.80, 1.95);

        var kickEstimators = new KickEstimators();
        var initial = kickEstimators.Process(
            CreateDetectedKick(records, kickPosition, kickDirection),
            null,
            [],
            records[^1].CaptureTimestamp,
            CreateFilteredBall(records[0]));
        Assert.NotNull(initial.BestFitResult);
        var fit = initial.BestFitResult!;

        var finishTimestamp = records[0].CaptureTimestamp + DeltaTime.FromSeconds(0.25);
        var finishPosition = fit.GetState(finishTimestamp).Position;
        var result = kickEstimators.Process(
            null,
            null,
            [CreateRobot(finishPosition)],
            finishTimestamp,
            CreateFilteredBall(records[0]));

        Assert.Null(result.BestFitResult);
        Assert.Empty(kickEstimators.ActiveEstimators);
    }

    private static FilteredRobot CreateRobot(Vector2 position)
    {
        return new FilteredRobot
        {
            Id = new RobotId { Id = 1 },
            Timestamp = Timestamp.Zero,
            State = new RobotState
            {
                Position = position,
                Velocity = Vector2.Zero,
                Angle = Angle.Zero,
                AngularVelocity = Angle.Zero
            },
            Quality = 1f
        };
    }

    private static FilteredBall CreateFilteredBall(RawBall record)
    {
        return new FilteredBall
        {
            Timestamp = record.CaptureTimestamp,
            LastVisibleTimestamp = record.CaptureTimestamp,
            State = new BallState
            {
                Position3D = new Vector3(record.Detection.Position.X, record.Detection.Position.Y, 0f),
                Velocity = Vector3.Zero,
                Acceleration = Vector3.Zero,
                SpinRadians = Vector2.Zero
            }
        };
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
