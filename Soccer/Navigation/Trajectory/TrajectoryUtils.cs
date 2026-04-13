using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;

namespace Tyr.Soccer.Navigation.Trajectory;

[Configurable]
public static partial class TrajectoryUtils
{
    public static void DrawTrajectory(Trajectory2D trajectory, Color color)
    {
        Draw.DrawDrawable(new Trajectory2DDrawable { Trajectory = trajectory }, color);
    }

    public static void DrawTrajectory(Trajectory2DChained trajectory, Color color)
    {
        Draw.DrawDrawable(new Trajectory2DChainedDrawable { Trajectory = trajectory }, color);
    }
}
