using Tyr.Common.Time;

namespace Tyr.Gui.Views;

public record PlaybackTime(bool Live, Timestamp EndTime, DeltaTime Offset)
{
    public Timestamp Time => EndTime + Offset;

    public (Timestamp Start, Timestamp End)? GetVisibleFrame(Tyr.Gui.Data.SwitchableDebugDb debugDb, string module)
    {
        if (!Live)
            return debugDb.GetFrameAt(module, Time);

        var newestFrame = debugDb.GetFrameAt(module, Timestamp.MaxValue);
        if (!newestFrame.HasValue)
            return null;

        var completedFrameTime = newestFrame.Value.Start - DeltaTime.FromNanoseconds(1);
        return debugDb.GetFrameAt(module, completedFrameTime) ?? newestFrame;
    }
}
