using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Math;
using Tyr.Common.Vision.Data;

namespace Tyr.Vision.Tracking;

[Configurable]
public partial class RobotMerger
{
    [ConfigEntry(
        "Factor to weight stdDeviation during tracker merging, reasonable range: 1.0 - 2.0. High values lead to more jitter")]
    private static float MergePower { get; set; } = 1.5f;

    // Bolt: eliminates ~N allocs/frame — replacing LINQ GroupBy/ToDictionary with fixed-size arrays and loops
    private readonly List<RobotTracker>[] _trackersByRobotIndex = new List<RobotTracker>[Tyr.Common.Data.CommonConfigs.MaxRobots * 2];
    private readonly Dictionary<RobotId, List<RobotTracker>> _fallbackTrackers = new();

    public RobotMerger()
    {
        for (int i = 0; i < _trackersByRobotIndex.Length; i++)
        {
            _trackersByRobotIndex[i] = new List<RobotTracker>(8);
        }
    }

    private int GetRobotIndex(RobotId id)
    {
        if (!id.Id.HasValue || !id.Team.HasValue || id.Team == Tyr.Common.Data.TeamColor.Unknown)
            return -1;

        var idVal = id.Id.Value;
        if (idVal >= Tyr.Common.Data.CommonConfigs.MaxRobots)
            return -1;

        return id.Team == Tyr.Common.Data.TeamColor.Yellow ? (int)idVal : (int)idVal + Tyr.Common.Data.CommonConfigs.MaxRobots;
    }

    public List<FilteredRobot> Process(IEnumerable<Camera> cameras, Timestamp timestamp)
    {
        for (int i = 0; i < _trackersByRobotIndex.Length; i++)
        {
            _trackersByRobotIndex[i].Clear();
        }

        foreach (var trackers in _fallbackTrackers.Values)
        {
            trackers.Clear();
        }

        foreach (var camera in cameras)
        {
            foreach (var tracker in camera.Robots.Values)
            {
                var index = GetRobotIndex(tracker.Id);
                if (index >= 0)
                {
                    _trackersByRobotIndex[index].Add(tracker);
                }
                else
                {
                    if (!_fallbackTrackers.TryGetValue(tracker.Id, out var fallbackList))
                    {
                        fallbackList = new List<RobotTracker>(8);
                        _fallbackTrackers[tracker.Id] = fallbackList;
                    }
                    fallbackList.Add(tracker);
                }
            }
        }

        var mergedRobots = new List<FilteredRobot>(Tyr.Common.Data.CommonConfigs.MaxRobots * 2);

        for (uint idVal = 0; idVal < Tyr.Common.Data.CommonConfigs.MaxRobots; idVal++)
        {
            var yellowIndex = (int)idVal;
            var yellowTrackers = _trackersByRobotIndex[yellowIndex];
            if (yellowTrackers.Count > 0)
            {
                mergedRobots.Add(Merge(new RobotId { Id = idVal, Team = Tyr.Common.Data.TeamColor.Yellow }, yellowTrackers, timestamp));
            }

            var blueIndex = (int)idVal + Tyr.Common.Data.CommonConfigs.MaxRobots;
            var blueTrackers = _trackersByRobotIndex[blueIndex];
            if (blueTrackers.Count > 0)
            {
                mergedRobots.Add(Merge(new RobotId { Id = idVal, Team = Tyr.Common.Data.TeamColor.Blue }, blueTrackers, timestamp));
            }
        }

        foreach (var kvp in _fallbackTrackers)
        {
            if (kvp.Value.Count > 0)
            {
                mergedRobots.Add(Merge(kvp.Key, kvp.Value, timestamp));
            }
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