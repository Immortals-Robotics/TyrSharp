using System.Numerics;

namespace Tyr.Vision.Tracker;

public class Ball
{
    public DateTime LastUpDateTime { get; private set; }

    // returns whether the detection was valid for updating the tracker
    public bool Update(Common.Data.Ssl.Vision.Detection.Ball detection, DateTime time)
    {
        LastUpDateTime = time;
        return true;
    }

    public void Predict(DateTime time)
    {
    }

    public Vector2 GetPosition(DateTime time)
    {
        return Vector2.Zero;
    }

    public Vector2 Velocity => Vector2.Zero;
}