using ProtoBuf;
using Tyr.Common.Shape;

namespace Tyr.Common.Debug;

[ProtoContract]
public readonly struct DrawCommand<T>(
    T shape,
    Color color,
    bool filled,
    float thickness,
    string? memberName = null,
    string? filePath = null,
    int lineNumber = 0)
    where T : struct, IShape
{
    [ProtoMember(1)] public readonly T Shape = shape;
    [ProtoMember(2)] public readonly Color Color = color;
    [ProtoMember(3)] public readonly bool Filled = filled;
    [ProtoMember(4)] public readonly float Thickness = thickness;

    [ProtoMember(5)] public readonly string? MemberName = memberName;
    [ProtoMember(6)] public readonly string? FilePath = filePath;
    [ProtoMember(7)] public readonly int LineNumber = lineNumber;
}

public static class DrawCommands<T> where T : struct, IShape
{
    public static readonly List<DrawCommand<T>> Commands = [];
}