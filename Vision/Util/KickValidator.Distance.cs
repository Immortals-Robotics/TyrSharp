using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;

namespace Tyr.Vision.Util;

public partial class KickValidator
{
    [ConfigEntry("Minimum distance (in mm) that at least one ball sample must be from the robot to consider a kick")]
    private static double AtLeastOneBeyondDist { get; set; } = 160.0;

    [ConfigEntry(
        "Distance threshold (in mm) for kick validation: first sample must be closer, all others further away")]
    private static double ThresholdDist1 { get; set; } = 130.0;

    [ConfigEntry(
        "Alternative distance threshold (in mm) for kick validation: first sample must be closer, all others further away")]
    private static double ThresholdDist2 { get; set; } = 170.0;

    private bool DistanceValidator(List<MergedBall> balls, List<FilteredRobot> robots)
    {
        var frameByCameraId =
            new Dictionary<uint, List<(MergedBall, FilteredRobot)>>();

        for (var i = 0; i < robots.Count; i++)
        {
            var cameraId = balls[i].LatestRawBall!.Value.CameraId;
            frameByCameraId.TryAdd(cameraId, []);
            frameByCameraId[cameraId].Add((balls[i], robots[i]));
        }

        foreach (var data in frameByCameraId.Values)
        {
            var distances = data
                .Select(d => Vector2.Distance(d.Item1.Position, d.Item2.State.Position))
                .ToList();

            var distantBall = distances.Any(d => d > AtLeastOneBeyondDist);

            if ((distances[0] < ThresholdDist1)
                && distances.Skip(1).All(d => d > ThresholdDist1)
                && distantBall)
            {
                return true;
            }

            if ((distances[0] < ThresholdDist2)
                && distances.Skip(1).All(d => d > ThresholdDist2)
                && distantBall)
            {
                return true;
            }
        }

        return false;
    }
}
