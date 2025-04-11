using Tyr.Common.Runner;

namespace Tyr.Common.Network;

public class UdpReceiver<T> : IDisposable where T : class
{
    private readonly Action<T> _onData;

    private readonly UdpClient _client;
    private readonly RunnerAsync _runner;

    public UdpReceiver(Address address, Action<T> onData)
    {
        _onData = onData;
        _client = new UdpClient(address);

        _runner = new RunnerAsync(Tick);
        _runner.Start();
    }

    private async Task Tick(CancellationToken token)
    {
        var packet = await _client.Receive<T>(token);
        if (packet == null)
        {
            Logger.ZLogError($"Received null {typeof(T).Name} packet");
            return;
        }

        Logger.ZLogTrace($"Received {typeof(T).Name} packet from {_client.GetLastReceiveEndpoint()}");

        _onData(packet);
    }

    public void Dispose()
    {
        _runner.Stop();
        _client.Dispose();
    }
}