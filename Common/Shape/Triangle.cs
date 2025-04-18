using ProtoBuf;
using System.Numerics;

namespace Tyr.Common.Shape;

[ProtoContract]
public struct Triangle : IShape
{
    [ProtoMember(1)] public Vector2 Corner1;
    [ProtoMember(2)] public Vector2 Corner2;
    [ProtoMember(3)] public Vector2 Corner3;

    public Triangle(Vector2 corner1, Vector2 corner2, Vector2 corner3)
    {
        Corner1 = corner1;
        Corner2 = corner2;
        Corner3 = corner3;

        // Sort corners clockwise around Corner1
        float area = (Corner2.X - Corner1.X) * (Corner3.Y - Corner1.Y)
                     - (Corner3.X - Corner1.X) * (Corner2.Y - Corner1.Y);

        if (area < 0)
        {
            (Corner2, Corner3) = (Corner3, Corner2); // swap
        }
    }

    public float Circumference
    {
        get
        {
            var a = Vector2.Distance(Corner1, Corner2);
            var b = Vector2.Distance(Corner2, Corner3);
            var c = Vector2.Distance(Corner3, Corner1);
            return a + b + c;
        }
    }

    public float Area =>
        // Using the shoelace formula
        MathF.Abs(
            (Corner1.X * (Corner2.Y - Corner3.Y) +
             Corner2.X * (Corner3.Y - Corner1.Y) +
             Corner3.X * (Corner1.Y - Corner2.Y)) * 0.5f);

    public float Distance(Vector2 point)
    {
        throw new NotImplementedException();
    }

    public bool Inside(Vector2 point, float margin = 0)
    {
        throw new NotImplementedException();
    }

    public Vector2 NearestOutside(Vector2 point, float margin = 0)
    {
        throw new NotImplementedException();
    }
}