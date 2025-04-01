namespace Tyr.Common.Network;

public record Address
{
    public string Ip { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 0;
}