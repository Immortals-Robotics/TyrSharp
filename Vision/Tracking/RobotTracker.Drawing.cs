using System.Numerics;
using Tyr.Common.Data;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;

namespace Tyr.Vision.Tracking;

public partial class RobotTracker
{
    private const string Layer = Meta.DebugLayerPrefix + "RobotTracker";

    public void DrawDebug(Timestamp timestamp)
    {
        Draw.BeginLayer(Layer);

        var outlineColor = Id.Team == TeamColor.Blue ? Color.Blue200 : Color.Yellow100;
        var textColor = Id.Team == TeamColor.Blue ? Color.Blue200 : Color.Yellow50;

        Draw.DrawRobot(Position, Angle, null, outlineColor, 120f,
            Options.Outline());

        var uncertainty = FilterXy.PositionUncertainty.Length() * Uncertainty;
        var behind = timestamp - LastUpdateTimestamp;
        Draw.DrawText($"[{Camera.Id}] unc.: {uncertainty:F2}, dt: {behind.Milliseconds:F2}ms",
            Position + new Vector2(130, Camera.Id * 60), 50f,
            textColor, TextAlignment.BottomLeft);

        Draw.EndLayer();
    }
}