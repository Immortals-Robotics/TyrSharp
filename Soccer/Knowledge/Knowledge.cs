namespace Tyr.Soccer.Knowledge;

public partial class Knowledge
{
    public BallInterception BallInterception { get; } = new();
    public BallPrediction BallPrediction { get; } = new();
    public BallReceiving BallReceiving { get; } = new();
    public OpenAngle OpenAngle { get; } = new();

    public int OwnRobotsCount { get; private set; }
    public int OpponentRobotsCount { get; private set; }


    public void Update()
    {
        OwnRobotsCount = Context.OwnRobots.Count(robot => robot.Seen);
        OpponentRobotsCount = Context.OppRobots.Count;

        UpdateGameConditions();
        UpdateDefense();

        foreach (var zone in Context.Knowledge.Zones)
        {
            zone.UpdateScore();
            zone.DrawZone();
        }
    }
}
