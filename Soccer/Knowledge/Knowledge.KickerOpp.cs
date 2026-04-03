using System.Numerics;
using Tyr.Common.Vision.Data;

namespace Tyr.Soccer.Knowledge;

public partial class Knowledge
{
    public FilteredRobot? FindKickerOpp(int? mask = null, float maxDis = 500.0f)
    {
        var minDis = maxDis;
        FilteredRobot? result = null;

        foreach (var robot in Context.OppRobots)
        {
            if (robot.Id.Id == mask)
                continue;

            var dis = Vector2.Distance(Context.Ball.State.Position, robot.State.Position);
            if (!(dis < minDis)) continue;

            minDis = dis;
            result = robot;
        }

        return result;
    }
}