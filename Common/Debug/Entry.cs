namespace Tyr.Common.Debug;

public interface IEntry
{
    public Timestamp Timestamp { get; set; }
    public Meta Meta { get; set; }
    public string? ShardKey { get; }
}
