namespace Tyr.Common.Data.Robot;

public record DiscoveredRobot
{
    public int Id { get; init; }
    public string Ip { get; init; } = string.Empty;
    public Timestamp LastSeen { get; init; }
    public bool IsOnline { get; init; }
}
