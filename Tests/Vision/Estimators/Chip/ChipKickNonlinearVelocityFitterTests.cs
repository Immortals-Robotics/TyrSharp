using System.Numerics;
using Tyr.Common.Time;
using Tyr.Vision.Estimators.Chip;

namespace Tyr.Tests.Vision.Estimators.Chip;

public class ChipKickNonlinearVelocityFitterTests
{
    [Fact]
    public void Solve_RecoversVelocity_FromFullChipTrajectorySamples()
    {
        var cameraCalibrations = ChipTestHelper.CreateCalibrations(
            (1, new Vector3(1200f, -1800f, 2600f)),
            (2, new Vector3(-1400f, 1600f, 2550f)));
        var kickPosition = new Vector2(100f, -40f);
        var kickVelocity = new Vector3(2200f, 700f, 1800f);
        var records = ChipTestHelper.CreateChipTrajectoryRecords(
            kickPosition,
            kickVelocity,
            cameraCalibrations,
            2.0,
            (1, 0.03), (2, 0.06), (1, 0.09), (2, 0.12), (1, 0.15), (2, 0.18), (1, 0.21), (2, 0.24),
            (1, 0.30), (2, 0.36), (1, 0.42), (2, 0.48), (1, 0.54), (2, 0.60));
        var linearFitter = new ChipKickLinearTimeOffsetFitter(kickPosition, Timestamp.FromSeconds(2.0), cameraCalibrations);
        var initialEstimate = linearFitter.Solve(records.Take(8).ToList())!.Velocity;

        var fitter = new ChipKickNonlinearVelocityFitter(
            kickPosition,
            Timestamp.FromSeconds(2.0),
            cameraCalibrations,
            initialEstimate);

        var solved = fitter.Solve(records);

        Assert.NotNull(solved);
        Assert.True(
            Vector3.Distance(solved!.Velocity, kickVelocity) < 200f,
            $"Expected {kickVelocity}, got {solved.Velocity}");
    }
}
