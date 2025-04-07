using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc;

public class TeamInfo
{
    [ProtoMember(1)] public string Name { get; set; }

    [ProtoMember(2)] public uint Score { get; set; }

    [ProtoMember(3)] public uint RedCards { get; set; }

    [ProtoMember(4)] public List<uint> YellowCardTimes { get; set; } = [];

    [ProtoMember(5)] public uint YellowCards { get; set; }

    [ProtoMember(6)] public uint Timeouts { get; set; }

    [ProtoMember(7)] public uint TimeoutTime { get; set; }

    [ProtoMember(8)] public uint Goalkeeper { get; set; }

    [ProtoMember(9)] public uint? FoulCounter { get; set; }

    [ProtoMember(10)] public uint? BallPlacementFailures { get; set; }

    [ProtoMember(12)] public bool? CanPlaceBall { get; set; }

    [ProtoMember(13)] public uint? MaxAllowedBots { get; set; }

    [ProtoMember(14)] public bool? BotSubstitutionIntent { get; set; }

    [ProtoMember(15)] public bool? BallPlacementFailuresReached { get; set; }

    [ProtoMember(16)] public bool? BotSubstitutionAllowed { get; set; }

    [ProtoMember(17)] public uint? BotSubstitutionsLeft { get; set; }

    [ProtoMember(18)] public uint? BotSubstitutionTimeLeft { get; set; }
}