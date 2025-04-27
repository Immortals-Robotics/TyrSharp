using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Robot(Vector2 Position, Angle? Orientation = null, uint? Id = null) : IDrawable
{
    public Robot(Math.Shapes.Robot robot, uint? id = null)
        : this(robot.Center, robot.Angle, id)
    {
    }

    public Robot(Data.Ssl.Vision.Detection.Robot robot, uint? id = null)
        : this(robot.Position, robot.Orientation, id)
    {
    }

    public Robot(Data.Ssl.Vision.Tracker.Robot robot)
        : this(robot.Position, robot.Angle, robot.Id.Id)
    {
    }
}