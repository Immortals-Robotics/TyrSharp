namespace Tyr.Common.Dataflow;

public static class Hub
{
    // ssl-vision data
    public static readonly BroadcastChannel<Data.Ssl.Vision.Detection.Frame> RawDetection = new();
    public static readonly BroadcastChannel<Data.Ssl.Vision.Geometry.FieldSize> FieldSize = new();
    public static readonly BroadcastChannel<Data.Ssl.Vision.Geometry.CameraCalibration> CameraCalibration = new();
    public static readonly BroadcastChannel<Data.Ssl.Vision.Geometry.BallModels> BallModels = new();

    // game-controller data
    public static readonly BroadcastChannel<Data.Ssl.Gc.Referee> RawReferee = new();

    // robot status and discovery data
    public static readonly BroadcastChannel<Data.Robot.StatusUpdate> RobotStatus = new();
    public static readonly BroadcastChannel<IReadOnlyList<Data.Robot.DiscoveredRobot>> DiscoveredRobots = new();

    // our published data
    public static readonly BroadcastChannel<Vision.Data.FilteredFrame> Vision = new();
    public static readonly BroadcastChannel<Referee.Data.State> Referee = new();
    public static readonly BroadcastChannel<Sender.Data.CommandsWrapper> Commands = new();
    public static readonly BroadcastChannel<Data.Ssl.Simulation.RobotControlResponse> SimFeedback = new();

    // debug
    public static readonly BroadcastChannel<Debug.Frame> Frames = new();
}
