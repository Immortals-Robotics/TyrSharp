using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Estimators;

namespace Tyr.Vision.Util;

public class BallHelpers
{
    private FastKickDetector FastKickDetector { get; } = new();
    private SlowKickDetector SlowKickDetector { get; } = new();

    public KickEstimators KickEstimators { get; } = new();

    public static (Line, Vector2)? GetKickDirection(List<Vector2> ballPath, int minimumSamples = 2)
    {
        if (ballPath.Count < minimumSamples) return null;

        var estimator = new OrthogonalRegressionLineEstimator(ballPath.Count);
        foreach (var sample in ballPath)
        {
            estimator.AddSample(sample.X, sample.Y);
        }

        var estimate = estimator.Estimate;
        if (!estimate.HasValue) return null;

        var direction = estimate.Value.Direction;
        var travel = ballPath[^1] - ballPath[0];

        if (Utils.ApproximatelyZero(travel.LengthSquared()))
        {
            for (var i = 1; i < ballPath.Count; i++)
            {
                travel += ballPath[i] - ballPath[i - 1];
            }
        }

        if (Utils.ApproximatelyZero(direction.LengthSquared()) ||
            Utils.ApproximatelyZero(travel.LengthSquared()))
            return null;

        return (estimate.Value, Vector2.Dot(direction, travel) < 0f ? -direction : direction);
    }

    public static (Line, Vector2)? GetKickDirectionByRegressionLine(List<Vector2> ballPath, int minimumSamples = 2)
    {
        if (ballPath.Count < minimumSamples) return null;

        var estimator = new LinearRegressionLineEstimator(ballPath.Count);
        foreach (var sample in ballPath)
        {
            estimator.AddSample(sample.X, sample.Y);
        }

        var estimate = estimator.Estimate;
        if (!estimate.HasValue) return null;

        var x1 = ballPath[0].X;
        var x2 = ballPath[^1].X;
        var p1 = new Vector2(x1, estimate.Value.Y(x1));
        var p2 = new Vector2(x2, estimate.Value.Y(x2));
        var direction = p2 - p1;

        if (Utils.ApproximatelyZero(direction.LengthSquared()))
            return null;

        return (estimate.Value, Vector2.Normalize(direction));
    }

    public DetectedKick? DetectKick(MergedBall? ball, List<FilteredRobot> robots)
    {
        if (ball == null) return null;
        var fastKick = FastKickDetector.Tick(ball.Value, robots);
        var slowKick = SlowKickDetector.Tick(ball.Value, robots);
        return slowKick ?? fastKick;
    }

    public void Reset()
    {
        FastKickDetector.Reset();
    }
}
