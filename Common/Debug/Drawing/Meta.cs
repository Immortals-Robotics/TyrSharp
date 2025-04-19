namespace Tyr.Common.Debug.Drawing;

public record Meta(
    string Category,
    DateTime Time,
    int FrameId,
    string? MemberName,
    string? FilePath,
    int LineNumber);