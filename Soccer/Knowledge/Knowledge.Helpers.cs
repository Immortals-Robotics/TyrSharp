using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Vision.Data;

namespace Tyr.Soccer.Knowledge;

public partial class Knowledge
{
    public FilteredRobot? FindNearestOpp(Vector2 pos, int? mask = null, bool acceptNearBall = true)
    {
        var minDis = float.MaxValue;
        FilteredRobot? result = null;

        foreach (var robot in Context.OppRobots)
        {
            if (robot.Id.Id == mask)
                continue;

            if (Context.Field.Rectangle.Distance(robot.State.Position) > 0)
                continue;

            if (!acceptNearBall && Vector2.Distance(Context.Ball.State.Position, robot.State.Position) < 500)
                continue;

            var dis = Vector2.Distance(pos, robot.State.Position);
            if (dis < minDis)
            {
                minDis = dis;
                result = robot;
            }
        }

        return result;
    }

    public bool ShootBlocked(Vector2 initPos, Vector2 targetPos, float maxDistance, float radius)
    {
        var shootLine = new LineSegment { Start = targetPos, End = initPos };
        foreach (var robot in Context.OppRobots)
        {
            if (Vector2.Distance(robot.State.Position, initPos) > maxDistance)
                continue;

            if (shootLine.Distance(robot.State.Position) < radius)
            {
                return true;
            }
        }

        return false;
    }
}