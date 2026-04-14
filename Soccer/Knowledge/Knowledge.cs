namespace Tyr.Soccer.Knowledge;

public partial class Knowledge
{
    public int OwnRobotsCount { get; private set; }
    public int OpponentRobotsCount { get; private set; }

    public void Update()
    {
        OwnRobotsCount = Context.OwnRobots.Count(robot => robot.Seen);
        OpponentRobotsCount = Context.OppRobots.Count;

        foreach (var zone in Context.Knowledge.Zones)
        {
            zone.UpdateScore(false);
            zone.DrawZone();
        }
    }
}
