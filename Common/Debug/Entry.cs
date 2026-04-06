namespace Tyr.Common.Debug;

public interface IEntry
{
    public bool IsEmpty { get; }
    public Timestamp Timestamp { get; }
}