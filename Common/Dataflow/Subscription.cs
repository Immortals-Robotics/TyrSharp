namespace Tyr.Common.Dataflow;

public class Subscription : IDisposable
{
    private readonly CancellationTokenSource _cts;

    public Subscription(CancellationTokenSource cts)
    {
        _cts = cts;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}