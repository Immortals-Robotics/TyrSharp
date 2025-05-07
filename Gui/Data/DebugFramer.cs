using Tyr.Common.Dataflow;
using Tyr.Common.Time;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Data;

public class DebugFramer
{
    private readonly Subscriber<Debug.Logging.Entry> _logSubscriber = Hub.Logs.Subscribe(Mode.All);
    private readonly Subscriber<Debug.Drawing.Command> _drawSubscriber = Hub.Draws.Subscribe(Mode.All);
    private readonly Subscriber<Debug.Plotting.Command> _plotSubscriber = Hub.Plots.Subscribe(Mode.All);

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
        var dirty = false;

        while (_frameSubscriber.Reader.TryRead(out var frame))
        {
            GetOrCreateModuleFramer(frame.ModuleName).OnFrame(frame);
            dirty = true;
        }

        while (_logSubscriber.Reader.TryRead(out var log))
        {
            if (log.IsEmpty)
            {
                foreach (var module in Modules.Values)
                {
                    module.OnLog(log);
                }
            }
            else
            {
                GetOrCreateModuleFramer(log.Meta.Module).OnLog(log);
            }

            dirty = true;
        }

        while (_drawSubscriber.Reader.TryRead(out var draw))
        {
            if (draw.IsEmpty)
            {
                foreach (var module in Modules.Values)
                {
                    module.OnDraw(draw);
                }
            }
            else
            {
                GetOrCreateModuleFramer(draw.Meta.Module).OnDraw(draw);
            }

            dirty = true;
        }

        while (_plotSubscriber.Reader.TryRead(out var plot))
        {
            if (plot.IsEmpty)
            {
                foreach (var module in Modules.Values)
                {
                    module.OnPlot(plot);
                }
            }
            else
            {
                GetOrCreateModuleFramer(plot.Meta.Module).OnPlot(plot);
            }

            dirty = true;
        }

        // update time ranges if anything has changed
        if (dirty)
        {
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
}