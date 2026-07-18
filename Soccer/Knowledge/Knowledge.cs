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
        // Bolt: eliminates ~1 enumerator & closure alloc/frame by avoiding LINQ Count()
        OwnRobotsCount = 0;
        foreach (var robot in Context.OwnRobots)
        {
            if (robot.Seen) OwnRobotsCount++;
        }
        OpponentRobotsCount = Context.OppRobots.Count;

        UpdateAttackerAssignmentCosts();
        UpdateGameConditions();
        UpdateDefense();
        UpdateOpponentThreats();
        UpdateZones();
        UpdateAttackerDecisions();
    }
}
