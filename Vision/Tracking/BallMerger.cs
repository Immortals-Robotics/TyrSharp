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
    private static float MinSearchRadius { get; set; } = 300f;

    private Timestamp? _lastBallUpdateTimestamp;

    private static float PositionUncertaintyWeight(BallTracker ballTracker) =>
        MathF.Pow(ballTracker.Filter.PositionUncertainty.Length() * ballTracker.Uncertainty, -MergePower);

    private static float VelocityUncertaintyWeight(BallTracker ballTracker) =>
        MathF.Pow(ballTracker.Filter.VelocityUncertainty.Length() * ballTracker.Uncertainty, -MergePower);

    public MergedBall? Process(List<BallTracker> ballTrackers, Timestamp timestamp, FilteredBall lastFilteredBall)
    {
        if (ballTrackers.Count == 0) return null;

        _lastBallUpdateTimestamp ??= lastFilteredBall.Timestamp;

        var dt = timestamp - _lastBallUpdateTimestamp.Value;
        var searchRadius = MathF.Abs((float)dt.Seconds * BallTracker.MaxLinearVelocity);
        searchRadius = MathF.Max(searchRadius, MinSearchRadius);

        List<BallTracker> validTrackers = [];

        foreach (var ballTracker in ballTrackers)
        {
            // if the ball is airborne, project its position to the ground
            var searchPosition = lastFilteredBall.State.IsChipped
                ? ballTracker.Camera.ProjectToGround(lastFilteredBall.State.Position)
                : lastFilteredBall.State.Position.Xy();

            var trackerPos = ballTracker.Filter.GetPosition(timestamp);

            if (Vector2.Distance(trackerPos, searchPosition) < searchRadius)
            {
                validTrackers.Add(ballTracker);
            }
        }

        if (validTrackers.Count == 0) return null;

        // select at most one tracker per camera
        var selectedTrackers = validTrackers
            .GroupBy(tracker => tracker.Camera.Id)
            .Select(grouping => grouping.MaxBy(tracker => tracker.LastRawBall.CaptureTimestamp))
            .OfType<BallTracker>()
            .ToList();

        Assert.IsPositive(selectedTrackers.Count);

        var mergedBall = Merge(selectedTrackers, timestamp);

        if (mergedBall.LatestRawBall.HasValue)
        {
            _lastBallUpdateTimestamp = mergedBall.LatestRawBall.Value.CaptureTimestamp;
        }

        return mergedBall;
    }

    public void Reset()
    {
        _lastBallUpdateTimestamp = null;
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