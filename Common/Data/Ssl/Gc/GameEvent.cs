using ProtoBuf;
using Tyr.Common.Data.Ssl.Gc.Events;

namespace Tyr.Common.Data.Ssl.Gc;

/// GameEvent contains exactly one game event
/// Each game event has optional and required fields. The required fields are mandatory to process the event.
/// Some optional fields are only used for visualization, others are required to determine the ball placement position.
/// If fields are missing that are required for the ball placement position, no ball placement command will be issued.
/// Fields are marked optional to make testing and extending of the protocol easier.
/// An autoRef should ideally set all fields, except if there are good reasons to not do so.
[ProtoContract]
public class GameEvent
{
    /// A globally unique id of the game event.
    [ProtoMember(50)] public string? Id { get; set; }
    
    /// The type of the game event.
    [ProtoMember(40)] public Events.Type? Type { get; set; }
    
    /// The origins of this game event.
    /// Empty, if it originates from game controller.
    /// Contains autoRef name(s), if it originates from one or more autoRefs.
    /// Ignored if sent by autoRef to game controller.
    [ProtoMember(41)] public List<string> Origins { get; set; } = [];
    
    /// Unix timestamp in microseconds when the event was created.
    [ProtoMember(49)] public ulong? CreatedTimestampMicroseconds { get; set; }

    /// Unix timestamp when the event was created.
    public Timestamp? CreatedTimestamp => CreatedTimestampMicroseconds.HasValue
        ? Timestamp.FromMicroseconds(CreatedTimestampMicroseconds.Value)
        : null;

    private DiscriminatedUnionObject _eventUnion;

    /// The ball left the field via touch line
    [ProtoMember(6)]
    public BallLeftField? BallLeftFieldTouchLine
    {
        get => _eventUnion.Is(6) ? (BallLeftField)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(6, value);
    }

    /// The ball left the field via goal line
    [ProtoMember(7)]
    public BallLeftField? BallLeftFieldGoalLine
    {
        get => _eventUnion.Is(7) ? (BallLeftField)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(7, value);
    }

    /// The ball left the field via goal line and a team committed an aimless kick
    [ProtoMember(11)]
    public AimlessKick? AimlessKick
    {
        get => _eventUnion.Is(11) ? (AimlessKick)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(11, value);
    }

    /// An attacker was located too near to the opponent defense area during stop or free kick
    [ProtoMember(19)]
    public AttackerTooCloseToDefenseArea? AttackerTooCloseToDefenseArea
    {
        get => _eventUnion.Is(19) ? (AttackerTooCloseToDefenseArea)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(19, value);
    }

    /// A defender other than the keeper was fully located inside its own defense and touched the ball
    [ProtoMember(31)]
    public DefenderInDefenseArea? DefenderInDefenseArea
    {
        get => _eventUnion.Is(31) ? (DefenderInDefenseArea)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(31, value);
    }

    /// A robot chipped the ball over the field boundary out of the playing surface
    [ProtoMember(43)]
    public BoundaryCrossing? BoundaryCrossing
    {
        get => _eventUnion.Is(43) ? (BoundaryCrossing)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(43, value);
    }

    /// A keeper held the ball in its defense area for too long
    [ProtoMember(13)]
    public KeeperHeldBall? KeeperHeldBall
    {
        get => _eventUnion.Is(13) ? (KeeperHeldBall)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(13, value);
    }

    /// A bot dribbled the ball too far
    [ProtoMember(17)]
    public BotDribbledBallTooFar? BotDribbledBallTooFar
    {
        get => _eventUnion.Is(17) ? (BotDribbledBallTooFar)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(17, value);
    }

    /// A bot pushed another bot over a significant distance
    [ProtoMember(24)]
    public BotPushedBot? BotPushedBot
    {
        get => _eventUnion.Is(24) ? (BotPushedBot)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(24, value);
    }

    /// A bot held the ball for too long
    [ProtoMember(26)]
    public BotHeldBallDeliberately? BotHeldBallDeliberately
    {
        get => _eventUnion.Is(26) ? (BotHeldBallDeliberately)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(26, value);
    }

    /// A bot tipped over
    [ProtoMember(27)]
    public BotTippedOver? BotTippedOver
    {
        get => _eventUnion.Is(27) ? (BotTippedOver)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(27, value);
    }

    /// A bot dropped parts
    [ProtoMember(51)]
    public BotDroppedParts? BotDroppedParts
    {
        get => _eventUnion.Is(51) ? (BotDroppedParts)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(51, value);
    }

    /// An attacker touched the ball inside the opponent defense area
    [ProtoMember(15)]
    public AttackerTouchedBallInDefenseArea? AttackerTouchedBallInDefenseArea
    {
        get => _eventUnion.Is(15) ? (AttackerTouchedBallInDefenseArea)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(15, value);
    }

    /// A bot kicked the ball too fast
    [ProtoMember(18)]
    public BotKickedBallTooFast? BotKickedBallTooFast
    {
        get => _eventUnion.Is(18) ? (BotKickedBallTooFast)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(18, value);
    }

    /// Two robots crashed into each other and one team was found guilty due to significant speed difference
    [ProtoMember(22)]
    public BotCrashUnique? BotCrashUnique
    {
        get => _eventUnion.Is(22) ? (BotCrashUnique)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(22, value);
    }

    /// Two robots crashed into each other with similar speeds
    [ProtoMember(21)]
    public BotCrashDrawn? BotCrashDrawn
    {
        get => _eventUnion.Is(21) ? (BotCrashDrawn)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(21, value);
    }

    /// A bot of the defending team got too close to the kick point during a free kick
    [ProtoMember(29)]
    public DefenderTooCloseToKickPoint? DefenderTooCloseToKickPoint
    {
        get => _eventUnion.Is(29) ? (DefenderTooCloseToKickPoint)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(29, value);
    }

    /// A bot moved too fast while the game was stopped
    [ProtoMember(28)]
    public BotTooFastInStop? BotTooFastInStop
    {
        get => _eventUnion.Is(28) ? (BotTooFastInStop)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(28, value);
    }

    /// A bot interfered the ball placement of the other team
    [ProtoMember(20)]
    public BotInterferedPlacement? BotInterferedPlacement
    {
        get => _eventUnion.Is(20) ? (BotInterferedPlacement)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(20, value);
    }

    /// A possible goal event
    [ProtoMember(39)]
    public Goal? PossibleGoal
    {
        get => _eventUnion.Is(39) ? (Goal)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(39, value);
    }

    /// A team shot a goal
    [ProtoMember(8)]
    public Goal? Goal
    {
        get => _eventUnion.Is(8) ? (Goal)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(8, value);
    }

    /// An invalid goal
    [ProtoMember(44)]
    public Goal? InvalidGoal
    {
        get => _eventUnion.Is(44) ? (Goal)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(44, value);
    }

    /// An attacker touched the ball multiple times when it was not allowed to
    [ProtoMember(14)]
    public AttackerDoubleTouchedBall? AttackerDoubleTouchedBall
    {
        get => _eventUnion.Is(14) ? (AttackerDoubleTouchedBall)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(14, value);
    }

    /// A team successfully placed the ball
    [ProtoMember(5)]
    public PlacementSucceeded? PlacementSucceeded
    {
        get => _eventUnion.Is(5) ? (PlacementSucceeded)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(5, value);
    }

    /// The penalty kick failed (by time or by keeper)
    [ProtoMember(45)]
    public PenaltyKickFailed? PenaltyKickFailed
    {
        get => _eventUnion.Is(45) ? (PenaltyKickFailed)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(45, value);
    }

    /// Game was stuck
    [ProtoMember(2)]
    public NoProgressInGame? NoProgressInGame
    {
        get => _eventUnion.Is(2) ? (NoProgressInGame)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(2, value);
    }

    /// Ball placement failed
    [ProtoMember(3)]
    public PlacementFailed? PlacementFailed
    {
        get => _eventUnion.Is(3) ? (PlacementFailed)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(3, value);
    }

    /// A team collected multiple yellow cards
    [ProtoMember(32)]
    public MultipleCards? MultipleCards
    {
        get => _eventUnion.Is(32) ? (MultipleCards)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(32, value);
    }

    /// A team collected multiple fouls, which results in a yellow card
    [ProtoMember(34)]
    public MultipleFouls? MultipleFouls
    {
        get => _eventUnion.Is(34) ? (MultipleFouls)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(34, value);
    }

    /// Bots are being substituted by a team
    [ProtoMember(37)]
    public BotSubstitution? BotSubstitution
    {
        get => _eventUnion.Is(37) ? (BotSubstitution)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(37, value);
    }

    /// A foul for excessive bot substitutions
    [ProtoMember(52)]
    public ExcessiveBotSubstitution? ExcessiveBotSubstitution
    {
        get => _eventUnion.Is(52) ? (ExcessiveBotSubstitution)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(52, value);
    }

    /// A team has too many robots on the field
    [ProtoMember(38)]
    public TooManyRobots? TooManyRobots
    {
        get => _eventUnion.Is(38) ? (TooManyRobots)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(38, value);
    }

    /// A challenge flag, requested by a team previously, is flagged
    [ProtoMember(46)]
    public ChallengeFlag? ChallengeFlag
    {
        get => _eventUnion.Is(46) ? (ChallengeFlag)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(46, value);
    }

    /// A challenge, flagged recently, has been handled by the referee
    [ProtoMember(48)]
    public ChallengeFlagHandled? ChallengeFlagHandled
    {
        get => _eventUnion.Is(48) ? (ChallengeFlagHandled)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(48, value);
    }

    /// An emergency stop, requested by team previously, occurred
    [ProtoMember(47)]
    public EmergencyStop? EmergencyStop
    {
        get => _eventUnion.Is(47) ? (EmergencyStop)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(47, value);
    }

    /// A team was found guilty for minor unsporting behavior
    [ProtoMember(35)]
    public UnsportingBehaviorMinor? UnsportingBehaviorMinor
    {
        get => _eventUnion.Is(35) ? (UnsportingBehaviorMinor)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(35, value);
    }

    /// A team was found guilty for major unsporting behavior
    [ProtoMember(36)]
    public UnsportingBehaviorMajor? UnsportingBehaviorMajor
    {
        get => _eventUnion.Is(36) ? (UnsportingBehaviorMajor)_eventUnion.Object : null;
        set => _eventUnion = new DiscriminatedUnionObject(36, value);
    }
}