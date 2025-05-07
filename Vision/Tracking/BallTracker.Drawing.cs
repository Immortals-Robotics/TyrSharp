using System.Numerics;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;

namespace Tyr.Vision.Tracking;

public partial class BallTracker
{
    private const string Layer = Meta.DebugLayerPrefix + "BallTracker";

    public void DrawDebug(Timestamp timestamp)
    {
        Draw.BeginLayer(Layer);

        Draw.DrawCircle(Filter.Position, 60f, Color.Orange300,
            Options.Outline() with { Thickness = 5f });

        var uncertainty = Filter.PositionUncertainty.Length() * Uncertainty;
        var behind = timestamp - LastUpdateTimestamp;
        Draw.DrawText($"[{Camera.Id}] unc.: {uncertainty:F2}, dt: {behind.Milliseconds:F2}ms",
            Filter.Position + new Vector2(70, Camera.Id * 60), 50f,
            Color.Orange200, TextAlignment.BottomLeft);

        Draw.EndLayer();
    }
}