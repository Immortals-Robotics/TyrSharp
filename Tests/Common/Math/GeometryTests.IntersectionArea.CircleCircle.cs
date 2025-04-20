using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;

namespace Tyr.Tests.Common.Math;

public partial class GeometryTests
{
    [Theory]
    [InlineData(0f, 0f, 1f, 3f, 0f, 1f, 0f)]
    [InlineData(0f, 0f, 1f, 2f, 0f, 1f, 0f)] // When circles just touch at a single point, the area is zero
    [InlineData(0f, 0f, 1f, 1.9f, 0f, 1f, 0.0418f)] // Small overlap should result in small area
    [InlineData(0f, 0f, 2f, 2f, 0f, 2f, 4.9135f)]
    [InlineData(0f, 0f, 2f, 1f, 0f, 2f, 8.6084f)]
    [InlineData(0f, 0f, 3f, 4f, 0f, 2f, 1.9898f)]
    [InlineData(0f, 0f, 2f, 0f, 2f, 2f, 4.9135f)]
    [InlineData(0f, 0f, 2f, 2f, 2f, 2f, 2.2832f)]
    [InlineData(0f, 0f, 2f, 0f, 0f, 2f, MathF.PI * 4f)] // πr²
    [InlineData(0f, 0f, 5f, 0f, 0f, 2f, MathF.PI * 4f)] // πr² of the smaller circle
    public void IntersectionArea_ReturnsCorrectArea(
        float c1X, float c1Y, float c1R,
        float c2X, float c2Y, float c2R,
        float expectedArea)
    {
        var circle1 = new Circle { Center = new Vector2(c1X, c1Y), Radius = c1R };
        var circle2 = new Circle { Center = new Vector2(c2X, c2Y), Radius = c2R };

        var area = Geometry.IntersectionArea(circle1, circle2);

        Assert.Equal(expectedArea, area, 0.0001f);
    }
}