using System.Runtime.InteropServices;

namespace Tyr.Common.Runner;

public static class TimerResolutionWindows
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TimeCaps
    {
        public int PeriodMin;
        public int PeriodMax;
    }

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
    public static extern uint TimeBeginPeriod(uint milliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
    public static extern uint TimeEndPeriod(uint milliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeGetDevCaps")]
    public static extern int TimeGetDevCaps(ref TimeCaps ptc, int cbtc);

    public static TimeCaps GetCaps()
    {
        TimeCaps caps = new();

        var result = TimeGetDevCaps(ref caps, Marshal.SizeOf(typeof(TimeCaps)));
        if (result != 0)
            throw new InvalidOperationException($"timeGetDevCaps failed with code {result}");

        return caps;
    }

    public static int Set(int resolutionMs)
    {
        // Clamp the requested resolution to min/max allowed
        var caps = GetCaps();
        var clamped = System.Math.Clamp(resolutionMs, caps.PeriodMin, caps.PeriodMax);

        var result = TimeBeginPeriod((uint)clamped);
        if (result != 0)
            throw new InvalidOperationException($"timeBeginPeriod failed with code {result}");

        return clamped;
    }

    public static void Reset(int resolutionMs)
    {
        var result = TimeEndPeriod((uint)resolutionMs);
        if (result != 0)
            throw new InvalidOperationException($"timeEndPeriod failed with code {result}");
    }
}