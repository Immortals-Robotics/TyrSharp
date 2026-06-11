using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data;
using Tyr.Common.Math;
using Tyr.Common.Vision.Data;

namespace Tyr.Vision.Tracking;

[Configurable]
public partial class RobotMerger
{
    [ConfigEntry(
        "Factor to weight stdDeviation during tracker merging, reasonable range: 1.0 - 2.0. High values lead to more jitter")]
    private static float MergePower { get; set; } = 1.5f;

    // Bolt: eliminates ~15 allocs/frame (LINQ SelectMany/GroupBy/ToDictionary + enumerators) — using class-level dictionary and manual loops
    // To verify: dotnet-counters monitor --counters System.Runtime[gen-0-gc-count,alloc-rate] -p <pid>
    private readonly Dictionary<RobotId, List<RobotTracker>> _trackersById = new(CommonConfigs.MaxRobots * 2);

    public List<FilteredRobot> Process(IEnumerable<Camera> cameras, Timestamp timestamp)
    {
        foreach (var list in _trackersById.Values)
        {
            list.Clear();
        }

        foreach (var camera in cameras)
        {
            foreach (var robot in camera.Robots.Values)
            {
                if (!_trackersById.TryGetValue(robot.Id, out var trackers))
                {
                    trackers = new List<RobotTracker>(4);
                    _trackersById[robot.Id] = trackers;
                }
                trackers.Add(robot);
            }
        }

        var mergedRobots = new List<FilteredRobot>(_trackersById.Count);

        foreach (var (id, trackers) in _trackersById)
        {
            if (trackers.Count == 0) continue;
            mergedRobots.Add(Merge(id, trackers, timestamp));
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