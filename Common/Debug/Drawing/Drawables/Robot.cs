using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

[Configurable]
public readonly record struct Robot : IDrawable
{
    [ConfigEntry] private static float DefaultRadius { get; set; } = 90f;

    [ConfigEntry("Angle in degrees of the flat front of the robot")]
    public static Angle FlatAngle { get; set; } = Angle.FromDeg(45f);

    [ConfigEntry] public static float TextSize { get; set; } = 135f;
    [ConfigEntry] public static Color TextColor { get; set; } = Color.Zinc950;

    public Vector2 Position { get; init; }
    public Angle? Orientation { get; init; }
    public uint? Id { get; init; }
    public float Radius { get; init; }

    public Robot(Vector2 Position, Angle? Orientation = null, uint? Id = null, float? Radius = null)
    {
        this.Position = Position;
        this.Radius = Radius ?? DefaultRadius;
        this.Orientation = Orientation;
        this.Id = Id;
    }

    public Robot(Math.Shapes.Robot robot, uint? id = null)
        : this(robot.Center, robot.Angle, id, robot.Radius)
    {
    }

    public Robot(Data.Ssl.Vision.Detection.Robot robot, uint? id = null)
        : this(robot.Position, robot.Orientation, id, DefaultRadius)
    {
    }

    public Robot(Data.Ssl.Vision.Tracker.Robot robot)
        : this(robot.Position, robot.Angle, robot.Id.Id, DefaultRadius)
    {
    }
}