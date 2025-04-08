namespace Tyr.Common.Dataflow;

public class Hub
{
    public static readonly BroadcastChannel<Data.Ssl.Vision.Tracker.Frame> Vision = new();
    public static readonly BroadcastChannel<Data.Referee.State> Referee = new();
    public static readonly BroadcastChannel<Data.Robot.Command[]> RobotCommands = new();
}