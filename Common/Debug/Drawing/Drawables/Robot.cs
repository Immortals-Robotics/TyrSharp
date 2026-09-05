using System.Numerics;
using MemoryPack;
using Tyr.Common.Config;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

[Configurable]
[MemoryPackable]
public partial record struct Robot : IEntry
{
    [ConfigEntry] public static partial float DefaultRadius { get; set; } = 90f;

    [ConfigEntry("Angle in degrees of the flat front of the robot")]
    public static partial Angle FlatAngle { get; set; } = Angle.FromDeg(45f);

    [ConfigEntry] public static partial float TextSize { get; set; } = 135f;
    [ConfigEntry] public static partial Color TextColor { get; set; } = Color.Zinc950;

    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; } = Meta.Empty;
    [MemoryPackIgnore] public string? ShardKey => null;

    public Vector2 Position { get; init; }
    public Angle? Orientation { get; init; }
    public uint? Id { get; init; }
    public float Radius { get; init; } = DefaultRadius;
    public Color Color { get; set; }
    public Options Options { get; set; }

    [MemoryPackConstructor]
    public Robot()
    {
    }
    
    public Robot(Math.Shapes.Robot robot, uint? id = null)
    {
        Position = robot.Center;
        Orientation = robot.Angle;
        Id = id;
        Radius = robot.Radius;
    }

    public Robot(Data.Ssl.Vision.Detection.Robot robot, uint? id = null)
    {
        Position = robot.Position;
        Orientation = robot.Orientation;
        Id = id;
    }

    public Robot(Data.Ssl.Vision.Tracker.Robot robot)
    {
        Position = robot.Position;
        Orientation = robot.Angle;
        Id = robot.Id.Id;
    }
}
