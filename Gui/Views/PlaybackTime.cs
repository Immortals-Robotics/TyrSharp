using Tyr.Common.Time;

namespace Tyr.Gui.Views;

public record PlaybackTime(bool Live, Timestamp StartTime, DeltaTime Delta)
{
    public Timestamp Time => StartTime + Delta;
}