using System.Numerics;
using System.Runtime.CompilerServices;
using Tyr.Common.Dataflow;

namespace Tyr.Common.Debug.Plotting;

public class Plotter(string moduleName)
{
    private void Draw(string id, object value, string? valueExpression,
        string? memberName, string? filePath, int lineNumber)
    {
        var meta = new Meta(moduleName, Timestamp.Now, valueExpression,
            memberName, filePath, lineNumber);
        var command = new Command(id, value, meta);

        Hub.Plots.Publish(command);
    }

    public void Plot(string id, float value,
        [CallerArgumentExpression("value")] string? valueExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        Draw(id, value, valueExpression, memberName, filePath, lineNumber);
    }

    public void Plot(string id, Vector2 value,
        [CallerArgumentExpression("value")] string? valueExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        Draw(id, value, valueExpression, memberName, filePath, lineNumber);
    }
}