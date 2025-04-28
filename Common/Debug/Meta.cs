namespace Tyr.Common.Debug;

public record Meta(
    string ModuleName,
    string? Expression,
    string? MemberName,
    string? FilePath,
    int LineNumber)
{
    public static readonly Meta Empty = new(string.Empty, null, null, null, 0);
}