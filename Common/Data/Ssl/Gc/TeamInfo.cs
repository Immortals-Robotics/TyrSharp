using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc;

public struct TeamInfo
{
    public TeamInfo()
    {
    }

    [ProtoMember(1)] public string Name { get; set; } = "";

    [ProtoMember(2)] public uint Score { get; set; } = 0;

    [ProtoMember(3)] public uint RedCards { get; set; } = 0;

    [ProtoMember(4)] public List<uint> YellowCardTimes { get; set; } = [];

    [ProtoMember(5)] public uint YellowCards { get; set; } = 0;

    [ProtoMember(6)] public uint Timeouts { get; set; } = 0;

    [ProtoMember(7)] public uint TimeoutTime { get; set; } = 0;

    [ProtoMember(8)] public uint Goalkeeper { get; set; } = 0;

    [ProtoMember(9)] public uint? FoulCounter { get; set; } = null;

    [ProtoMember(10)] public uint? BallPlacementFailures { get; set; } = null;

    [ProtoMember(12)] public bool? CanPlaceBall { get; set; } = null;

    [ProtoMember(13)] public uint? MaxAllowedBots { get; set; } = null;

    [ProtoMember(14)] public bool? BotSubstitutionIntent { get; set; } = null;

    [ProtoMember(15)] public bool? BallPlacementFailuresReached { get; set; } = null;

    [ProtoMember(16)] public bool? BotSubstitutionAllowed { get; set; } = null;

    [ProtoMember(17)] public uint? BotSubstitutionsLeft { get; set; } = null;

    [ProtoMember(18)] public uint? BotSubstitutionTimeLeft { get; set; } = null;
}