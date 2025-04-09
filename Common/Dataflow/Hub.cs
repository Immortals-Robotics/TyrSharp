namespace Tyr.Common.Dataflow;

public static class Hub
{
    // external data
    public static readonly BroadcastChannel<Data.Ssl.Vision.WrapperPacket> SslVision = new();
    public static readonly BroadcastChannel<Data.Ssl.Gc.Referee> Gc = new();

    // our published data
    public static readonly BroadcastChannel<Data.Ssl.Vision.Tracker.Frame> Vision = new();
    public static readonly BroadcastChannel<Data.Referee.State> Referee = new();
    public static readonly BroadcastChannel<Data.Robot.Command[]> RobotCommands = new();
}