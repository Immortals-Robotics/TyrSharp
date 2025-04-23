namespace Tyr.Common.Debug.Drawing;

public record Meta(
    string ModuleName,
    Timestamp Timestamp,
    string? MemberName,
    string? FilePath,
    int LineNumber);