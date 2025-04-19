namespace Tyr.Common.Dataflow;

public static class Hub
{
    // external data
    public static readonly BroadcastChannel<Data.Ssl.Vision.Detection.Frame> RawDetection = new();
    public static readonly BroadcastChannel<Data.Ssl.Vision.Geometry.Data> RawGeometry = new();
    public static readonly BroadcastChannel<Data.Ssl.Gc.Referee> RawReferee = new();

    // our published data
    public static readonly BroadcastChannel<Data.Ssl.Vision.Tracker.Frame> Vision = new();
    public static readonly BroadcastChannel<Data.Referee.State> Referee = new();
    public static readonly BroadcastChannel<Data.Robot.Command[]> RobotCommands = new();

    // debug draws
    public static readonly BroadcastChannel<Debug.Drawing.Command> Draws = new();
}