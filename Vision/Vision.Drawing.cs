using System.Numerics;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;

namespace Tyr.Vision;

public sealed partial class Vision
{
    private const string Layer = "Filtered";

    private static void DrawFilteredFrame(FilteredFrame frame)
    {
        Draw.BeginLayer(Layer);

        DrawFilteredBall(frame.Ball);

        foreach (var robot in frame.Robots)
        {
            DrawFilteredRobot(robot);
        }

        Draw.EndLayer();
    }

    private static void DrawFilteredRobot(FilteredRobot robot)
    {
        Draw.DrawRobot(robot.State.Position, robot.State.Angle, robot.Id, null,
            Options.Filled with { Thickness = 10f });

        Plot.Plot($"Robot {robot.Id} velocity", robot.State.Velocity, "vel (mm/s)");
    }

    private static void DrawFilteredBall(FilteredBall ball)
    {
        Draw.DrawCircle(ball.State.Position, 25f, Color.Orange400,
            Options.Filled with { Thickness = 5f });

        if (ball.State.IsChipped)
        {
            Draw.DrawText("chipped",
                ball.State.Position + new Vector2(0f, -70f), 50f,
                Color.Orange200, TextAlignment.TopCenter);
        }

        Plot.Plot($"ball position", ball.State.Position3D, "pos (mm)");
        Plot.Plot($"ball velocity", ball.State.Velocity, "vel (mm/s)");
    }

    private static void DrawDetectedKick(DetectedKick kick, Vector2? kickDirection)
    {
        Draw.BeginLayer(Layer);

        Draw.DrawCircle(kick.KickPosition, 70f, Color.Red400, Options.Outline(20f));
        Draw.DrawRobot(kick.RobotState.Position, kick.RobotState.Angle, kick.RobotId,
            options: Options.Outline(20f));
        Draw.DrawText($"kick {kick.RobotId}",
            kick.KickPosition + new Vector2(0f, -90f), 50f,
            Color.Red200, TextAlignment.TopCenter);

        if (kickDirection.HasValue && kickDirection.Value != Vector2.Zero)
        {
            Draw.DrawArrow(kick.KickPosition, kick.KickPosition + kickDirection.Value.WithLength(450f),
                kick.IsFastDetection ? Color.Red500 : Color.Blue, Options.Outline(5f));
        }

        Draw.EndLayer();
    }
}