using Tyr.Common.Time;

namespace Tyr.Gui.Views;

public record PlaybackTime(bool Live, Timestamp EndTime, DeltaTime Offset)
{
    public Timestamp Time => EndTime + Offset;
}
