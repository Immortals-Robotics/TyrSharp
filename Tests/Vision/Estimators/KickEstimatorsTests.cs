using System.Numerics;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Detection;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Estimators;
using Tyr.Vision.Estimators.Chip;
using Tyr.Vision.Estimators.Direct;
using Tyr.Vision.Trajectory;
using RawBall = Tyr.Vision.Data.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;

namespace Tyr.Tests.Vision.Estimators;

public class KickEstimatorsTests
{
    [Fact]
    public void Process_SpawnsFlatAndChipEstimators_ForSlowDetection()
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
            ChipTestHelper.CreateFilteredBall(records[0]));

        Assert.NotNull(result.BestFitResult);
        Assert.Equal(2, kickEstimators.ActiveEstimators.Count);
        Assert.Single(kickEstimators.ActiveEstimators.OfType<DirectKickEstimator>());
        Assert.Single(kickEstimators.ActiveEstimators.OfType<ChipKickEstimator>());
        Assert.Single(kickEstimators.KickEventHistory);
        Assert.True(Vector2.Distance(result.BestFitResult!.KickPosition, kickPosition) < 5f);
    }

    [Fact]
    public void Process_FastDetectionSkipsChipEstimator()
    {
        var kickPosition = new Vector2(200f, -50f);
        var kickDirection = Vector2.Normalize(new Vector2(3f, 4f));
        var records = CreateFlatTrajectoryRecords(kickPosition, kickDirection * 1800f, 2.0,
            0.0, 0.03, 0.06, 0.09, 0.12, 0.16, 0.20, 0.24, 0.30);

        var kickEstimators = new KickEstimators();

        kickEstimators.Process(
            CreateDetectedKick(records, kickPosition, kickDirection, isFastDetection: true),
            null,
            [],
            records[^1].CaptureTimestamp,
            ChipTestHelper.CreateFilteredBall(records[0]));

        Assert.Single(kickEstimators.ActiveEstimators);
        Assert.Single(kickEstimators.ActiveEstimators.OfType<DirectKickEstimator>());
        Assert.Empty(kickEstimators.ActiveEstimators.OfType<ChipKickEstimator>());
    }

    [Fact]
    public void Process_ReusesFlatEstimator_WhenNewKickMatchesCurrentFit()
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
            ChipTestHelper.CreateFilteredBall(records[0]));
        var firstFlatEstimator = kickEstimators.ActiveEstimators.OfType<DirectKickEstimator>().Single();

        var nearKickPosition = kickPosition + new Vector2(40f, -20f);
        var result = kickEstimators.Process(
            CreateDetectedKick(records, nearKickPosition, kickDirection),
            null,
            [],
            records[^1].CaptureTimestamp + DeltaTime.FromSeconds(0.02),
            ChipTestHelper.CreateFilteredBall(records[0]));

        Assert.NotNull(result.BestFitResult);
        Assert.Same(firstFlatEstimator, kickEstimators.ActiveEstimators.OfType<DirectKickEstimator>().Single());
    }

    [Fact]
    public void Process_ReplacesFlatEstimator_WhenKickDeviationIsLarge()
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
            ChipTestHelper.CreateFilteredBall(records[0]));
        var firstFlatEstimator = kickEstimators.ActiveEstimators.OfType<DirectKickEstimator>().Single();

        var farKickPosition = kickPosition + new Vector2(800f, 0f);
        var farRecords = CreateFlatTrajectoryRecords(farKickPosition, kickDirection * 1800f, 3.0,
            0.0, 0.03, 0.06, 0.09, 0.12, 0.16, 0.20, 0.24, 0.30);
        kickEstimators.Process(
            CreateDetectedKick(farRecords, farKickPosition, kickDirection),
            null,
            [],
            farRecords[^1].CaptureTimestamp,
            ChipTestHelper.CreateFilteredBall(farRecords[0]));

        Assert.NotSame(firstFlatEstimator, kickEstimators.ActiveEstimators.OfType<DirectKickEstimator>().Single());
    }

    [Fact]
    public void Process_IgnoresKickEvent_WhenBestChipEstimatorIsStillAirborne()
    {
        var cameraCalibrations = ChipTestHelper.CreateCalibrations((1, new Vector3(1200f, -1800f, 2600f)));
        var kickPosition = new Vector2(100f, -40f);
        var kickVelocity = new Vector3(2200f, 700f, 2000f);
        var kickDirection = Vector2.Normalize(kickVelocity.Xy());
        var chipRecords = ChipTestHelper.CreateChipTrajectoryRecords(
            kickPosition,
            kickVelocity,
            cameraCalibrations,
            2.0,
            (1, 0.03), (1, 0.06), (1, 0.09), (1, 0.12), (1, 0.15), (1, 0.18), (1, 0.21), (1, 0.24), (1, 0.27));

        var kickEstimators = new KickEstimators();
        kickEstimators.Process(
            ChipTestHelper.CreateDetectedKick(
                chipRecords.Take(8).ToList(),
                kickPosition,
                kickDirection,
                kickTimestamp: Timestamp.FromSeconds(2.0)),
            null,
            [],
            cameraCalibrations,
            chipRecords[7].CaptureTimestamp,
            ChipTestHelper.CreateFilteredBall(chipRecords[0]));

        kickEstimators.Process(
            null,
            ChipTestHelper.CreateMergedBall(chipRecords[8]),
            [],
            cameraCalibrations,
            chipRecords[8].CaptureTimestamp,
            ChipTestHelper.CreateFilteredBall(chipRecords[0]));

        var firstFlatEstimator = kickEstimators.ActiveEstimators.OfType<DirectKickEstimator>().Single();
        var firstChipEstimator = kickEstimators.ActiveEstimators.OfType<ChipKickEstimator>().Single();
        Assert.Equal(KickEstimatorType.Chip, kickEstimators.LastBestEstimator?.Type);

        var farKickPosition = kickPosition + new Vector2(900f, 0f);
        var farKickDirection = Vector2.UnitY;
        var farRecords = CreateFlatTrajectoryRecords(farKickPosition, farKickDirection * 1800f, 3.0,
            0.0, 0.03, 0.06, 0.09, 0.12, 0.16, 0.20, 0.24, 0.30);

        kickEstimators.Process(
            CreateDetectedKick(farRecords, farKickPosition, farKickDirection),
            null,
            [],
            cameraCalibrations,
            chipRecords[8].CaptureTimestamp + DeltaTime.FromSeconds(0.05),
            ChipTestHelper.CreateFilteredBall(chipRecords[0]));

        Assert.Same(firstFlatEstimator, kickEstimators.ActiveEstimators.OfType<DirectKickEstimator>().Single());
        Assert.Same(firstChipEstimator, kickEstimators.ActiveEstimators.OfType<ChipKickEstimator>().Single());
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
            ChipTestHelper.CreateFilteredBall(records[0]));
        Assert.NotNull(initial.BestFitResult);
        var fit = initial.BestFitResult!;

        var finishTimestamp = records[0].CaptureTimestamp + DeltaTime.FromSeconds(0.25);
        var finishPosition = fit.GetState(finishTimestamp).Position;
        var result = kickEstimators.Process(
            null,
            null,
            [ChipTestHelper.CreateRobot(finishPosition)],
            finishTimestamp,
            ChipTestHelper.CreateFilteredBall(records[0]));

        Assert.Null(result.BestFitResult);
        Assert.Empty(kickEstimators.ActiveEstimators);
    }

    private static DetectedKick CreateDetectedKick(
        IReadOnlyList<RawBall> records,
        Vector2 kickPosition,
        Vector2 kickDirection,
        bool isFastDetection = false)
    {
        return ChipTestHelper.CreateDetectedKick(records, kickPosition, kickDirection, isFastDetection);
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
                return ChipTestHelper.CreateRawBall(1, position, firstTimestampSeconds + offset, (uint)(index + 1));
            })
            .ToList();
    }
}
