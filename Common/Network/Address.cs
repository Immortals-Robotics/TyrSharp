namespace Tyr.Common.Network;

public record Address
{
    public required string Ip { get; init; }
    public int Port { get; init; }
}