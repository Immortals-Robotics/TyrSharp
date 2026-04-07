namespace Tyr.Common.Debug.Plotting;

public record struct Command(
    string Id,
    object Value,
    string? Title,
    Meta Meta,
    Timestamp Timestamp
) : IEntry
{
    public static Command Empty => new(string.Empty, null!, null, Meta.Empty, Timestamp.Now);

    public bool IsEmpty => string.IsNullOrEmpty(Id);
}