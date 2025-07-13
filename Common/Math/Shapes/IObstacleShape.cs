using System.Numerics;

namespace Tyr.Common.Math.Shapes;

public interface IObstacleShape
{
    bool Inside(Vector2 point, float margin = 0f);
    float Distance(Vector2 point);
}