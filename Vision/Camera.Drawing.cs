using System.Numerics;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;

namespace Tyr.Vision;

public partial class Camera
{
    private const string Layer = Meta.DebugLayerPrefix + "Camera";

    private void DrawCalibration(Timestamp timestamp)
    {
        if (!Calibration.HasValue) return;

        Draw.BeginLayer(Layer);

        var pos3d = Calibration.Value.DerivedCameraWorld;
        var pos2d = pos3d.Xy();

        Draw.DrawPoint(pos2d, Color.Cyan, Options.Outline(15));

        Draw.DrawText(Id.ToString(),
            pos2d + new Vector2(0, 50), 150f,
            Color.Cyan300, TextAlignment.BottomCenter);

        Draw.DrawText($"Height: {pos3d.Z:F2}mm",
            pos2d + new Vector2(100, -50), 100f,
            Color.Cyan400, TextAlignment.MiddleLeft);

        Draw.DrawText($"FPS: {Fps:F2}",
            pos2d + new Vector2(100, 50), 100f,
            Color.Cyan400, TextAlignment.MiddleLeft);

        Draw.DrawText($"Behind: {(timestamp - Timestamp).Milliseconds:F2}ms",
            pos2d + new Vector2(100, 150), 100f,
            Color.Cyan400, TextAlignment.MiddleLeft);

        Draw.EndLayer();
    }

    private void DrawTrackedBalls(Timestamp timestamp)
    {
        foreach (var tracker in Balls)
        {
            tracker.DrawDebug(timestamp);
        }
    }

    private void DrawTrackedRobots()
    {
        foreach (var tracker in Robots.Values)
        {
            tracker.DrawDebug(timestamp: Timestamp);
        }
    }
}