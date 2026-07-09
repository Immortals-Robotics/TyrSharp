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
        // Bolt: eliminates ~1 enumerator alloc/frame by avoiding LINQ Count()
        var ownCount = 0;
        foreach (var robot in Context.OwnRobots)
        {
            if (robot.Seen)
            {
                ownCount++;
            }
        }
        OwnRobotsCount = ownCount;

        OpponentRobotsCount = Context.OppRobots.Count;

        UpdateAttackerAssignmentCosts();
        UpdateGameConditions();
        UpdateDefense();
        UpdateOpponentThreats();
        UpdateZones();
        UpdateAttackerDecisions();
    }
}
