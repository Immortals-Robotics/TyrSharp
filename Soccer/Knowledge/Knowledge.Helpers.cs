using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Vision.Data;

namespace Tyr.Soccer.Knowledge;

public partial class Knowledge
{
    public bool BallIsGoaling()
    {
        if (Context.Ball.State.Velocity.Length() < 300.0f)
            return false;

        var movingToOurGoal = (Context.SideSign == -1 && Context.Ball.State.Velocity.X < 0) ||
                              (Context.SideSign == 1 && Context.Ball.State.Velocity.X > 0);

        if (!movingToOurGoal)
            return false;

        var ballTrajectory = Line.FromTwoPoints(Context.Ball.State.Position,
            Context.Ball.State.Position + Context.Ball.State.Velocity.Xy());
        var goalLine = Context.Field.OwnGoalLine();

        var intersection = Geometry.Intersection(ballTrajectory, goalLine);

        if (!intersection.HasValue) return false;
        var toIntersection = intersection.Value - Context.Ball.State.Position;
        var dot = Vector2.Dot(toIntersection, Context.Ball.State.Velocity.Xy());
        return dot > 0;
    }

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