namespace Tyr.Common.Data.Ssl.Gc;

public enum Command
{
    Halt = 0,
    Stop = 1,
    NormalStart = 2,
    ForceStart = 3,
    PrepareKickoffYellow = 4,
    PrepareKickoffBlue = 5,
    PreparePenaltyYellow = 6,
    PreparePenaltyBlue = 7,
    DirectFreeYellow = 8,
    DirectFreeBlue = 9,
    TimeoutYellow = 12,
    TimeoutBlue = 13,
    BallPlacementYellow = 16,
    BallPlacementBlue = 17
}