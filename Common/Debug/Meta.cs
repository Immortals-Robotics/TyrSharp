namespace Tyr.Common.Debug;

public record Meta(
    string ModuleName,
    Timestamp Timestamp,
    string? Expression,
    string? MemberName,
    string? FilePath,
    int LineNumber)
{
    public static readonly Meta Empty = new(string.Empty, Timestamp.Zero, null, null, null, 0);
}