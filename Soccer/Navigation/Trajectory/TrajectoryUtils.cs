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
        if (trajectory is Trajectory2D trajectory2D)
        {
            Draw.DrawDrawable(new TrajectoryDrawable { Trajectory = trajectory2D }, color);
            return;
        }

        for (var t = trajectory.StartTime; t < trajectory.EndTime - DrawTimeStep; t += DrawTimeStep)
        {
            var p0 = trajectory.GetPosition(t);
            var p1 = trajectory.GetPosition(t + DrawTimeStep);
            Draw.DrawLineSegment(p0, p1, color);
        }
    }
}
