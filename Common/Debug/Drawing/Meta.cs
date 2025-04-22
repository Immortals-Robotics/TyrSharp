namespace Tyr.Common.Debug.Drawing;

public record Meta(
    string Category,
    Timestamp Timestamp,
    int FrameId,
    string? MemberName,
    string? FilePath,
    int LineNumber);