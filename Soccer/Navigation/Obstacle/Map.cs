using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Math.Shapes;
using Tyr.Soccer.Navigation.Trajectory;

namespace Tyr.Soccer.Navigation.Obstacle;

[Configurable]
public partial class Map
{
    [ConfigEntry] private static float CollisionDetectStepSize { get; set; } = 50f;

    private readonly List<Obstacle<Circle>> _circles = [];
    private readonly List<Obstacle<Rectangle>> _rects = [];

    public Physicality Physicality { get; set; } = Physicality.All;

    public float BaseMargin { get; set; }

    public void Add(Circle circle, Physicality physicality)
    {
        _circles.Add(new Obstacle<Circle>(circle, physicality));
        Draw.DrawCircle(circle, Color.Rose700.WithAlpha(0.5f));
    }

    public void Add(Rectangle rect, Physicality physicality)
    {
        _rects.Add(new Obstacle<Rectangle>(rect, physicality));
        Draw.DrawRectangle(rect, Color.Rose700.WithAlpha(0.5f));
    }

    public bool Inside(Vector2 point, float margin = 0.0f, Physicality physicality = Physicality.All)
    {
        var marginTotal = BaseMargin + margin;

        // TODO: convert this to an actual obstacle shape if needed
        // TODO: enable after globals are there
#if false
        var fieldMargin = Field.Instance.BoundaryWidth - Field.Instance.RobotRadius + 10f;
        if (MathF.Abs(point.X) > Field.Instance.Width + fieldMargin ||
            MathF.Abs(point.Y) > Field.Instance.Height + fieldMargin)
        {
            return true;
        }
#endif

        var activePhysicality = Physicality & physicality;

        foreach (var circle in _circles)
        {
            if ((circle.Physicality & activePhysicality) == 0) continue;
            if (circle.Inside(point, marginTotal)) return true;
        }

        foreach (var rect in _rects)
        {
            if ((rect.Physicality & activePhysicality) == 0) continue;
            if (rect.Inside(point, marginTotal)) return true;
        }

        return false;
    }

    public float Distance(Vector2 point, Physicality physicality = Physicality.All)
    {
        var activePhysicality = Physicality & physicality;
        var minDistance = float.MaxValue;

        foreach (var circle in _circles)
        {
            if ((circle.Physicality & activePhysicality) == 0) continue;
            var d = circle.Distance(point);
            if (d < minDistance) minDistance = d;
            if (minDistance <= 0f) return minDistance;
        }

        foreach (var rect in _rects)
        {
            if ((rect.Physicality & activePhysicality) == 0) continue;
            var d = rect.Distance(point);
            if (d < minDistance) minDistance = d;
            if (minDistance <= 0f) return minDistance;
        }

        return minDistance;
    }

    // TODO: change param to LineSegment
    public bool CollisionDetect(Vector2 p1, Vector2 p2, Physicality physicality = Physicality.All)
    {
        var direction = Vector2.Normalize(p2 - p1) * CollisionDetectStepSize;
        var current = p1;

        while (Vector2.Distance(current, p2) > CollisionDetectStepSize)
        {
            if (Inside(current, 0f, physicality))
                return true;

            current += direction;
        }

        return false;
    }

    public (bool collided, float time) HasCollision(
        ITrajectory<Vector2> trajectory,
        float lookAhead = 3.0f, float stepT = 0.1f,
        Physicality physicality = Physicality.All)
    {
        var tEnd = Math.Min(trajectory.EndTime, trajectory.StartTime + lookAhead);

        for (var t = trajectory.StartTime; t < tEnd; t += stepT)
        {
            var pos = trajectory.GetPosition(t);
            if (Inside(pos, 0.0f, physicality))
            {
                return (true, t);
            }
        }

        return (false, float.MaxValue);
    }

    public (bool reachedFree, float time) ReachFree(
        ITrajectory<Vector2> trajectory,
        float lookAhead = 3.0f, float stepT = 0.1f,
        Physicality physicality = Physicality.All)
    {
        var tEnd = Math.Min(trajectory.EndTime, trajectory.StartTime + lookAhead);

        for (var t = trajectory.StartTime; t < tEnd; t += stepT)
        {
            var pos = trajectory.GetPosition(t);
            if (!Inside(pos, 0.0f, physicality))
            {
                return (true, t);
            }
        }

        return (false, float.MaxValue);
    }

    public void Reset()
    {
        _circles.Clear();
        _rects.Clear();
        Physicality = Physicality.All;
        BaseMargin = 0.0f;
    }
}