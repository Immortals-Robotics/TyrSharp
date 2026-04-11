namespace Tyr.Soccer.Robot;

[Flags]
public enum NavigationFlags
{
    None = 0,
    NoObstacles = 1 << 1,
    BallObstacle = 1 << 2,
    BallMediumObstacle = 1 << 3,
    BallSmallObstacle = 1 << 4,
    NoBreak = 1 << 5,
    NoOwnPenaltyArea = 1 << 6,
    NoExtraMargin = 1 << 7,
    BallPlacementLine = 1 << 8,
    TheirHalf = 1 << 9,
    DynamicBallObstacle = 1 << 10,
    NoBallObstacle = 1 << 11,
}
