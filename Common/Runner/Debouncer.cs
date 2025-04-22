using Tyr.Common.Time;
using Timer = System.Timers.Timer;

namespace Tyr.Common.Runner;

/// <summary>
/// A utility that delays execution of an action until a period of inactivity has passed.
/// Useful for coalescing rapid-fire triggers into a single event (e.g., config changes).
/// </summary>
public sealed class Debouncer : IDisposable
{
    private readonly Timer _timer;
    private readonly Lock _lock = new();

    /// <summary>
    /// Creates a new debouncer that runs the given action after the specified delay.
    /// If triggered repeatedly, the action is postponed until no further triggers occur.
    /// </summary>
    /// <param name="delay">The delay to wait before invoking the action.</param>
    /// <param name="action">The action to execute after the delay.</param>
    public Debouncer(DeltaTime delay, Action action)
    {
        _timer = new Timer(delay.ToTimeSpan()) { AutoReset = false };
        _timer.Elapsed += (_, _) =>
        {
            lock (_lock)
            {
                action();
            }
        };
    }

    /// <summary>
    /// Triggers the debouncer. Resets the timer if it's already running.
    /// </summary>
    public void Trigger()
    {
        lock (_lock)
        {
            _timer.Stop();
            _timer.Start();
        }
    }

    /// <summary>
    /// Cancels the current pending action, if any.
    /// </summary>
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