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
        var meta = new Meta(moduleName, valueExpression,
            memberName, filePath, lineNumber);

        id = string.Intern(id);
        title = title != null ? string.Intern(title) : null;

        var command = new Command(id, value!, title, meta, Timestamp.Now);

        Hub.Plots.Publish(command);
    }
}