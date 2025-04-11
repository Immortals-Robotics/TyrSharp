namespace Tyr.Common.Runner;

public static class TimerResolution
{
    public static int CurrentMs { get; private set; } = GetDefaultResolution;
    public static float CurrentSeconds => CurrentMs / 1000f;

    public static void Set(int resolutionMs)
    {
        if (OperatingSystem.IsWindows())
        {
            CurrentMs = TimerResolutionWindows.Set(resolutionMs);
        }
    }

    public static void Reset()
    {
        if (OperatingSystem.IsWindows())
        {
            TimerResolutionWindows.Reset(CurrentMs);
            CurrentMs = GetDefaultResolution;
        }
    }

    private static int GetDefaultResolution => OperatingSystem.IsWindows() ? 16 : 1;
}