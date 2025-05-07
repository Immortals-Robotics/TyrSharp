using System.Numerics;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Vision.Data;

namespace Tyr.Vision;

public sealed partial class Vision
{
    private static void DrawFilteredFrame(FilteredFrame frame)
    {
        DrawFilteredBall(frame.Ball);

        foreach (var robot in frame.Robots)
        {
            DrawFilteredRobot(robot);
        }
    }

    private static void DrawFilteredRobot(FilteredRobot robot)
    {
        Draw.DrawRobot(robot.State.Position, robot.State.Angle, robot.Id,
            Options.Filled with { Thickness = 10f });

        Plot.Plot($"Robot {robot.Id} velocity", robot.State.Velocity, "vel (mm/s)");
    }

    private static void DrawFilteredBall(FilteredBall ball)
    {
        Draw.DrawCircle(ball.State.Position.Xy(), 25f, Color.Orange400,
            Options.Filled with { Thickness = 5f });

        if (ball.State.IsChipped)
        {
            Draw.DrawText("chipped",
                ball.State.Position.Xy() + new Vector2(0f, -70f), 50f,
                Color.Orange200, TextAlignment.TopCenter);
        }

        Plot.Plot($"ball position", ball.State.Position, "pos (mm)");
        Plot.Plot($"ball velocity", ball.State.Velocity, "vel (mm/s)");
    }
}