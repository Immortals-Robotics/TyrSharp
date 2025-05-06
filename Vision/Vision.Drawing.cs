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

    private static void DrawTrackedBalls(Camera camera)
    {
        for (var index = 0; index < camera.Balls.Count; index++)
        {
            var tracker = camera.Balls[index];
            Draw.DrawCircle(tracker.Filter.Position, 25f, Color.Orange400,
                Options.Outline() with { Thickness = 5f });
        }
    }

    private static void DrawTrackedRobots(Camera camera)
    {
        foreach (var (id, tracker) in camera.Robots)
        {
            Draw.DrawRobot(tracker.Position, tracker.Angle, id,
                Options.Outline() with { Thickness = 10f });
        }
    }
}