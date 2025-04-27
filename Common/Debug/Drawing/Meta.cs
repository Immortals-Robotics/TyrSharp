namespace Tyr.Common.Debug.Drawing;

public record Meta(
    string ModuleName,
    Timestamp Timestamp,
    string? MemberName,
    string? FilePath,
    int LineNumber)
{
    public static readonly Meta Empty = new(string.Empty, Timestamp.Zero, null, null, 0);
}