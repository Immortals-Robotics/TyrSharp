using System.Numerics;
using Tyr.Common.Debug.Plotting;

namespace Tyr.Tests.Common.Debug.Plotting;

public class PlotValueTests
{
    [Fact]
    public void From_ConvertsNumbersToNumberValues()
    {
        var value = PlotValue.From(12);

        Assert.Equal(PlotValueKind.Number, value.Kind);
        Assert.Equal(12, value.Number);
    }

    [Fact]
    public void From_PreservesVectorValues()
    {
        var value = PlotValue.From(new Vector2(3, 4));

        Assert.Equal(PlotValueKind.Vector2, value.Kind);
        Assert.Equal(new Vector2(3, 4), value.Vector2Value);
    }

    [Fact]
    public void From_RejectsUnsupportedTypes()
    {
        var exception = Assert.Throws<NotSupportedException>(() => PlotValue.From(DateTime.UnixEpoch));

        Assert.Contains("Supported types are numeric primitives, bool, Vector2, and Vector3.", exception.Message);
    }
}
