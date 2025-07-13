using System.Numerics;
using Tyr.Common.Debug.Drawing;

namespace Tyr.Soccer.Navigation.Trajectory;

public static class TrajectoryUtils
{
    private const float dt = 0.1f;

    public static void DrawTrajectory(ITrajectory<Vector2> trajectory, Color color)
    {
        for (var t = trajectory.StartTime; t < trajectory.EndTime - dt; t += dt)
        {
            var p0 = trajectory.GetPosition(t);
            var p1 = trajectory.GetPosition(t + dt);
            Draw.DrawLineSegment(p0, p1, color);
        }
    }
}