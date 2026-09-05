using ProtoBuf;
using Tyr.Common.Data;

namespace Tyr.Control.GameController;

[ProtoContract]
public sealed class ControllerReply
{
    [ProtoMember(1)] public ControllerStatusCode? StatusCode { get; set; }
    [ProtoMember(2)] public string? Reason { get; set; }
    [ProtoMember(3)] public string? NextToken { get; set; }
}

public enum ControllerStatusCode
{
    Unknown = 0,
    Ok = 1,
    Rejected = 2,
}

[ProtoContract]
public sealed class RemoteControlRegistration
{
    // proto2 required — TeamColor values match Team enum (UNKNOWN=0, YELLOW=1, BLUE=2)
    [ProtoMember(1, IsRequired = true)] public TeamColor Team { get; set; }
}

/// <summary>
/// Message sent from our client to the game controller.
/// Only one of the oneof fields should be set per message.
/// </summary>
[ProtoContract]
public sealed class RemoteControlToController
{
    [ProtoMember(2)] public GcRequest? Request { get; set; }
    [ProtoMember(3)] public int? DesiredKeeper { get; set; }
    // bool? ensures false is serialized on wire (needed to cancel an active request)
    [ProtoMember(4)] public bool? RequestRobotSubstitution { get; set; }
    [ProtoMember(5)] public bool? RequestTimeout { get; set; }
    [ProtoMember(6)] public bool? RequestEmergencyStop { get; set; }

    public static RemoteControlToController Ping()              => new() { Request = GcRequest.Ping };
    public static RemoteControlToController ChallengeFlag()     => new() { Request = GcRequest.ChallengeFlag };
    public static RemoteControlToController StopTimeout()       => new() { Request = GcRequest.StopTimeout };
    public static RemoteControlToController FailBallPlacement() => new() { Request = GcRequest.FailBallPlacement };
    public static RemoteControlToController SetKeeper(int id)   => new() { DesiredKeeper = id };

    public static RemoteControlToController SetRobotSubstitution(bool value) =>
        new() { RequestRobotSubstitution = value };

    public static RemoteControlToController SetTimeout(bool value) =>
        new() { RequestTimeout = value };

    public static RemoteControlToController SetEmergencyStop(bool value) =>
        new() { RequestEmergencyStop = value };
}

public enum GcRequest
{
    Unknown = 0,
    Ping = 1,
    ChallengeFlag = 2,
    StopTimeout = 3,
    FailBallPlacement = 4,
}

/// <summary>
/// Wrapper for all messages from the game controller to our client.
/// </summary>
[ProtoContract]
public sealed class ControllerToRemoteControl
{
    [ProtoMember(1)] public ControllerReply? ControllerReply { get; set; }
    [ProtoMember(2)] public RemoteControlTeamState? State { get; set; }
}

[ProtoContract]
public sealed class RemoteControlTeamState
{
    [ProtoMember(12)] public TeamColor Team { get; set; }
    [ProtoMember(1)]  public List<GcRequestType> AvailableRequests { get; set; } = [];
    [ProtoMember(2)]  public List<GcRequestType> ActiveRequests    { get; set; } = [];
    [ProtoMember(3)]  public int?   KeeperId              { get; set; }
    [ProtoMember(4)]  public float? EmergencyStopIn       { get; set; }
    [ProtoMember(5)]  public int?   TimeoutsLeft          { get; set; }
    [ProtoMember(10)] public float? TimeoutTimeLeft       { get; set; }
    [ProtoMember(6)]  public int?   ChallengeFlagsLeft    { get; set; }
    [ProtoMember(7)]  public int?   MaxRobots             { get; set; }
    [ProtoMember(9)]  public int?   RobotsOnField         { get; set; }
    [ProtoMember(8)]  public List<float> YellowCardsDue   { get; set; } = [];
    [ProtoMember(11)] public bool?  CanSubstituteRobot    { get; set; }
    [ProtoMember(13)] public uint?  BotSubstitutionsLeft  { get; set; }
    [ProtoMember(14)] public float? BotSubstitutionTimeLeft { get; set; }
}

public enum GcRequestType
{
    Unknown = 0,
    EmergencyStop = 1,
    RobotSubstitution = 2,
    Timeout = 3,
    ChallengeFlag = 4,
    ChangeKeeperId = 5,
    StopTimeout = 6,
    FailBallPlacement = 7,
}
