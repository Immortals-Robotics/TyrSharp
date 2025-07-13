using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;

namespace Tyr.Soccer.Navigation.Trajectory;

[Configurable]
public static partial class TrajectoryUtils
{
    [ConfigEntry] private static float DrawTimeStep { get; set; } = 0.1f;

    public static void DrawTrajectory(ITrajectory<Vector2> trajectory, Color color)
    {
        for (var t = trajectory.StartTime; t < trajectory.EndTime - DrawTimeStep; t += DrawTimeStep)
        {
            var p0 = trajectory.GetPosition(t);
            var p1 = trajectory.GetPosition(t + DrawTimeStep);
            Draw.DrawLineSegment(p0, p1, color);
        }
    }
}