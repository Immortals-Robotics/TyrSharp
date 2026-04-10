using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Plotting;

public enum PlotValueKind
{
    None,
    Number,
    Boolean,
    Vector2,
    Vector3,
}

[MemoryPackable]
public readonly partial record struct PlotValue
{
    public PlotValueKind Kind { get; init; }
    public double Number { get; init; }
    public bool Boolean { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }

    [MemoryPackIgnore]
    public static PlotValue Empty => default;

    [MemoryPackIgnore]
    public Vector2 Vector2Value => new(X, Y);

    [MemoryPackIgnore]
    public Vector3 Vector3Value => new(X, Y, Z);

    public static PlotValue From<T>(T value)
    {
        if (value is null)
            return ThrowUnsupported(typeof(T));

        return value switch
        {
            PlotValue plotValue => plotValue,
            bool boolean => FromBoolean(boolean),
            Half number => FromNumber((double)number),
            byte number => FromNumber(number),
            sbyte number => FromNumber(number),
            short number => FromNumber(number),
            ushort number => FromNumber(number),
            int number => FromNumber(number),
            uint number => FromNumber(number),
            long number => FromNumber(number),
            ulong number => FromNumber(number),
            float number => FromNumber(number),
            double number => FromNumber(number),
            decimal number => FromNumber((double)number),
            nint number => FromNumber(number),
            nuint number => FromNumber((double)number),
            Vector2 vector2 => FromVector(vector2),
            Vector3 vector3 => FromVector(vector3),
            _ => ThrowUnsupported(value.GetType())
        };
    }

    public static PlotValue FromNumber(double number) => new()
    {
        Kind = PlotValueKind.Number,
        Number = number,
    };

    public static PlotValue FromBoolean(bool boolean) => new()
    {
        Kind = PlotValueKind.Boolean,
        Boolean = boolean,
    };

    public static PlotValue FromVector(Vector2 vector) => new()
    {
        Kind = PlotValueKind.Vector2,
        X = vector.X,
        Y = vector.Y,
    };

    public static PlotValue FromVector(Vector3 vector) => new()
    {
        Kind = PlotValueKind.Vector3,
        X = vector.X,
        Y = vector.Y,
        Z = vector.Z,
    };

    [MemoryPackIgnore]
    public bool IsEmpty => Kind == PlotValueKind.None;

    private static PlotValue ThrowUnsupported(Type type) => throw new NotSupportedException(
        $"Plotting values of type '{type.FullName}' is not supported. " +
        "Supported types are numeric primitives, bool, Vector2, and Vector3.");
}
