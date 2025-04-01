namespace Tyr.Common.Config;

public class Vision
{
    public const int CamCount = 8;
    public const int MaxBalls = 10;

    public float VisionFrameRate { get; set; } = 60.0f;
    public float PredictTime { get; set; } = 0.12f;

    public List<bool> UseCamera { get; set; } = new(CamCount);

    public int MaxBallHist { get; set; } = 10;

    public float MergeDistance { get; set; } = 70.0f;
    public float BallMergeDistance { get; set; } = 70.0f;
    public float MaxBall2FrameDist { get; set; } = 1000.0f;
    public float MaxRobot2FrameDist { get; set; } = 1000.0f;

    public int MaxRobotFrameNotSeen { get; set; } = 200;
    public int MaxBallFrameNotSeen { get; set; } = 120;

    public float CameraDelay { get; set; } = 0.0f;
    public float KickThreshold { get; set; } = 0.0f;
    public float ChipMaxError { get; set; } = 300000f;
    public int ChipMinRecords { get; set; } = 3;
    public int ChipMaxRecords { get; set; } = 200;
    public float ChipMaxVelZ { get; set; } = 7000.0f;
    public float KickerDepth { get; set; } = 150.0f;
    public bool UseBall3D { get; set; } = true;

    public float BallRollingFriction { get; set; } = 700.0f;
}