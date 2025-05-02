using Tomlet;
using Tomlet.Models;

namespace Tyr.Common.Time;

public readonly record struct DeltaTime : IComparable<DeltaTime>
{
    public long Nanoseconds { get; }

    static DeltaTime()
    {
        TomletMain.RegisterMapper(
            dt => new TomlLong(dt.Nanoseconds),
            toml => FromNanoseconds(((TomlLong)toml).Value));
    }

    private DeltaTime(long nanoseconds) => Nanoseconds = nanoseconds;

    public static readonly DeltaTime Zero = new(0);

    public static DeltaTime FromSeconds(double seconds) => new((long)(seconds * 1e9));
    public static DeltaTime FromMilliseconds(double ms) => new((long)(ms * 1e6));
    public static DeltaTime FromMicroseconds(double us) => new((long)(us * 1e3));
    public static DeltaTime FromNanoseconds(long ns) => new(ns);

    public double Seconds => Nanoseconds / 1e9;
    public double Milliseconds => Nanoseconds / 1e6;
    public double Microseconds => Nanoseconds / 1e3;

    public TimeSpan ToTimeSpan() => TimeSpan.FromTicks(Nanoseconds / TimeSpan.NanosecondsPerTick);

    public static DeltaTime Min(DeltaTime a, DeltaTime b) => a < b ? a : b;
    public static DeltaTime Max(DeltaTime a, DeltaTime b) => a > b ? a : b;

    public static DeltaTime Clamp(DeltaTime value, DeltaTime min, DeltaTime max)
        => Max(min, Min(max, value));

    // Arithmetic
    public static DeltaTime operator +(DeltaTime a, DeltaTime b) => new(a.Nanoseconds + b.Nanoseconds);
    public static DeltaTime operator -(DeltaTime a, DeltaTime b) => new(a.Nanoseconds - b.Nanoseconds);
    public static DeltaTime operator *(DeltaTime a, double scalar) => new((long)(a.Nanoseconds * scalar));
    public static DeltaTime operator *(double scalar, DeltaTime a) => new((long)(a.Nanoseconds * scalar));
    public static DeltaTime operator /(DeltaTime a, double scalar) => new((long)(a.Nanoseconds / scalar));
    public static double operator /(DeltaTime a, DeltaTime b) => (double)a.Nanoseconds / b.Nanoseconds;

    public static bool operator <(DeltaTime a, DeltaTime b) => a.Nanoseconds < b.Nanoseconds;
    public static bool operator >(DeltaTime a, DeltaTime b) => a.Nanoseconds > b.Nanoseconds;
    public static bool operator <=(DeltaTime a, DeltaTime b) => a.Nanoseconds <= b.Nanoseconds;
    public static bool operator >=(DeltaTime a, DeltaTime b) => a.Nanoseconds >= b.Nanoseconds;


    public override string ToString() => $"{Seconds:F6}s";

    public int CompareTo(DeltaTime other) => Nanoseconds.CompareTo(other.Nanoseconds);
}