using Timer = System.Timers.Timer;

namespace Tyr.Common.Config;

public sealed class Debouncer : IDisposable
{
    private readonly Timer _timer;
    private readonly object _lock = new();

    public Debouncer(int delayMs, Action action)
    {
        _timer = new Timer(delayMs) { AutoReset = false };
        _timer.Elapsed += (_, _) =>
        {
            lock (_lock)
            {
                action();
            }
        };
    }

    public void Trigger()
    {
        lock (_lock)
        {
            _timer.Stop();
            _timer.Start();
        }
    }

    public void Cancel()
    {
        lock (_lock)
        {
            _timer.Stop();
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}