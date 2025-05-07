using Tyr.Common.Debug.Drawing;
using Tyr.Common.Vision.Data;

namespace Tyr.Vision;

public sealed partial class Vision
{
    private static void DrawFilteredFrame(FilteredFrame frame)
    {
        Draw.DrawCircle(frame.Ball.State.Position.Xy(), 25f, Color.Orange400,
            Options.Filled with { Thickness = 5f });

        Plot.Plot($"ball velocity", frame.Ball.State.Velocity, "vel (mm/s)");

        foreach (var robot in frame.Robots)
        {
            Draw.DrawRobot(robot.State.Position, robot.State.Angle, robot.Id,
                Options.Filled with { Thickness = 10f });

            Plot.Plot($"Robot {robot.Id} velocity", frame.Ball.State.Velocity, "vel (mm/s)");
        }
    }
}