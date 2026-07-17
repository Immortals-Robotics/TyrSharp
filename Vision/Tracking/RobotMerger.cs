using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Math;
using Tyr.Common.Vision.Data;
using Tyr.Common.Data;

namespace Tyr.Vision.Tracking;

[Configurable]
public partial class RobotMerger
{
    [ConfigEntry(
        "Factor to weight stdDeviation during tracker merging, reasonable range: 1.0 - 2.0. High values lead to more jitter")]
    private static float MergePower { get; set; } = 1.5f;

    // Use a fixed size array of lists for max robots * 2 teams
    private readonly List<RobotTracker>[] _trackersById = new List<RobotTracker>[CommonConfigs.MaxRobots * 2];

    // For unknown or out of bounds ids
    // Bolt: We use a custom struct key and explicitly reuse pools to eliminate allocation overhead for dynamic groupings
    private readonly Dictionary<RobotId, List<RobotTracker>> _unknownTrackersById = new(new RobotIdComparer());
    private readonly List<List<RobotTracker>> _unknownTrackersPool = new();
    private int _unknownTrackersPoolIndex;

    private sealed class RobotIdComparer : IEqualityComparer<RobotId>
    {
        public bool Equals(RobotId x, RobotId y)
        {
            return x.Id == y.Id && x.Team == y.Team;
        }

        public int GetHashCode(RobotId obj)
        {
            return HashCode.Combine(obj.Id, obj.Team);
        }
    }

    public RobotMerger()
    {
        for (int i = 0; i < _trackersById.Length; i++)
        {
            _trackersById[i] = new List<RobotTracker>();
        }
    }

    private int GetIndex(RobotId id)
    {
        if (id.Id is null || id.Team is null || id.Id >= CommonConfigs.MaxRobots || id.Team == TeamColor.Unknown)
            return -1;

        int offset = id.Team == TeamColor.Blue ? 0 : CommonConfigs.MaxRobots;
        return (int)id.Id.Value + offset;
    }

    public List<FilteredRobot> Process(IEnumerable<Camera> cameras, Timestamp timestamp)
    {
        for (int i = 0; i < _trackersById.Length; i++)
        {
            _trackersById[i].Clear();
        }

        _unknownTrackersById.Clear();
        _unknownTrackersPoolIndex = 0;

        int activeRobotCount = 0;

        foreach (var camera in cameras)
        {
            foreach (var tracker in camera.Robots.Values)
            {
                var idx = GetIndex(tracker.Id);
                if (idx == -1)
                {
                    if (!_unknownTrackersById.TryGetValue(tracker.Id, out var unknownList))
                    {
                        if (_unknownTrackersPoolIndex < _unknownTrackersPool.Count)
                        {
                            unknownList = _unknownTrackersPool[_unknownTrackersPoolIndex++];
                            unknownList.Clear();
                        }
                        else
                        {
                            unknownList = new List<RobotTracker>();
                            _unknownTrackersPool.Add(unknownList);
                            _unknownTrackersPoolIndex++;
                        }
                        _unknownTrackersById[tracker.Id] = unknownList;
                    }
                    unknownList.Add(tracker);
                }
                else
                {
                    if (_trackersById[idx].Count == 0)
                    {
                        activeRobotCount++;
                    }
                    _trackersById[idx].Add(tracker);
                }
            }
        }

        // Bolt: eliminates ~N allocs/frame — replacing LINQ groupings with pre-allocated list array processing
        var mergedRobots = new List<FilteredRobot>(activeRobotCount + _unknownTrackersById.Count);

        for (int i = 0; i < _trackersById.Length; i++)
        {
            var trackers = _trackersById[i];
            if (trackers.Count > 0)
            {
                var id = trackers[0].Id;
                mergedRobots.Add(Merge(id, trackers, timestamp));
            }
        }

        foreach (var pair in _unknownTrackersById)
        {
            mergedRobots.Add(Merge(pair.Key, pair.Value, timestamp));
        }

        return mergedRobots;
    }

    private static FilteredRobot Merge(RobotId id, List<RobotTracker> trackers, Timestamp timestamp)
    {
        Assert.IsNotEmpty(trackers);

        var totalPositionUncertainty = 0f;
        var totalVelocityUncertainty = 0f;
        var totalOrientationUncertainty = 0f;
        var totalAngularVelocityUncertainty = 0f;

        var maxQuality = 0f;

        // calculate sum of all uncertainties
        foreach (var tracker in trackers)
        {
            totalPositionUncertainty += PositionUncertaintyWeight(tracker);
            totalVelocityUncertainty += VelocityUncertaintyWeight(tracker);
            totalOrientationUncertainty += OrientationUncertaintyWeight(tracker);
            totalAngularVelocityUncertainty += AngularVelocityUncertaintyWeight(tracker);

            maxQuality = MathF.Max(maxQuality, tracker.VisionQuality);
        }

        Assert.IsPositive(totalPositionUncertainty);
        Assert.IsPositive(totalVelocityUncertainty);
        Assert.IsPositive(totalOrientationUncertainty);
        Assert.IsPositive(totalAngularVelocityUncertainty);

        var position = Vector2.Zero;
        var velocity = Vector2.Zero;
        var orientationVec = Vector2.Zero;
        var angularVelocity = 0f;

        // take all trackers and calculate their pos/vel sum weighted by uncertainty.
        // Trackers with high uncertainty have less influence on the merged result.
        foreach (var tracker in trackers)
        {
            var positionWeight = PositionUncertaintyWeight(tracker);
            position += tracker.GetPosition(timestamp) * positionWeight;

            velocity += tracker.Velocity * VelocityUncertaintyWeight(tracker);

            orientationVec += tracker.GetAngle(timestamp).ToUnitVec() * OrientationUncertaintyWeight(tracker);

            angularVelocity += tracker.AngularVelocity.Rad * AngularVelocityUncertaintyWeight(tracker);
        }

        position /= totalPositionUncertainty;
        velocity /= totalVelocityUncertainty;
        angularVelocity /= totalAngularVelocityUncertainty;

        if (Utils.ApproximatelyZero(orientationVec.LengthSquared()))
        {
            orientationVec = trackers[0].GetAngle(timestamp).ToUnitVec();
        }

        var state = new RobotState
        {
            Position = position,
            Velocity = velocity,
            Angle = orientationVec.ToAngle(),
            AngularVelocity = Angle.FromRad(angularVelocity)
        };

        return new FilteredRobot
        {
            Id = id,
            Timestamp = timestamp,
            State = state,
            Quality = maxQuality,
        };
    }

    private static float PositionUncertaintyWeight(RobotTracker tracker) =>
        MathF.Pow(tracker.FilterXy.PositionUncertainty.Length() * tracker.Uncertainty, -MergePower);

    private static float VelocityUncertaintyWeight(RobotTracker tracker) =>
        MathF.Pow(tracker.FilterXy.VelocityUncertainty.Length() * tracker.Uncertainty, -MergePower);

    private static float OrientationUncertaintyWeight(RobotTracker tracker) =>
        MathF.Pow(tracker.FilterW.PositionUncertainty * tracker.Uncertainty, -MergePower);

    private static float AngularVelocityUncertaintyWeight(RobotTracker tracker) =>
        MathF.Pow(tracker.FilterW.VelocityUncertainty * tracker.Uncertainty, -MergePower);
}
