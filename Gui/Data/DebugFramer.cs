using Tyr.Common.Dataflow;
using Tyr.Common.Time;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Data;

public class DebugFramer
{
    private readonly Subscriber<Debug.Drawing.Command> _drawCommandsSubscriber = Hub.Draws.Subscribe(Mode.All);
    private readonly Subscriber<Debug.Frame> _frameSubscriber = Hub.Frames.Subscribe(Mode.All);

    public Dictionary<string, ModuleDebugFramer> Modules { get; } = [];

    public Timestamp StartTime { get; private set; }
    public Timestamp EndTime { get; private set; }
    public DeltaTime Duration => EndTime - StartTime;

    private ModuleDebugFramer GetOrCreateModuleFramer(string moduleName)
    {
        if (!Modules.TryGetValue(moduleName, out var moduleFramer))
        {
            moduleFramer = new ModuleDebugFramer();
            Modules[moduleName] = moduleFramer;
        }

        return moduleFramer;
    }

    internal void Tick()
    {
        while (_frameSubscriber.Reader.TryRead(out var frame))
        {
            GetOrCreateModuleFramer(frame.ModuleName).OnFrame(frame);
        }

        while (_drawCommandsSubscriber.Reader.TryRead(out var draw))
        {
            GetOrCreateModuleFramer(draw.Meta.ModuleName).OnDraw(draw);
        }
        
        // update times
        StartTime = Timestamp.MaxValue;
        EndTime = Timestamp.Zero;

        foreach (var framer in Modules.Values)
        {
            if (!framer.StartTime.HasValue || !framer.EndTime.HasValue) continue;

            StartTime = Timestamp.Min(StartTime, framer.StartTime.Value);
            EndTime = Timestamp.Max(EndTime, framer.EndTime.Value);
        }

        StartTime = Timestamp.Clamp(StartTime, Timestamp.Zero, EndTime);
    }
}