using System.Numerics;
using Tyr.Common.Time;
using Tyr.Vision.Estimators.Chip;

namespace Tyr.Tests.Vision.Estimators.Chip;

public class ChipKickLinearPositionTimeOffsetFitterTests
{
    [Fact]
    public void Solve_ImprovesKickPositionEstimate_WithTwoCameraSamples()
    {
        var cameraCalibrations = ChipTestHelper.CreateCalibrations(
            (1, new Vector3(1200f, -1800f, 2600f)),
            (2, new Vector3(-1400f, 1600f, 2550f)));
        var trueKickPosition = new Vector2(150f, -30f);
        var initialKickPosition = trueKickPosition + new Vector2(120f, -80f);
        var kickVelocity = new Vector3(2100f, 500f, 1700f);
        var records = ChipTestHelper.CreateChipTrajectoryRecords(
            trueKickPosition,
            kickVelocity,
            cameraCalibrations,
            2.0,
            (1, 0.03), (2, 0.05), (1, 0.07), (2, 0.09), (1, 0.11), (2, 0.13), (1, 0.15), (2, 0.17), (1, 0.19), (2, 0.21));

        var fixedPositionFitter = new ChipKickLinearTimeOffsetFitter(initialKickPosition, Timestamp.FromSeconds(2.0), cameraCalibrations);
        var positionFitter = new ChipKickLinearPositionTimeOffsetFitter(initialKickPosition, Timestamp.FromSeconds(2.0), cameraCalibrations);

        var fixedPosition = fixedPositionFitter.Solve(records);
        var estimatedPosition = positionFitter.Solve(records);

        Assert.NotNull(fixedPosition);
        Assert.NotNull(estimatedPosition);
        Assert.True(Vector2.Distance(estimatedPosition!.Position, trueKickPosition) < Vector2.Distance(fixedPosition!.Position, trueKickPosition));
        Assert.True(Vector2.Distance(estimatedPosition.Position, trueKickPosition) < 5f);
    }
}
