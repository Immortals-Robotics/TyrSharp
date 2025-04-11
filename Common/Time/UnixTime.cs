namespace Tyr.Common.Time;

public static class UnixTime
{
    public static DateTime FromSeconds(double seconds)
        => DateTime.UnixEpoch.AddSeconds(seconds);

    public static DateTime FromMilliseconds(double milliseconds)
        => DateTime.UnixEpoch.AddMilliseconds(milliseconds);

    public static DateTime FromMicroseconds(long microseconds)
        => DateTime.UnixEpoch.AddTicks(microseconds * 10); // 1 tick = 100ns

    public static double ToSeconds(DateTime time)
        => (time - DateTime.UnixEpoch).TotalSeconds;

    public static double ToMilliseconds(DateTime time)
        => (time - DateTime.UnixEpoch).TotalMilliseconds;

    public static long ToMicroseconds(DateTime time)
        => (time - DateTime.UnixEpoch).Ticks / 10;
}