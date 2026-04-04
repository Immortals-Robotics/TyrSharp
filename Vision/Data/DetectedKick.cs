using System.Numerics;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Math;
using Tyr.Common.Vision.Data;

namespace Tyr.Vision.Data;

public class DetectedKick(
    RobotId robotId,
    RobotState robotState,
    Vector2 kickPosition,
    Timestamp timestamp,
    bool isFastDetection,
    List<MergedBall> ballStatesAfterKick)
{
    public RobotId RobotId { get; init; } = robotId;
    public RobotState RobotState { get; init; } = robotState;
    public Vector2 KickPosition { get; init; } = kickPosition;
    public Timestamp Timestamp { get; init; } = timestamp;
    public bool IsFastDetection { get; init; } = isFastDetection;
    List<MergedBall> BallStatesAfterKick { get; set; } = ballStatesAfterKick;


    public Vector2? GetKickDirection()
    {
        var samples = BallStatesAfterKick
            .Select(ballState => new
            {
                Position = ballState.LatestRawBall?.Detection.Position ?? ballState.Position,
                Timestamp = ballState.LatestRawBall?.CaptureTimestamp ?? ballState.Timestamp
            })
            .OrderBy(sample => sample.Timestamp)
            .ToList();

        if (samples.Count < 2) return null;

        var estimator = new OrthogonalRegressionLineEstimator(samples.Count);
        foreach (var sample in samples)
        {
            estimator.AddSample(sample.Position.X, sample.Position.Y);
        }

        if (!estimator.Estimate.HasValue) return null;

        var direction = estimator.Estimate.Value.Direction;
        var travel = samples[^1].Position - samples[0].Position;

        if (Utils.ApproximatelyZero(travel.LengthSquared()))
        {
            for (var i = 1; i < samples.Count; i++)
            {
                travel += samples[i].Position - samples[i - 1].Position;
            }
        }

        if (Utils.ApproximatelyZero(direction.LengthSquared()) ||
            Utils.ApproximatelyZero(travel.LengthSquared()))
            return null;

        return Vector2.Dot(direction, travel) < 0f ? -direction : direction;
    }
}
