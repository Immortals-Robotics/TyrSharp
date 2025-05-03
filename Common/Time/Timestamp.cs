using Tomlet;
using Tomlet.Models;

namespace Tyr.Common.Time;

public readonly record struct Timestamp : IComparable<Timestamp>
{
    public long Nanoseconds { get; }

    static Timestamp()
    {
        TomletMain.RegisterMapper(
            time => new TomlLong(time.Nanoseconds),
            toml => FromNanoseconds(((TomlLong)toml).Value));
    }

    private Timestamp(long nanoseconds) => Nanoseconds = nanoseconds;

    public static Timestamp FromSeconds(double seconds) => new((long)(seconds * 1e9));
    public static Timestamp FromMilliseconds(double ms) => new((long)(ms * 1e6));
    public static Timestamp FromMicroseconds(double us) => new((long)(us * 1e3));
    public static Timestamp FromNanoseconds(long ns) => new(ns);

    public static Timestamp FromDateTime(DateTime dateTime) =>
        FromNanoseconds((dateTime - DateTime.UnixEpoch).Ticks * TimeSpan.NanosecondsPerTick);

    public static Timestamp FromDateTimeOffset(DateTimeOffset dateTimeOffset) =>
        FromNanoseconds((dateTimeOffset - DateTimeOffset.UnixEpoch).Ticks * TimeSpan.NanosecondsPerTick);

    public DateTime ToDateTime() => DateTime.UnixEpoch.AddTicks(Nanoseconds / TimeSpan.NanosecondsPerTick);

    public static Timestamp Zero => new(0);
    public static Timestamp MaxValue => new(long.MaxValue);
    public static Timestamp Now => FromDateTime(DateTime.UtcNow);

    public double Hours => Seconds / 3600;
    public double Minutes => Seconds / 60;
    public double Seconds => Nanoseconds / 1e9;
    public double Milliseconds => Nanoseconds / 1e6;
    public double Microseconds => Nanoseconds / 1e3;

    public double NormalizedHours => Minutes / 60 % 24;
    public double NormalizedMinutes => Minutes % 60;
    public double NormalizedSeconds => Seconds % 60;
    public double NormalizedMilliseconds => Milliseconds % 1000;

    public static Timestamp Min(Timestamp a, Timestamp b) => a < b ? a : b;
    public static Timestamp Max(Timestamp a, Timestamp b) => a > b ? a : b;

    public static Timestamp Clamp(Timestamp value, Timestamp min, Timestamp max)
        => Max(min, Min(max, value));

    public static DeltaTime operator -(Timestamp a, Timestamp b) =>
        DeltaTime.FromNanoseconds(a.Nanoseconds - b.Nanoseconds);

    public static Timestamp operator +(Timestamp a, DeltaTime b) =>
        FromNanoseconds(a.Nanoseconds + b.Nanoseconds);

    public static Timestamp operator -(Timestamp a, DeltaTime b) =>
        FromNanoseconds(a.Nanoseconds - b.Nanoseconds);

    public static bool operator <(Timestamp a, Timestamp b) => a.Nanoseconds < b.Nanoseconds;
    public static bool operator >(Timestamp a, Timestamp b) => a.Nanoseconds > b.Nanoseconds;
    public static bool operator <=(Timestamp a, Timestamp b) => a.Nanoseconds <= b.Nanoseconds;
    public static bool operator >=(Timestamp a, Timestamp b) => a.Nanoseconds >= b.Nanoseconds;

    public int CompareTo(Timestamp other) => Nanoseconds.CompareTo(other.Nanoseconds);

    public override string ToString() => $"{Nanoseconds}ns";
}