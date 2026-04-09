using System.Runtime.CompilerServices;
using Tyr.Common.Dataflow;

namespace Tyr.Common.Debug.Plotting;

public class Plotter(string module)
{
    public void Plot<T>(InternedStringHandler id, T value, string? title = null, string? layer = null,
        [CallerArgumentExpression("value")] string? expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
        => Plot(id.ToInternedString(), value, title, layer, expression, member, file, line);

    public void Plot<T>(string id, T value, string? title = null, string? layer = null,
        [CallerArgumentExpression("value")] string? expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var meta = Meta.GetOrCreate(module, layer, file, member, line, expression);

        id = string.Intern(id);
        title = title != null ? string.Intern(title) : null;

        var command = new Command
        {
            Value = PlotValue.From(value),
            Title = title,
            Meta = meta,
            ShardKey = id,
            Timestamp = Timestamp.Now,
        };

        Hub.Plots.Publish(command);
    }
}
