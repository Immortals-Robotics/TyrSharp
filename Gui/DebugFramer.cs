using Tyr.Common.Dataflow;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui;

public class DebugFramer
{
    private readonly Subscriber<Debug.Drawing.Command> _drawCommandsSubscriber = Hub.Draws.Subscribe(Mode.All);
    private readonly Subscriber<Debug.Frame> _frameSubscriber = Hub.Frames.Subscribe(Mode.All);

    public Dictionary<string, ModuleDebugFramer> Modules { get; } = [];

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
    }
}