using System.Numerics;
using Tyr.Common.Math.Shapes;

namespace Tyr.Soccer.Knowledge;

public partial class Knowledge
{
    public List<Zone> Zones { get; } = [];

    private void UpdateZones()
    {
        Zones.Clear();

        if (Zone.ZoneCountX == 0 || Zone.ZoneCountY == 0) return;

        var zoneWidth = Context.Field.Width * 2 / Zone.ZoneCountX;
        var zoneHeight = Context.Field.Height * 2 / Zone.ZoneCountY;

        for (uint i = 0; i < Zone.ZoneCountX; i++)
        {
            for (uint j = 0; j < Zone.ZoneCountY; j++)
            {
                var fieldMin = new Vector2(-Context.Field.Width, -Context.Field.Height);
                var zoneMin = fieldMin + new Vector2(i * zoneWidth, j * zoneHeight);

                var zone = new Zone
                {
                    Rect = Rectangle.FromCornerAndSize(zoneMin, zoneWidth, zoneHeight)
                };
                zone.UpdateScore();

                zone.DrawZone();

                Zones.Add(zone);
            }
        }
    }
}
