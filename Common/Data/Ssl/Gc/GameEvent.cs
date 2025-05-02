using ProtoBuf;
using Tyr.Common.Data.Ssl.Gc.Events;

namespace Tyr.Common.Data.Ssl.Gc;

[ProtoContract]
public class GameEvent
{
    [ProtoMember(50)] public string? Id { get; set; }
    [ProtoMember(40)] public Events.Type? Type { get; set; }
    [ProtoMember(41)] public List<string> Origins { get; set; } = [];
    [ProtoMember(49)] public ulong? CreatedTimestampMicroseconds { get; set; }

    public Timestamp? CreatedTimestamp => CreatedTimestampMicroseconds.HasValue
        ? Timestamp.FromMicroseconds(CreatedTimestampMicroseconds.Value)
        : null;

    private DiscriminatedUnionObject _eventUnion;

    [ProtoMember(6)]
    public BallLeftField? BallLeftFieldTouchLine
    {
        get => _eventUnion.Is(6) ? (BallLeftField)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(6, value);
    }

    [ProtoMember(7)]
    public BallLeftField? BallLeftFieldGoalLine
    {
        get => _eventUnion.Is(7) ? (BallLeftField)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(7, value);
    }

    [ProtoMember(11)]
    public AimlessKick? AimlessKick
    {
        get => _eventUnion.Is(11) ? (AimlessKick)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(11, value);
    }

    [ProtoMember(19)]
    public AttackerTooCloseToDefenseArea? AttackerTooCloseToDefenseArea
    {
        get => _eventUnion.Is(19) ? (AttackerTooCloseToDefenseArea)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(19, value);
    }

    [ProtoMember(31)]
    public DefenderInDefenseArea? DefenderInDefenseArea
    {
        get => _eventUnion.Is(31) ? (DefenderInDefenseArea)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(31, value);
    }

    [ProtoMember(43)]
    public BoundaryCrossing? BoundaryCrossing
    {
        get => _eventUnion.Is(43) ? (BoundaryCrossing)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(43, value);
    }

    [ProtoMember(13)]
    public KeeperHeldBall? KeeperHeldBall
    {
        get => _eventUnion.Is(13) ? (KeeperHeldBall)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(13, value);
    }

    [ProtoMember(17)]
    public BotDribbledBallTooFar? BotDribbledBallTooFar
    {
        get => _eventUnion.Is(17) ? (BotDribbledBallTooFar)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(17, value);
    }

    [ProtoMember(24)]
    public BotPushedBot? BotPushedBot
    {
        get => _eventUnion.Is(24) ? (BotPushedBot)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(24, value);
    }

    [ProtoMember(26)]
    public BotHeldBallDeliberately? BotHeldBallDeliberately
    {
        get => _eventUnion.Is(26) ? (BotHeldBallDeliberately)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(26, value);
    }

    [ProtoMember(27)]
    public BotTippedOver? BotTippedOver
    {
        get => _eventUnion.Is(27) ? (BotTippedOver)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(27, value);
    }

    [ProtoMember(51)]
    public BotDroppedParts? BotDroppedParts
    {
        get => _eventUnion.Is(51) ? (BotDroppedParts)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(51, value);
    }

    [ProtoMember(15)]
    public AttackerTouchedBallInDefenseArea? AttackerTouchedBallInDefenseArea
    {
        get => _eventUnion.Is(15) ? (AttackerTouchedBallInDefenseArea)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(15, value);
    }

    [ProtoMember(18)]
    public BotKickedBallTooFast? BotKickedBallTooFast
    {
        get => _eventUnion.Is(18) ? (BotKickedBallTooFast)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(18, value);
    }

    [ProtoMember(22)]
    public BotCrashUnique? BotCrashUnique
    {
        get => _eventUnion.Is(22) ? (BotCrashUnique)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(22, value);
    }

    [ProtoMember(21)]
    public BotCrashDrawn? BotCrashDrawn
    {
        get => _eventUnion.Is(21) ? (BotCrashDrawn)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(21, value);
    }

    [ProtoMember(29)]
    public DefenderTooCloseToKickPoint? DefenderTooCloseToKickPoint
    {
        get => _eventUnion.Is(29) ? (DefenderTooCloseToKickPoint)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(29, value);
    }

    [ProtoMember(28)]
    public BotTooFastInStop? BotTooFastInStop
    {
        get => _eventUnion.Is(28) ? (BotTooFastInStop)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(28, value);
    }

    [ProtoMember(20)]
    public BotInterferedPlacement? BotInterferedPlacement
    {
        get => _eventUnion.Is(20) ? (BotInterferedPlacement)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(20, value);
    }

    [ProtoMember(39)]
    public Goal? PossibleGoal
    {
        get => _eventUnion.Is(39) ? (Goal)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(39, value);
    }

    [ProtoMember(8)]
    public Goal? Goal
    {
        get => _eventUnion.Is(8) ? (Goal)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(8, value);
    }

    [ProtoMember(44)]
    public Goal? InvalidGoal
    {
        get => _eventUnion.Is(44) ? (Goal)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(44, value);
    }

    [ProtoMember(14)]
    public AttackerDoubleTouchedBall? AttackerDoubleTouchedBall
    {
        get => _eventUnion.Is(14) ? (AttackerDoubleTouchedBall)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(14, value);
    }

    [ProtoMember(5)]
    public PlacementSucceeded? PlacementSucceeded
    {
        get => _eventUnion.Is(5) ? (PlacementSucceeded)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(5, value);
    }

    [ProtoMember(45)]
    public PenaltyKickFailed? PenaltyKickFailed
    {
        get => _eventUnion.Is(45) ? (PenaltyKickFailed)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(45, value);
    }

    [ProtoMember(2)]
    public NoProgressInGame? NoProgressInGame
    {
        get => _eventUnion.Is(2) ? (NoProgressInGame)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(2, value);
    }

    [ProtoMember(3)]
    public PlacementFailed? PlacementFailed
    {
        get => _eventUnion.Is(3) ? (PlacementFailed)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(3, value);
    }

    [ProtoMember(32)]
    public MultipleCards? MultipleCards
    {
        get => _eventUnion.Is(32) ? (MultipleCards)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(32, value);
    }

    [ProtoMember(34)]
    public MultipleFouls? MultipleFouls
    {
        get => _eventUnion.Is(34) ? (MultipleFouls)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(34, value);
    }

    [ProtoMember(37)]
    public BotSubstitution? BotSubstitution
    {
        get => _eventUnion.Is(37) ? (BotSubstitution)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(37, value);
    }

    [ProtoMember(52)]
    public ExcessiveBotSubstitution? ExcessiveBotSubstitution
    {
        get => _eventUnion.Is(52) ? (ExcessiveBotSubstitution)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(52, value);
    }

    [ProtoMember(38)]
    public TooManyRobots? TooManyRobots
    {
        get => _eventUnion.Is(38) ? (TooManyRobots)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(38, value);
    }

    [ProtoMember(46)]
    public ChallengeFlag? ChallengeFlag
    {
        get => _eventUnion.Is(46) ? (ChallengeFlag)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(46, value);
    }

    [ProtoMember(48)]
    public ChallengeFlagHandled? ChallengeFlagHandled
    {
        get => _eventUnion.Is(48) ? (ChallengeFlagHandled)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(48, value);
    }

    [ProtoMember(47)]
    public EmergencyStop? EmergencyStop
    {
        get => _eventUnion.Is(47) ? (EmergencyStop)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(47, value);
    }

    [ProtoMember(35)]
    public UnsportingBehaviorMinor? UnsportingBehaviorMinor
    {
        get => _eventUnion.Is(35) ? (UnsportingBehaviorMinor)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(35, value);
    }

    [ProtoMember(36)]
    public UnsportingBehaviorMajor? UnsportingBehaviorMajor
    {
        get => _eventUnion.Is(36) ? (UnsportingBehaviorMajor)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(36, value);
    }
}