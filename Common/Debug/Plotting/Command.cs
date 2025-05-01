namespace Tyr.Common.Debug.Plotting;

public readonly record struct Command(
    string Id,
    object Value,
    string? Title,
    Meta Meta,
    Timestamp Timestamp
)
{
    public static Command Empty => new(string.Empty, null!, null, Meta.Empty, Timestamp.Now);

    public bool IsEmpty => string.IsNullOrEmpty(Id);
}