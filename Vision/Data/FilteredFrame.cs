namespace Tyr.Vision.Data;

public record FilteredFrame
{
    public long Id { get; init; }
    public Timestamp Timestamp { get; init; }
    public FilteredBall Ball { get; init; }
    public List<FilteredRobot> Robots { get; init; } = [];
}