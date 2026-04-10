using Tyr.Gui.Data;
using Tyr.Gui.Views;
using Tyr.Common.Time;

namespace Tyr.Gui.Pipeline;

internal sealed class GuiFramePipeline : IDisposable
{
    private readonly Action<PrepareRequest, PreparedFrame> _prepare;
    private readonly Lock _sync = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _thread;
    private readonly Stack<PreparedFrame> _framePool = [];

    private PrepareRequest? _pending;
    private PreparedFrame? _latest;
    private bool _disposed;

    public GuiFramePipeline(Action<PrepareRequest, PreparedFrame> prepare)
    {
        _prepare = prepare;
        _framePool.Push(new PreparedFrame());
        _framePool.Push(new PreparedFrame());
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "GuiFramePipeline"
        };
        _thread.Start();
    }

    public void Enqueue(PrepareRequest request)
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _pending = request;
        }

        _signal.Set();
    }

    public bool TrySwapLatest(ref PreparedFrame current)
    {
        lock (_sync)
        {
            if (_latest is null)
                return false;

            RecycleFrameNoLock(current);
            current = _latest;
            _latest = null;
            return true;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        _signal.Set();
        _thread.Join();
        _signal.Dispose();
    }

    private void Loop()
    {
        while (true)
        {
            _signal.WaitOne();

            PrepareRequest? request;
            lock (_sync)
            {
                if (_disposed)
                    return;

                request = _pending;
                _pending = null;
            }

            if (request is null)
                continue;

            PreparedFrame frame;
            lock (_sync)
            {
                frame = _framePool.Count > 0 ? _framePool.Pop() : new PreparedFrame();
            }

            _prepare(request.Value, frame);

            lock (_sync)
            {
                if (_disposed)
                {
                    RecycleFrameNoLock(frame);
                    return;
                }

                if (_latest is not null)
                    RecycleFrameNoLock(_latest);

                _latest = frame;
            }
        }
    }

    private void RecycleFrameNoLock(PreparedFrame frame)
    {
        frame.Reset();
        _framePool.Push(frame);
    }

    internal readonly record struct PrepareRequest(
        PlaybackTime Time,
        DebugFilterSnapshot FilterSnapshot,
        PlotView.PrepareState PlotState);

    internal sealed class PreparedFrame
    {
        public PlaybackTime Time { get; set; } = new(true, Timestamp.Zero, DeltaTime.Zero);
        public FieldView.PreparedData Field { get; } = new();
        public LogView.PreparedData Log { get; } = new();
        public PlotView.PreparedData Plots { get; } = new();

        public void Reset()
        {
            Field.Reset();
            Log.Reset();
            Plots.Reset();
        }
    }
}
