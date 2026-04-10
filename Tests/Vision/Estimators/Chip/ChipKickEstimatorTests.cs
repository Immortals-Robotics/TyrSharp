using System.Numerics;
using Tyr.Common.Extensions;
using Tyr.Common.Time;
using Tyr.Vision.Estimators.Chip;

namespace Tyr.Tests.Vision.Estimators.Chip;

public class ChipKickEstimatorTests
{
    [Fact]
    public void AddRawBall_DoesNotFitBeforeMinimumRecordCount()
    {
        var cameraCalibrations = ChipTestHelper.CreateCalibrations((1, new Vector3(1200f, -1800f, 2600f)));
        var kickPosition = new Vector2(100f, -40f);
        var kickVelocity = new Vector3(2200f, 700f, 1800f);
        var records = ChipTestHelper.CreateChipTrajectoryRecords(
            kickPosition,
            kickVelocity,
            cameraCalibrations,
            2.0,
            (1, 0.03), (1, 0.06), (1, 0.09), (1, 0.12), (1, 0.15), (1, 0.18), (1, 0.21));

        var estimator = new ChipKickEstimator(
            cameraCalibrations,
            ChipTestHelper.CreateDetectedKick(
                records.Take(5).ToList(),
                kickPosition,
                Vector2.Normalize(kickVelocity.Xy()),
                kickTimestamp: Timestamp.FromSeconds(2.0)));

        Assert.Null(estimator.GetFitResult());

        estimator.AddRawBall(records[5]);
        estimator.AddRawBall(records[6]);

        Assert.Null(estimator.GetFitResult());
    }

    [Fact]
    public void AddRawBall_SwitchesFromLinearToNonlinearAfterFirstHop()
    {
        var cameraCalibrations = ChipTestHelper.CreateCalibrations((1, new Vector3(1200f, -1800f, 2600f)));
        var kickPosition = new Vector2(100f, -40f);
        var kickVelocity = new Vector3(2200f, 700f, 2000f);
        var records = ChipTestHelper.CreateChipTrajectoryRecords(
            kickPosition,
            kickVelocity,
            cameraCalibrations,
            2.0,
            (1, 0.03), (1, 0.06), (1, 0.09), (1, 0.12), (1, 0.15), (1, 0.18), (1, 0.21), (1, 0.24),
            (1, 0.30), (1, 0.36), (1, 0.42), (1, 0.48), (1, 0.54), (1, 0.60), (1, 0.66));

        var estimator = new ChipKickEstimator(
            cameraCalibrations,
            ChipTestHelper.CreateDetectedKick(
                records.Take(7).ToList(),
                kickPosition,
                Vector2.Normalize(kickVelocity.Xy()),
                kickTimestamp: Timestamp.FromSeconds(2.0)));

        estimator.AddRawBall(records[7]);
        Assert.NotNull(estimator.GetFitResult());
        Assert.Equal(nameof(ChipKickLinearTimeOffsetFitter), estimator.GetFitResult()!.SolverName);

        for (var i = 8; i < records.Count; i++)
        {
            estimator.AddRawBall(records[i]);
        }

        Assert.NotNull(estimator.GetFitResult());
        Assert.Equal(nameof(ChipKickNonlinearVelocityFitter), estimator.GetFitResult()!.SolverName);
    }
}
