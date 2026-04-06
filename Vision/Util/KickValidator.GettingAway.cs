using System.Numerics;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;

namespace Tyr.Vision.Util;

public partial class KickValidator
{
    private bool GettingAwayValidator(List<FilteredRobot> robots)
    {
        var bot = robots[0];

        foreach (var group in _ballsByCamera.Values)
        {
            var distances = group
                .Select(ball => Vector2.Distance(ball.LatestRawBall!.Value.Detection.Position, bot.State.Position))
                .ToList();

            if (distances.Count < 2)
            {
                continue;
            }

            var valid = Enumerable.Range(1, distances.Count - 1)
                .All(i => distances[i] > distances[i - 1]);

            if (valid)
            {
                return true;
            }
        }

        return false;
    }
}
