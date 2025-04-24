using Tyr.Common.Time;

namespace Tyr.Common.Runner;

public static class TimerResolution
{
    public static DeltaTime Current { get; private set; } = GetDefaultResolution;

    public static void Set(DeltaTime resolution)
    {
        if (OperatingSystem.IsWindows())
        {
            var resolutionMs = (int)System.Math.Round(resolution.Milliseconds);
            var actualMs = TimerResolutionWindows.Set(resolutionMs);
            Current = DeltaTime.FromMilliseconds(actualMs);
        }
    }

    public static void Reset()
    {
        if (OperatingSystem.IsWindows())
        {
            var currentMs = (int)System.Math.Round(Current.Milliseconds);
            TimerResolutionWindows.Reset(currentMs);
            Current = GetDefaultResolution;
        }
    }

    private static DeltaTime GetDefaultResolution => DeltaTime.FromMilliseconds(OperatingSystem.IsWindows() ? 16 : 1);
}