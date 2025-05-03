using System.Numerics;
using Tyr.Common.Config;
using Tyr.Vision.Data;

namespace Tyr.Vision.Tracking;

[Configurable]
public partial class BallMerger
{
    [ConfigEntry(
        "Factor to weight stdDeviation during tracker merging, reasonable range: 1.0 - 2.0. High values lead to more jitter")]
    private static float MergePower { get; set; } = 1.5f;

    [ConfigEntry("Minimum search radius for cam balls around last known position [mm]")]
    private static float minSearchRadius { get; set; } = 300f;

    private float lastBallSearchRadius = 1f;
    private List<Vector2> lastSearchPositions = [];
    private Timestamp? lastBallUpdateTimestamp;

    private static float PositionUncertaintyWeight(BallTracker ballTracker) =>
        MathF.Pow(ballTracker.Filter.PositionUncertainty.Length() * ballTracker.Uncertainty, -MergePower);

    private static float VelocityUncertaintyWeight(BallTracker ballTracker) =>
        MathF.Pow(ballTracker.Filter.VelocityUncertainty.Length() * ballTracker.Uncertainty, -MergePower);

    public MergedBall? Process(List<BallTracker> ballTrackers, Timestamp timestamp, FilteredBall lastFilteredBall)
    {
        if (ballTrackers.Count == 0) return null;

        lastBallUpdateTimestamp ??= lastFilteredBall.Timestamp;

        lastBallSearchRadius =
            MathF.Abs((float)((timestamp - lastBallUpdateTimestamp.Value).Seconds * BallTracker.MaxLinearVelocity));
        lastBallSearchRadius = MathF.Max(lastBallSearchRadius, minSearchRadius);
        
        lastSearchPositions.Clear();
        List<BallTracker> primaryTrackers;

        if (lastFilteredBall.State.IsChipped)
        {
            // if the ball is airborne we project its position to the ground
            // from all cameras and use these locations as search point
            
            
        }
    }

    public void Reset()
    {
        lastBallUpdateTimestamp = null;
    }

    // Merges multiple ball trackers into a single merged ball,
    // weighted by their state uncertainty (less certain = less influence).
    public static MergedBall Merge(IReadOnlyList<BallTracker> trackers, Timestamp timestamp)
    {
        Assert.IsNotEmpty(trackers);

        var totalPositionUncertainty = 0f;
        var totalVelocityUncertainty = 0f;

        RawBall? lastRawBall = null;

        // calculate sum of all uncertainty weights
        foreach (var tracker in trackers)
        {
            totalPositionUncertainty += PositionUncertaintyWeight(tracker);
            totalVelocityUncertainty += VelocityUncertaintyWeight(tracker);

            if (tracker.Updated)
            {
                tracker.Updated = false; // TODO: move this out of this function
                lastRawBall = tracker.LastRawBall;
            }
        }

        Assert.IsPositive(totalPositionUncertainty);
        Assert.IsPositive(totalVelocityUncertainty);

        var position = Vector2.Zero;
        var positionRaw = Vector2.Zero;
        var velocity = Vector2.Zero;

        // take all trackers and calculate their pos/vel sum weighted by uncertainty.
        // Trackers with high uncertainty have less influence on the merged result.
        foreach (var tracker in trackers)
        {
            var positionWeight = PositionUncertaintyWeight(tracker);
            positionRaw += tracker.LastRawBall.Detection.Position * positionWeight;
            position += tracker.Filter.GetPosition(timestamp) * positionWeight;

            var velocityWeight = VelocityUncertaintyWeight(tracker);
            velocity += tracker.Filter.Velocity * velocityWeight;
        }

        positionRaw /= totalPositionUncertainty;
        position /= totalPositionUncertainty;
        velocity /= totalVelocityUncertainty;

        return new MergedBall
        {
            Position = position,
            RawPosition = positionRaw,
            Velocity = velocity,
            Timestamp = timestamp,
            LatestRawBall = lastRawBall,
        };
    }
}