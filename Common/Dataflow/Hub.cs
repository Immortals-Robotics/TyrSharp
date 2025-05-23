﻿namespace Tyr.Common.Dataflow;

public static class Hub
{
    // ssl-vision data
    public static readonly BroadcastChannel<Data.Ssl.Vision.Detection.Frame> RawDetection = new();
    public static readonly BroadcastChannel<Data.Ssl.Vision.Geometry.FieldSize> FieldSize = new();
    public static readonly BroadcastChannel<Data.Ssl.Vision.Geometry.CameraCalibration> CameraCalibration = new();
    public static readonly BroadcastChannel<Data.Ssl.Vision.Geometry.BallModels> BallModels = new();

    // game-controller data
    public static readonly BroadcastChannel<Data.Ssl.Gc.Referee> RawReferee = new();

    // our published data
    public static readonly BroadcastChannel<Vision.Data.FilteredFrame> Vision = new();
    public static readonly BroadcastChannel<Referee.Data.State> Referee = new();
    public static readonly BroadcastChannel<Data.Robot.Command[]> RobotCommands = new();

    // debug
    public static readonly BroadcastChannel<Debug.Frame> Frames = new();
    public static readonly BroadcastChannel<Debug.Logging.Entry> Logs = new();
    public static readonly BroadcastChannel<Debug.Drawing.Command> Draws = new();
    public static readonly BroadcastChannel<Debug.Plotting.Command> Plots = new();
}