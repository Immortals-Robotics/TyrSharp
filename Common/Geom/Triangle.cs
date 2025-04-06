using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Geom;

[ProtoContract]
public struct Triangle
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
}