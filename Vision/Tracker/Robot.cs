namespace Tyr.Vision.Tracker;

public class Robot
{
    public DateTime LastUpDateTime { get; private set; }

    public bool Update(Common.Data.Ssl.Vision.Detection.Robot detection, DateTime time)
    {
        LastUpDateTime = time;
        return true;
    }

    public void Predict(DateTime time)
    {
    }
}