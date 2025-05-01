using Tyr.Common.Runner;

namespace Tyr.Common.Network;

public class UdpReceiver<T> : IDisposable where T : class
{
    private readonly Action<T> _onData;

    public UdpClient Client { get; }
    public RunnerAsync Runner { get; }

    public UdpReceiver(Address address, Action<T> onData, string? callingModule = null)
    {
        _onData = onData;
        Client = new UdpClient(address);

        Runner = new RunnerAsync(Tick, 0, callingModule);
        Runner.Start();
    }

    private async Task Tick(CancellationToken token)
    {
        var packet = await Client.Receive<T>(token);
        if (packet == null)
        {
            if (!token.IsCancellationRequested)
            {
                Log.ZLogError($"Received null {typeof(T).Name}");
            }

            return;
        }

        Log.ZLogTrace($"Received {typeof(T).Name} from {Client.GetLastReceiveEndpoint()}");

        _onData(packet);
    }

    public void Dispose()
    {
        Runner.Stop();
        Client.Dispose();
    }
}