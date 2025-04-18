using System.Numerics;

namespace Tyr.Common.Shape;

public interface IShape
{
    float Circumference { get; }
    float Area { get; }

    float Distance(Vector2 point);
    bool Inside(Vector2 point, float margin = 0f);
    Vector2 NearestOutside(Vector2 point, float margin = 0f);
}