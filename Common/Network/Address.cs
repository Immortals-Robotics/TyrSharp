namespace Tyr.Common.Network;

public record Address
{
    public string Ip { get; set; } = "127.0.0.1";
    public ushort Port { get; set; } = 0;
}