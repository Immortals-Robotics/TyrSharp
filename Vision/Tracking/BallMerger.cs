using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Vision.Data;
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

    // Bolt: eliminate multiple LINQ allocations per frame
    private readonly List<BallTracker> _ballTrackers = new();
    private readonly List<BallTracker> _validTrackers = new();
    private readonly Dictionary<uint, BallTracker> _selectedTrackers = new();
    private readonly List<BallTracker> _selectedTrackersList = new();

    public MergedBall? Process(IEnumerable<Camera> cameras, Timestamp timestamp, FilteredBall lastFilteredBall)
    {
        _ballTrackers.Clear();
        foreach (var camera in cameras)
        {
            foreach (var ball in camera.Balls)
            {
                _ballTrackers.Add(ball);
            }
        }

        if (_ballTrackers.Count == 0) return null;

        _lastBallUpdateTimestamp ??= lastFilteredBall.Timestamp;

        var dt = timestamp - _lastBallUpdateTimestamp.Value;
        var searchRadius = MathF.Abs((float)dt.Seconds * BallTracker.MaxLinearVelocity);
        searchRadius = MathF.Max(searchRadius, MinSearchRadius);

        _validTrackers.Clear();

        foreach (var ballTracker in _ballTrackers)
        {
            if (!ballTracker.IsGrownUp) continue;

            // if the ball is airborne, project its position to the ground
            var searchPosition = lastFilteredBall.State.IsChipped
                ? ballTracker.Camera.ProjectToGround(lastFilteredBall.State.Position3D)
                : lastFilteredBall.State.Position;

            var trackerPos = ballTracker.Filter.GetPosition(timestamp);

            if (Vector2.Distance(trackerPos, searchPosition) < searchRadius)
            {
                _validTrackers.Add(ballTracker);
            }
        }

        if (_validTrackers.Count == 0) return null;

        // select at most one tracker per camera
        _selectedTrackers.Clear();
        _selectedTrackersList.Clear();
        foreach (var tracker in _validTrackers)
        {
            if (!_selectedTrackers.TryGetValue(tracker.Camera.Id, out var existingTracker) ||
                tracker.LastRawBall.CaptureTimestamp > existingTracker.LastRawBall.CaptureTimestamp)
            {
                _selectedTrackers[tracker.Camera.Id] = tracker;
            }
        }

        foreach (var tracker in _selectedTrackers.Values)
        {
            _selectedTrackersList.Add(tracker);
        }

        Assert.IsPositive(_selectedTrackersList.Count);

        var mergedBall = Merge(_selectedTrackersList, timestamp);

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
    private static MergedBall Merge(IReadOnlyList<BallTracker> trackers, Timestamp timestamp)
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

    private static float PositionUncertaintyWeight(BallTracker ballTracker) =>
        MathF.Pow(ballTracker.Filter.PositionUncertainty.Length() * ballTracker.Uncertainty, -MergePower);

    private static float VelocityUncertaintyWeight(BallTracker ballTracker) =>
        MathF.Pow(ballTracker.Filter.VelocityUncertainty.Length() * ballTracker.Uncertainty, -MergePower);
}