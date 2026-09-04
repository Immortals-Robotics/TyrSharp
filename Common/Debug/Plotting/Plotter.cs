using System.Runtime.CompilerServices;
namespace Tyr.Common.Debug.Plotting;

public class Plotter(string module)
{
    public void Plot<T>(InternedStringHandler id, T value, string? layer = null,
        [CallerArgumentExpression("value")] string? expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
        => Plot(id.ToInternedString(), value, layer, expression, member, file, line);

    public void Plot<T>(string id, T value, string? layer = null,
        [CallerArgumentExpression("value")] string? expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var meta = Meta.GetOrCreate(module, layer, file, member, line, expression);

        id = InternedStringCache.GetOrAdd(id);

        var command = new Command
        {
            Value = PlotValue.From(value),
            Meta = meta,
            ShardKey = id,
            Timestamp = Timestamp.Now,
        };

        DebugBus.Publish(command);
    }
}
