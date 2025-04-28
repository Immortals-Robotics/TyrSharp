using System.Runtime.CompilerServices;
using Tyr.Common.Dataflow;

namespace Tyr.Common.Debug.Plotting;

public class Plotter(string moduleName)
{
    public void Plot<T>(string id, T value, string? title = null,
        [CallerArgumentExpression("value")] string? valueExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var meta = new Meta(moduleName, Timestamp.Now, valueExpression,
            memberName, filePath, lineNumber);
        var command = new Command(id, value!, title, meta);

        Hub.Plots.Publish(command);
    }
}