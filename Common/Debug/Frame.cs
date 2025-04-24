namespace Tyr.Common.Debug;

public readonly record struct Frame
{
    public string ModuleName { get; init; }
    public Timestamp StartTimestamp { get; init; }
}