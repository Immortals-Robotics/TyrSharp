using System.Numerics;
using Tyr.Common.Time;
using Tyr.Vision.Estimators.Chip;
using RawBall = Tyr.Vision.Data.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;

namespace Tyr.Tests.Vision.Estimators.Chip;

public class ChipKickLinearTimeOffsetFitterTests
{
    [Fact]
    public void Solve_RecoversKickVelocity_ForSingleCameraFirstHopSamples()
    {
        var cameraCalibrations = ChipTestHelper.CreateCalibrations((1, new Vector3(1200f, -1800f, 2600f)));
        var kickPosition = new Vector2(100f, -40f);
        var kickVelocity = new Vector3(2200f, 700f, 1800f);
        var records = ChipTestHelper.CreateChipTrajectoryRecords(
            kickPosition,
            kickVelocity,
            cameraCalibrations,
            2.0,
            (1, 0.03), (1, 0.06), (1, 0.09), (1, 0.12), (1, 0.15), (1, 0.18), (1, 0.21), (1, 0.24));

        var fitter = new ChipKickLinearTimeOffsetFitter(kickPosition, Timestamp.FromSeconds(2.0), cameraCalibrations);

        var solved = fitter.Solve(records);

        Assert.NotNull(solved);
        Assert.True(Vector2.Distance(solved!.Position, kickPosition) < 0.001f);
        Assert.True(
            Vector3.Distance(solved.Velocity, kickVelocity) < 50f,
            $"Expected {kickVelocity}, got {solved.Velocity}");
    }

    [Fact]
    public void Solve_ReturnsNull_ForDegenerateSlowSamples()
    {
        var cameraCalibrations = ChipTestHelper.CreateCalibrations((1, new Vector3(1200f, -1800f, 2600f)));
        var records = new List<RawBall>
        {
            ChipTestHelper.CreateRawBall(1, new Vector2(100f, 50f), 2.00, 1)
        };

        var fitter = new ChipKickLinearTimeOffsetFitter(new Vector2(100f, 50f), Timestamp.FromSeconds(2.0), cameraCalibrations);

        Assert.Null(fitter.Solve(records));
    }
}
