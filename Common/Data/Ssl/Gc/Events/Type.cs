using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public enum Type
{
    UnknownGameEventType = 0,

    // Ball out of field events (stopping)
    BallLeftFieldTouchLine = 6, // triggered by autoRef
    BallLeftFieldGoalLine = 7, // triggered by autoRef
    AimlessKick = 11, // triggered by autoRef

    // Stopping Fouls
    AttackerTooCloseToDefenseArea = 19, // triggered by autoRef
    DefenderInDefenseArea = 31, // triggered by autoRef
    BoundaryCrossing = 41, // triggered by autoRef
    KeeperHeldBall = 13, // triggered by GC
    BotDribbledBallTooFar = 17, // triggered by autoRef

    BotPushedBot = 24, // triggered by human ref
    BotHeldBallDeliberately = 26, // triggered by human ref
    BotTippedOver = 27, // triggered by human ref
    BotDroppedParts = 47, // triggered by human ref

    // Non-Stopping Fouls
    AttackerTouchedBallInDefenseArea = 15, // triggered by autoRef
    BotKickedBallTooFast = 18, // triggered by autoRef
    BotCrashUnique = 22, // triggered by autoRef
    BotCrashDrawn = 21, // triggered by autoRef

    // Fouls while ball out of play
    DefenderTooCloseToKickPoint = 29, // triggered by autoRef
    BotTooFastInStop = 28, // triggered by autoRef
    BotInterferedPlacement = 20, // triggered by autoRef
    ExcessiveBotSubstitution = 48, // triggered by GC

    // Scoring goals
    PossibleGoal = 39, // triggered by autoRef
    Goal = 8, // triggered by GC
    InvalidGoal = 42, // triggered by GC

    // Other events
    AttackerDoubleTouchedBall = 14, // triggered by autoRef
    PlacementSucceeded = 5, // triggered by autoRef
    PenaltyKickFailed = 43, // triggered by GC and autoRef

    NoProgressInGame = 2, // triggered by GC
    PlacementFailed = 3, // triggered by GC
    MultipleCards = 32, // triggered by GC
    MultipleFouls = 34, // triggered by GC
    BotSubstitution = 37, // triggered by GC
    TooManyRobots = 38, // triggered by GC
    ChallengeFlag = 44, // triggered by GC
    ChallengeFlagHandled = 46, // triggered by GC
    EmergencyStop = 45, // triggered by GC

    UnsportingBehaviorMinor = 35, // triggered by human ref
    UnsportingBehaviorMajor = 36, // triggered by human ref
}